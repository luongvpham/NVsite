import type { DraftConflictParams } from '@vsite/shared';
import type { SavePageDraftResultDto } from '@vsite/api-sdk';
import { UPDATE_COALESCE_MS, checkTreeLimits, type EditorStore, type TreeEnvelope } from '@vsite/builder-core';
import { classifySaveError } from './classify-save-error';
import { mapTreeErrors, type MappedTreeErrors } from './map-tree-errors';

/** Nhịp lưu = nhịp gộp bước undo của store: một "đợt gõ" là một bước undo và một lần lưu. */
export const AUTOSAVE_DEBOUNCE_MS = UPDATE_COALESCE_MS;

export type AutosaveStatus =
  | 'saved'
  | 'unsaved' // có thay đổi, đang chờ debounce
  | 'saving'
  | 'error' // mạng/5xx — thử lại khi đổi tiếp hoặc bấm Thử lại
  | 'blocked' // quá lớn (client chặn hoặc BE 413) — không gửi cho tới khi cây nhỏ đi
  | 'invalid' // 422 — sửa chỗ đỏ
  | 'conflict'; // 409 — dừng, chờ người chọn

export interface AutosaveState {
  status: AutosaveStatus;
  /** Lý do không gửi/không lưu được vì kích thước. Xoá khi lưu thành công hoặc cây về bản đã lưu. */
  blocked: { code: string; message: string } | null;
  /** Lỗi 422 mới nhất, đã map theo node của cây hiện tại. Xoá khi một lần lưu kế tiếp thành công (R5). */
  invalid: { code: string; mapped: MappedTreeErrors } | null;
  conflict: { params: DraftConflictParams | undefined; resolving: boolean; resolveFailed: boolean } | null;
}

/** Bản draft vừa GET lại. `tree` null = vỏ cây không đọc được (Zod). `version` lấy nguyên văn từ response. */
export interface FetchedDraft {
  version: number;
  tree: TreeEnvelope | null;
}

export interface AutosaveDeps {
  store: EditorStore;
  /** `version` của draft lúc mở phiên, nguyên văn từ GET. */
  initialVersion: number;
  save: (tree: TreeEnvelope, version: number) => Promise<SavePageDraftResultDto>;
  fetchDraft: () => Promise<FetchedDraft>;
  /** Thông báo cho `blocked` khi BE trả 413 (map từ error_code ở tầng UI). */
  messageFor: (code: string) => string;
  debounceMs?: number;
}

/**
 * Autosave một bản nháp (Flow 5). Thuần TS (không React) để test bằng fake timer.
 *
 * Bất biến:
 * - **Không bao giờ hai PUT song song** (R1): `inFlight` chặn; đổi tiếp trong lúc bay → lưu tiếp SAU khi xong.
 * - `version` opaque: chỉ gán từ response PUT/GET, không so sánh/tăng. 409 "Giữ bản của tôi" lấy version bằng GET mới (R8),
 *   không dùng `params.currentVersion`.
 * - Response KHÔNG ghi đè cây local (R1). Ngoại lệ duy nhất: `sanitized` → nạp lại, và CHỈ khi cây hiện tại đúng là cây
 *   vừa gửi (R2) — không thì để lần lưu sạch kế tiếp.
 * - "Đã lưu" = tham chiếu cây hiện tại === tham chiếu cây của lần PUT thành công gần nhất (Immer giữ nguyên tham chiếu
 *   khi op không đổi gì, nên không có "bẩn giả").
 */
export class AutosaveController {
  private readonly deps: AutosaveDeps;
  private readonly debounceMs: number;
  private version: number;
  private savedTree: TreeEnvelope | null;
  private inFlight = false;
  private pendingFlush = false;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private disposed = false;
  /** Getter để TS không thu hẹp `disposed` thành `false` sau `await` (dispose có thể chạy trong lúc chờ). */
  private get stopped(): boolean {
    return this.disposed;
  }
  private readonly listeners = new Set<() => void>();
  private unsubscribeStore: (() => void) | undefined;
  private state: AutosaveState = { status: 'saved', blocked: null, invalid: null, conflict: null };

  constructor(deps: AutosaveDeps) {
    this.deps = deps;
    this.debounceMs = deps.debounceMs ?? AUTOSAVE_DEBOUNCE_MS;
    this.version = deps.initialVersion;
    this.savedTree = deps.store.getState().tree;
    this.disposed = true; // chưa chạy cho tới khi `start()`
  }

