import type { DraftConflictParams } from '@vsite/shared';
import type { SavePageDraftResultDto } from '@vsite/api-sdk';
import { UPDATE_COALESCE_MS, checkTreeLimits, type EditorStore, type TreeEnvelope } from '@vsite/builder-core';
import { classifySaveError } from './classify-save-error';
import { mapTreeErrors, type MappedTreeErrors } from './map-tree-errors';
import { sameJson } from './same-json';

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

/** Kết quả `prepareForPublish`. `ok: false` ⇒ controller KHÔNG khoá (autosave chạy tiếp như thường). */
export type PrepareForPublishResult =
  | { ok: true }
  | { ok: false; reason: 'invalid' | 'conflict' | 'blocked' | 'error' };

type SaveFailureReason = 'invalid' | 'conflict' | 'blocked' | 'error';

/**
 * Kết quả một lượt `flush`. `saved`: cây hiện tại (lúc bắt đầu lượt) đã nằm trên server — có PUT hoặc không cần PUT.
 * `skipped`: không làm gì (đang khoá, đang có PUT bay → hẹn lưu tiếp, phiên đã dispose).
 */
type FlushOutcome = { kind: 'saved' } | { kind: 'failed'; reason: SaveFailureReason } | { kind: 'skipped' };

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
 * - **Khoá cho publish** (R9, WEBSITE-006): `prepareForPublish` chờ PUT đang bay, lưu ngay phần còn lại rồi khoá;
 *   khoá kéo dài tới `finishPublish` (cả lúc dialog ghi chú đang mở — G5). Trong lúc khoá KHÔNG có PUT nào (kể cả
 *   `retry`): sửa cây chỉ đánh dấu `unsaved`. `finishPublish` GET lấy `version` (publish hạ `isDirty` ⇒ version draft
 *   đổi), KHÔNG đè cây và không đổi `savedTree`. Chỉ nhận `version` khi cây GET về trùng cấu trúc với `savedTree`;
 *   khác (tab khác vừa lưu) / null / GET lỗi (R3) ⇒ giữ `version` cũ — PUT kế tiếp nhận 409 và dialog xung đột lo.
 *   Mở khoá xong, phần gõ trong lúc khoá được lưu ngay. `prepareForPublish` trả `ok: false` thì không để lại khoá.
 * - **Thứ tự gọi**: `prepareForPublish` → (người xác nhận, POST publish — thành công hay lỗi) → `finishPublish`, đúng
 *   một cặp cho mỗi lần publish. Cặp này CHỈ dành cho publish từ editor, không dùng cho rollback. Gọi chồng được xếp hàng: prepare khi finish còn chạy
 *   ⇒ chờ finish rồi chuẩn bị thật; finish khi prepare còn chạy ⇒ chờ prepare rồi finish (prepare lỗi ⇒ không làm gì).
 * - **Phiên** (`session`): `dispose()` tăng số phiên; mọi đoạn chạy tiếp sau `await` (flush, prepare, finish, xử lý 409,
 *   sanitized) so số phiên đã chụp — phiên cũ không được ghi gì vào phiên mới (StrictMode mount → cleanup → mount).
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
  /** Tăng mỗi lần `dispose()`; đoạn chạy sau `await` chụp số này để biết mình còn thuộc phiên hiện tại không. */
  private session = 0;
  /**
   * `preparing`: đang chạy `prepareForPublish`; `locked`: đã khoá, chờ `finishPublish`;
   * `finishing`: `finishPublish` đang GET — vẫn khoá.
   */
  private lock: 'none' | 'preparing' | 'locked' | 'finishing' = 'none';
  private preparing: Promise<PrepareForPublishResult> | undefined;
  private finishing: Promise<void> | undefined;
  /** Cây của lần gửi/kiểm tra thất bại gần nhất — để biết "cây chưa đổi kể từ lỗi" (gửi lại cũng y hệt). */
  private lastFailedTree: TreeEnvelope | null = null;
  /** Lượt `flush` đang gửi; `prepareForPublish` chờ nó (được thả cả khi dispose). */
  private idle: Promise<void> | undefined;
  private releaseIdle: (() => void) | undefined;
  /** Phiên `gen` còn sống? Là hàm (không phải field) nên TS không thu hẹp sau `await`. */
  private alive(gen: number): boolean {
    return !this.disposed && this.session === gen;
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
    this.session += 1;
    clearTimeout(this.timer);
    this.timer = undefined;
    this.unsubscribeStore?.();
    this.unsubscribeStore = undefined;
    this.inFlight = false;
    this.pendingFlush = false;
    this.lock = 'none';
    this.preparing = undefined;
    this.finishing = undefined;
    // Ai đang chờ lượt gửi (prepareForPublish) thì thả ra — họ tự thấy phiên đã đổi.
    const release = this.releaseIdle;
    this.idle = undefined;
    this.releaseIdle = undefined;
    release?.();
  }

  // ── Điều khiển ──────────────────────────────────────────────────────────────
  /** Nút "Thử lại": lưu ngay, bỏ qua debounce. */
  retry() {
    if (this.state.conflict || this.lock !== 'none') return;
    this.cancelTimer();
    void this.flush();
  }

  /** 409 → "Tải bản mới": bỏ thay đổi ở tab này, nạp bản đang có trên server. */
  async reloadLatest() {
    if (!this.state.conflict || this.state.conflict.resolving) return;
    this.setConflict({ resolving: true, resolveFailed: false });
    const gen = this.session;
    try {
      const latest = await this.deps.fetchDraft();
      if (!this.alive(gen)) return;
      if (!latest.tree) throw new Error('draft unreadable');
      // Đặt mốc "đã lưu" TRƯỚC khi nạp để onTreeChanged thấy cây === savedTree (không lên lịch lưu).
      this.version = latest.version;
      this.savedTree = latest.tree;
      this.deps.store.getState().loadDraft(latest.tree, this.deps.store.getState().pageKind);
      this.cancelTimer();
      this.setState({ status: 'saved', conflict: null, blocked: null, invalid: null });
    } catch {
      if (this.alive(gen)) this.setConflict({ resolving: false, resolveFailed: true });
    }
  }

  /**
   * 409 → "Giữ bản của tôi": lấy `version` bằng GET mới (R8) rồi PUT cây local. Chỉ lấy `version` từ GET,
   * không đụng cây local. Nếu giữa lúc đó lại có bản mới hơn thì PUT sẽ 409 lần nữa → dialog hiện lại.
   */
  async keepMine() {
    if (!this.state.conflict || this.state.conflict.resolving) return;
    this.setConflict({ resolving: true, resolveFailed: false });
    const gen = this.session;
    try {
      const latest = await this.deps.fetchDraft();
      if (!this.alive(gen)) return;
      this.version = latest.version;
      this.setState({ conflict: null, status: 'unsaved' });
      this.cancelTimer();
      await this.flush();
    } catch {
      if (this.alive(gen)) this.setConflict({ resolving: false, resolveFailed: true });
    }
  }

  /**
   * Trước khi publish: chờ PUT đang bay, lưu ngay phần còn lại (huỷ debounce), rồi khoá autosave.
   * `ok: false` ⇒ không khoá. Gọi lại khi đang chuẩn bị ⇒ dùng chung kết quả; khi đã khoá ⇒ `ok: true`.
   */
  prepareForPublish(): Promise<PrepareForPublishResult> {
    if (this.disposed) return Promise.resolve({ ok: false, reason: 'error' });
    if (this.finishing) {
      // Publish trước còn đang mở khoá: chờ nó xong (phần gõ trong lúc khoá được đưa đi) rồi chuẩn bị thật.
      const gen = this.session;
      return this.finishing.then(() =>
        this.alive(gen) ? this.prepareForPublish() : { ok: false as const, reason: 'error' as const },
      );
    }
    if (this.lock === 'locked') return Promise.resolve({ ok: true });
    if (this.preparing) return this.preparing;
    if (this.state.conflict) return Promise.resolve({ ok: false, reason: 'conflict' });
    const run = this.runPrepare();
    this.preparing = run;
    return run;
  }

  /**
   * Sau publish (thành công hay lỗi): GET lấy `version` — KHÔNG đè cây — rồi mở khoá; phần gõ trong lúc khoá được lưu
   * ngay. Chỉ nhận `version` khi cây GET về trùng `savedTree`; GET lỗi ⇒ giữ `version` cũ (R3). Không khoá ⇒ no-op.
   * Đang prepare ⇒ chờ prepare rồi mới quyết. Không bao giờ ném.
   */
  finishPublish(): Promise<void> {
    if (this.disposed) return Promise.resolve();
    if (this.finishing) return this.finishing;
    if (this.preparing) {
      const gen = this.session;
      return this.preparing.then(() => (this.alive(gen) ? this.finishPublish() : undefined));
    }
    if (this.lock !== 'locked') return Promise.resolve();
    const run = this.runFinish();
    this.finishing = run;
    return run;
  }

  // ── Nội bộ ──────────────────────────────────────────────────────────────────
  private async runFinish(): Promise<void> {
    const gen = this.session;
    this.lock = 'finishing';
    try {
      const latest = await this.deps.fetchDraft();
      if (!this.alive(gen)) return;
      // Cây trên server đúng là cây đã lưu (publish chỉ hạ isDirty — G6) ⇒ nhận version. Khác ⇒ có người khác vừa lưu:
      // giữ version cũ để PUT kế tiếp 409 và dialog xung đột hiện — tuyệt đối không nạp cây.
      if (latest.tree && sameJson(latest.tree, this.savedTree)) this.version = latest.version;
    } catch {
      if (!this.alive(gen)) return;
      // R3: giữ version cũ; nếu PUT sau đó 409 thì dialog xung đột lo, cây local không mất.
    }
    this.lock = 'none';
    this.finishing = undefined;
    if (this.state.conflict) return;
    if (this.deps.store.getState().tree !== this.savedTree) {
      this.cancelTimer();
      void this.flush();
    }
  }

  private async runPrepare(): Promise<PrepareForPublishResult> {
    const gen = this.session;
    this.lock = 'preparing';
    this.cancelTimer();
    const result = await this.saveRemainder(gen);
    if (!this.alive(gen)) return { ok: false, reason: 'error' };
    this.preparing = undefined;
    if (result.ok) {
      this.lock = 'locked';
    } else {
      this.lock = 'none';
      this.resumeAfterUnlock();
    }
    return result;
  }

  /** Lõi của prepare: chờ lượt gửi đang bay rồi lưu ngay phần chưa lưu. Chạy khi `lock === 'preparing'`. */
  private async saveRemainder(gen: number): Promise<PrepareForPublishResult> {
    while (this.idle && this.alive(gen)) await this.idle;
    if (!this.alive(gen)) return { ok: false, reason: 'error' };
    if (this.state.conflict) return { ok: false, reason: 'conflict' };

    const tree = this.deps.store.getState().tree;
    if (tree === this.savedTree) return { ok: true };
    // Lỗi nội dung/kích thước mà cây chưa đổi kể từ lỗi → gửi lại cũng y hệt: không PUT.
    const { status } = this.state;
    if ((status === 'invalid' || status === 'blocked') && tree === this.lastFailedTree) {
      return { ok: false, reason: status };
    }
    // Một lượt lưu ngay, bỏ qua khoá đang chuẩn bị. Gõ thêm trong lúc PUT bay thuộc về sau khoá — lưu sau `finishPublish`.
    const outcome = await this.flush(true);
    if (!this.alive(gen)) return { ok: false, reason: 'error' };
    switch (outcome.kind) {
      case 'saved':
        return { ok: true };
      case 'failed':
        return { ok: false, reason: outcome.reason };
      case 'skipped':
        return { ok: false, reason: 'error' };
    }
  }

  /** Prepare thất bại: autosave chạy tiếp nếu có thay đổi chưa thử (gõ trong lúc chuẩn bị). */
  private resumeAfterUnlock() {
    if (this.disposed || this.state.conflict) return;
    const tree = this.deps.store.getState().tree;
    if (tree === this.savedTree || tree === this.lastFailedTree || this.timer !== undefined) return;
    this.timer = setTimeout(() => {
      this.timer = undefined;
      void this.flush();
    }, this.debounceMs);
  }

  /** Đánh dấu có lượt gửi; trả hàm thả (luôn resolve chính promise của nó, chỉ xoá `idle` nếu chưa bị thay). */
  private markBusy(): () => void {
    let resolve!: () => void;
    const promise = new Promise<void>((r) => {
      resolve = r;
    });
    this.idle = promise;
    this.releaseIdle = resolve;
    return () => {
      if (this.idle === promise) {
        this.idle = undefined;
        this.releaseIdle = undefined;
      }
      resolve();
    };
  }

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
    if (this.lock !== 'none') return; // R9: đang publish → chỉ đánh dấu, không hẹn giờ PUT
    clearTimeout(this.timer);
    this.timer = setTimeout(() => {
      this.timer = undefined;
      void this.flush();
    }, this.debounceMs);
  }

  /** `force`: do `prepareForPublish` gọi — được gửi dù đang chuẩn bị khoá. */
  private async flush(force = false): Promise<FlushOutcome> {
    if (this.disposed) return { kind: 'skipped' };
    if (this.state.conflict) return { kind: 'failed', reason: 'conflict' };
    if (this.lock !== 'none' && !force) return { kind: 'skipped' }; // R9: không PUT trong lúc publish
    if (this.inFlight) {
      this.pendingFlush = true; // xong request đang bay thì lưu tiếp (R1)
      return { kind: 'skipped' };
    }
    this.pendingFlush = false;

    const tree = this.deps.store.getState().tree;
    if (!tree || tree === this.savedTree) {
      this.setState({ status: 'saved', blocked: null });
      return { kind: 'saved' };
    }

    const limit = checkTreeLimits(tree);
    if (!limit.ok) {
      // R6: không gửi. Cây vẫn nằm ở store nên undo được; đổi tiếp thì debounce kiểm lại.
      this.lastFailedTree = tree;
      this.setState({ status: 'blocked', blocked: { code: limit.code, message: limit.message } });
      return { kind: 'failed', reason: 'blocked' };
    }

    const gen = this.session;
    this.inFlight = true;
    const release = this.markBusy();
    this.setState({ status: 'saving', blocked: null });
    let outcome: FlushOutcome;
    try {
      try {
        const result = await this.deps.save(tree, this.version);
        // Phiên đã dispose (kể cả đã start lại): không ghi gì — `inFlight` đã được dispose trả về false.
        if (!this.alive(gen)) return { kind: 'skipped' };
        this.version = result.version;
        this.savedTree = tree;
        this.lastFailedTree = null;
        this.inFlight = false;
        // Lưu thành công → lỗi 422 cũ hết hiệu lực (R5).
        this.setState({ status: this.deps.store.getState().tree === tree ? 'saved' : 'unsaved', invalid: null, blocked: null });
        outcome = { kind: 'saved' };
        if (result.sanitized) await this.reloadSanitized(tree, gen);
      } catch (error) {
        if (!this.alive(gen)) return { kind: 'skipped' };
        this.inFlight = false;
        this.lastFailedTree = tree;
        outcome = { kind: 'failed', reason: this.handleFailure(error) };
      }
      if (this.alive(gen)) this.afterAttempt(tree);
      return outcome;
    } finally {
      release();
    }
  }

  /** `sanitized: true` (R2): BE đã làm sạch cây → GET rồi thay cây, nhưng chỉ khi người dùng chưa sửa tiếp. */
  private async reloadSanitized(sentTree: TreeEnvelope, gen: number) {
    try {
      const latest = await this.deps.fetchDraft();
      if (!this.alive(gen) || !latest.tree) return;
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

  private handleFailure(error: unknown): SaveFailureReason {
    const failure = classifySaveError(error);
    switch (failure.kind) {
      case 'conflict':
        this.cancelTimer();
        this.setState({
          status: 'conflict',
          conflict: { params: failure.params, resolving: false, resolveFailed: false },
        });
        return 'conflict';
      case 'invalid': {
        const tree = this.deps.store.getState().tree;
        const mapped = tree
          ? mapTreeErrors(tree, failure.params ?? { errors: [], truncated: false })
          : { byNode: new Map(), banner: [], truncated: false, total: 0 };
        this.setState({ status: 'invalid', invalid: { code: failure.code, mapped } });
        return 'invalid';
      }
      case 'too-large':
        this.setState({
          status: 'blocked',
          blocked: { code: 'TREE_BODY_TOO_LARGE', message: this.deps.messageFor('TREE_BODY_TOO_LARGE') },
        });
        return 'blocked';
      case 'transient':
        this.setState({ status: 'error' });
        return 'error';
    }
  }

  /** Sau mỗi lượt gửi: nếu người dùng sửa tiếp trong lúc bay mà hẹn giờ đã trôi qua, lưu tiếp ngay (R1). */
  private afterAttempt(sentTree: TreeEnvelope) {
    if (this.disposed || this.state.conflict) return;
    if (this.lock !== 'none') {
      // R9: đang chuẩn bị/khoá publish — prepare tự lo phần còn lại, `finishPublish` lưu phần gõ trong lúc khoá.
      this.pendingFlush = false;
      return;
    }
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
