import { temporal, type TemporalState } from 'zundo';
import { createStore, type StoreApi } from 'zustand/vanilla';
import { applyOperation } from '../operations/apply';
import { OK, fail, type OperationResult } from '../operations/result';
import { parseOperation } from '../operations/types';
import { validateOperation, validateThemeTokens } from '../operations/validate';
import { findNode } from '../tree/query';
import { PageKind, type TreeEnvelope } from '../tree/types';

/** Số bước undo giữ lại (#42: chỉ client, trong một phiên — không persist op-log). */
const HISTORY_LIMIT = 100;

/**
 * Các op `update` liên tiếp cùng `nodeId` + `path` cách nhau chưa tới chừng này gộp vào MỘT bước undo (gõ một câu ở Inspector
 * = một bước, không phải một bước mỗi ký tự — lịch sử chỉ 100 bước). Cửa sổ trượt: mỗi lần gõ làm mới mốc.
 * Cố ý bằng nhịp debounce autosave (2 s) — một "đợt gõ" cũng là một lần lưu.
 */
export const UPDATE_COALESCE_MS = 2000;

export interface EditorStoreOptions {
  /** Đồng hồ (ms) — chỉ để test; mặc định `Date.now`. */
  now?: () => number;
  /** Ghi đè `UPDATE_COALESCE_MS` (test). */
  coalesceMs?: number;
}

const samePath = (a: readonly (string | number)[], b: readonly (string | number)[]) =>
  a.length === b.length && a.every((seg, i) => seg === b[i]);

export interface EditorState {
  /** Cây đang sửa của trang đang mở; `null` khi chưa nạp draft. Đây là **bản làm việc**, nạp một lần từ Query (#20). */
  tree: TreeEnvelope | null;
  pageKind: PageKind;
  selectedId: string | null;
  /** Theme đang sửa (preview sống trên canvas); ngoài lịch sử undo của cây. `null` = chưa nạp. */
  themeDraft: unknown;

  /** Nạp bản nháp vừa tải: đặt cây + loại trang, bỏ chọn, **xoá lịch sử** (undo không lùi về trước bản đã tải). */
  loadDraft: (tree: TreeEnvelope, pageKind: PageKind) => void;
  /** Đặt bản gốc của theme (từ `GET /theme`) — không vào lịch sử. */
  loadTheme: (tokens: unknown) => void;
  /** Cửa DUY NHẤT thay đổi cây/theme: parse → validate → apply. Không ném lỗi; op bị từ chối không đổi gì. */
  dispatch: (op: unknown) => OperationResult;
  select: (nodeId: string | null) => void;
  undo: () => void;
  redo: () => void;
  canUndo: () => boolean;
  canRedo: () => boolean;
}

/** Phần được lưu vào lịch sử undo: CHỈ cây. Chọn node và theme không phải bước undo. */
type History = Pick<EditorState, 'tree'>;

/** Bỏ chọn nếu node đang chọn không còn trong cây (bị xoá, hoặc undo/redo làm biến mất). */
function reconcileSelection(tree: TreeEnvelope | null, selectedId: string | null): string | null {
  return tree && selectedId && findNode(tree, selectedId) ? selectedId : null;
}

/**
 * Store vanilla (không phụ thuộc React → test được ở Node, dùng ở UI qua `useStore(store, selector)`).
 * Mỗi phiên editor tạo một store riêng.
 */
export function createEditorStore(options: EditorStoreOptions = {}): EditorStore {
  const now = options.now ?? Date.now;
  const coalesceMs = options.coalesceMs ?? UPDATE_COALESCE_MS;
  /** Op `update` gần nhất đã đổi cây — để gộp bước undo. Mọi thứ khác (op khác, undo/redo, nạp lại) xoá nó. */
  let lastUpdate: { nodeId: string; path: readonly (string | number)[]; at: number } | null = null;

  const store: EditorStore = createStore<EditorState>()(
    temporal<EditorState, [], [], History>(
      (set, get) => ({
        tree: null,
        pageKind: PageKind.Composable,
        selectedId: null,
        themeDraft: null,

        loadDraft: (tree, pageKind) => {
          lastUpdate = null;
          set({ tree, pageKind, selectedId: null });
          store.temporal.getState().clear();
        },

        loadTheme: (tokens) => {
          set({ themeDraft: tokens });
        },

        dispatch: (input) => {
          const parsed = parseOperation(input);
          if (!parsed.ok) return { ok: false, code: parsed.code, message: parsed.message };
          const { op } = parsed;

          // Theme thuộc website chứ không thuộc cây của trang → không cần draft, không vào lịch sử cây.
          if (op.type === 'changeTheme') {
            const checked = validateThemeTokens(op.tokens);
            if (!checked.ok) return checked;
            set({ themeDraft: op.tokens });
            return OK;
          }

          const { tree, pageKind, selectedId } = get();
          if (!tree) return fail('NO_DRAFT');
          const checked = validateOperation(op, { tree, pageKind });
          if (!checked.ok) return checked;

          const next = applyOperation(tree, op);
          if (next === tree) return OK; // op không đổi gì: không đụng lịch sử, không đổi mốc gộp

          const at = now();
          const merge =
            op.type === 'update' &&
            lastUpdate !== null &&
            lastUpdate.nodeId === op.nodeId &&
            samePath(lastUpdate.path, op.path) &&
            at - lastUpdate.at < coalesceMs;
          lastUpdate = op.type === 'update' ? { nodeId: op.nodeId, path: op.path, at } : null;

          const patch = { tree: next, selectedId: reconcileSelection(next, selectedId) };
          if (merge) {
            // Cùng đợt gõ: đổi cây nhưng không ghi bước mới (bước của lần gõ đầu đã giữ bản trước đợt).
            const temporal = store.temporal.getState();
            temporal.pause();
            try {
              set(patch);
            } finally {
              temporal.resume();
            }
          } else {
            set(patch);
          }
          return OK;
        },

        select: (nodeId) => {
          const { tree, selectedId } = get();
          if (nodeId === null) set({ selectedId: null });
          else if (tree && findNode(tree, nodeId)) set({ selectedId: nodeId });
          else set({ selectedId });
        },

        undo: () => {
          lastUpdate = null;
          store.temporal.getState().undo();
          set((state) => ({ selectedId: reconcileSelection(state.tree, state.selectedId) }));
        },

        redo: () => {
          lastUpdate = null;
          store.temporal.getState().redo();
          set((state) => ({ selectedId: reconcileSelection(state.tree, state.selectedId) }));
        },

        canUndo: () => store.temporal.getState().pastStates.length > 0,
        canRedo: () => store.temporal.getState().futureStates.length > 0,
      }),
      {
        partialize: ({ tree }) => ({ tree }),
        // Chỉ ghi một bước khi CÂY đổi (so tham chiếu — Immer giữ nguyên tham chiếu khi op không đổi gì).
        equality: (past, current) => past.tree === current.tree,
        limit: HISTORY_LIMIT,
      },
    ),
  );
  return store;
}

/** Store + API lịch sử (`temporal`) của zundo — khai báo tường minh để các action tự tham chiếu `store` không làm TS suy kiểu vòng. */
export type EditorStore = StoreApi<EditorState> & { temporal: StoreApi<TemporalState<History>> };