  /**
   * Bắt đầu theo dõi store. Gọi trong `useEffect` và ghép với `dispose()` ở cleanup — StrictMode mount → cleanup → mount
   * nên `start()` phải chạy lại được trên cùng một controller.
   */
  start() {
    if (!this.disposed) return;
    this.disposed = false;
    this.unsubscribeStore = this.deps.store.subscribe((state, prev) => {
      if (state.tree !== prev.tree) this.onTreeChanged();
    });
  }

  // ── Quan sát (useSyncExternalStore) ─────────────────────────────────────────
  subscribe = (listener: () => void): (() => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };

  getState = (): AutosaveState => this.state;

  /** Còn thay đổi chưa lên server (kể cả đang bay hoặc bị lỗi) — cho cảnh báo rời trang. */
  get hasUnsavedChanges(): boolean {
    return this.deps.store.getState().tree !== this.savedTree;
  }

  private setState(patch: Partial<AutosaveState>) {
    this.state = { ...this.state, ...patch };
    for (const listener of this.listeners) listener();
  }

  dispose() {
    this.disposed = true;
    clearTimeout(this.timer);
    this.timer = undefined;
    this.unsubscribeStore?.();
    this.unsubscribeStore = undefined;
    this.inFlight = false;
    this.pendingFlush = false;
  }

  // ── Điều khiển ──────────────────────────────────────────────────────────────
  /** Nút "Thử lại": lưu ngay, bỏ qua debounce. */
  retry() {
    if (this.state.conflict) return;
    this.cancelTimer();
    void this.flush();
  }

  /** 409 → "Tải bản mới": bỏ thay đổi ở tab này, nạp bản đang có trên server. */
  async reloadLatest() {
    if (!this.state.conflict || this.state.conflict.resolving) return;
    this.setConflict({ resolving: true, resolveFailed: false });
    try {
      const latest = await this.deps.fetchDraft();
      if (this.disposed) return;
      if (!latest.tree) throw new Error('draft unreadable');
      // Đặt mốc "đã lưu" TRƯỚC khi nạp để onTreeChanged thấy cây === savedTree (không lên lịch lưu).
      this.version = latest.version;
      this.savedTree = latest.tree;
      this.deps.store.getState().loadDraft(latest.tree, this.deps.store.getState().pageKind);
      this.cancelTimer();
      this.setState({ status: 'saved', conflict: null, blocked: null, invalid: null });
    } catch {
      if (!this.disposed) this.setConflict({ resolving: false, resolveFailed: true });
    }
  }

  /**
   * 409 → "Giữ bản của tôi": lấy `version` bằng GET mới (R8) rồi PUT cây local. Chỉ lấy `version` từ GET,
   * không đụng cây local. Nếu giữa lúc đó lại có bản mới hơn thì PUT sẽ 409 lần nữa → dialog hiện lại.
   */
  async keepMine() {
    if (!this.state.conflict || this.state.conflict.resolving) return;
    this.setConflict({ resolving: true, resolveFailed: false });
    try {
      const latest = await this.deps.fetchDraft();
      if (this.disposed) return;
      this.version = latest.version;
      this.setState({ conflict: null, status: 'unsaved' });
      this.cancelTimer();
      await this.flush();
    } catch {
      if (!this.disposed) this.setConflict({ resolving: false, resolveFailed: true });
    }
  }

  // ── Nội bộ ──────────────────────────────────────────────────────────────────
  private setConflict(patch: Partial<NonNullable<AutosaveState['conflict']>>) {
    const current = this.state.conflict;
    if (current) this.setState({ conflict: { ...current, ...patch } });
  }

  private cancelTimer() {
    clearTimeout(this.timer);
    this.timer = undefined;
    this.pendingFlush = false;
  }

  private onTreeChanged() {
    if (this.disposed || this.state.conflict) return; // 409: autosave đã dừng
    if (this.deps.store.getState().tree === this.savedTree) {
      // Undo về đúng bản đã lưu: không còn gì để gửi.
      this.cancelTimer();
      if (!this.inFlight) this.setState({ status: 'saved', blocked: null });
      return;
    }
    if (!this.inFlight) this.setState({ status: 'unsaved' });
    clearTimeout(this.timer);
    this.timer = setTimeout(() => {
      this.timer = undefined;
      void this.flush();
    }, this.debounceMs);
  }

  private async flush(): Promise<void> {
    if (this.disposed || this.state.conflict) return;
    if (this.inFlight) {
      this.pendingFlush = true; // xong request đang bay thì lưu tiếp (R1)
      return;
    }
    this.pendingFlush = false;

    const tree = this.deps.store.getState().tree;
    if (!tree || tree === this.savedTree) {
      this.setState({ status: 'saved', blocked: null });
      return;
    }

    const limit = checkTreeLimits(tree);
    if (!limit.ok) {
      // R6: không gửi. Cây vẫn nằm ở store nên undo được; đổi tiếp thì debounce kiểm lại.
      this.setState({ status: 'blocked', blocked: { code: limit.code, message: limit.message } });
      return;
    }

    this.inFlight = true;
    this.setState({ status: 'saving', blocked: null });
    try {
      const result = await this.deps.save(tree, this.version);
      if (this.stopped) return;
      this.version = result.version;
      this.savedTree = tree;
      this.inFlight = false;
      // Lưu thành công → lỗi 422 cũ hết hiệu lực (R5).
      this.setState({ status: this.deps.store.getState().tree === tree ? 'saved' : 'unsaved', invalid: null, blocked: null });
      if (result.sanitized) await this.reloadSanitized(tree);
    } catch (error) {
      this.inFlight = false;
      if (this.stopped) return;
      this.handleFailure(error);
    }
    this.afterAttempt(tree);
  }

  /** `sanitized: true` (R2): BE đã làm sạch cây → GET rồi thay cây, nhưng chỉ khi người dùng chưa sửa tiếp. */
  private async reloadSanitized(sentTree: TreeEnvelope) {
    try {
      const latest = await this.deps.fetchDraft();
      if (this.disposed || !latest.tree) return;
      // Đã sửa tiếp (trong lúc PUT hoặc lúc GET) → không đè việc đang làm; lần lưu sạch kế tiếp sẽ nạp lại.
      if (this.deps.store.getState().tree !== sentTree) return;
      this.version = latest.version;
      this.savedTree = latest.tree;
      // Cùng trang, chỉ prop được làm sạch → giữ khối đang chọn (Inspector không đóng giữa lúc gõ).
      this.deps.store.getState().loadDraft(latest.tree, this.deps.store.getState().pageKind, { keepSelection: true });
      this.setState({ status: 'saved' });
    } catch {
      // Không GET được thì giữ cây local; bản trên server đã sạch, lần lưu kế tiếp sẽ thử nạp lại.
    }
  }

  private handleFailure(error: unknown) {
    const failure = classifySaveError(error);
    switch (failure.kind) {
      case 'conflict':
        this.cancelTimer();
        this.setState({
          status: 'conflict',
          conflict: { params: failure.params, resolving: false, resolveFailed: false },
        });
        return;
      case 'invalid': {
        const tree = this.deps.store.getState().tree;
        const mapped = tree
          ? mapTreeErrors(tree, failure.params ?? { errors: [], truncated: false })
          : { byNode: new Map(), banner: [], truncated: false, total: 0 };
        this.setState({ status: 'invalid', invalid: { code: failure.code, mapped } });
        return;
      }
      case 'too-large':
        this.setState({
          status: 'blocked',
          blocked: { code: 'TREE_BODY_TOO_LARGE', message: this.deps.messageFor('TREE_BODY_TOO_LARGE') },
        });
        return;
      case 'transient':
        this.setState({ status: 'error' });
        return;
    }
  }

  /** Sau mỗi lượt gửi: nếu người dùng sửa tiếp trong lúc bay mà hẹn giờ đã trôi qua, lưu tiếp ngay (R1). */
  private afterAttempt(sentTree: TreeEnvelope) {
    if (this.disposed || this.state.conflict) return;
    const dirty = this.deps.store.getState().tree !== this.savedTree;
    if (!dirty) return;
    const failed = ['error', 'blocked', 'invalid'].includes(this.state.status);
    // Lỗi mà cây không đổi kể từ lúc gửi → gửi lại cũng y hệt. Có sửa tiếp trong lúc bay (có thể chính là chỗ sửa lỗi) thì thử lại.
    if (failed && this.deps.store.getState().tree === sentTree) {
      this.pendingFlush = false;
      return;
    }
    if (this.pendingFlush) {
      this.pendingFlush = false;
      void this.flush();
    } else if (this.timer === undefined) {
      this.timer = setTimeout(() => {
        this.timer = undefined;
        void this.flush();
      }, this.debounceMs);
    }
  }
}
