import { useMemo } from 'react';
import { useStore } from 'zustand';
import { useGetPageDraft } from '@vsite/api-sdk';
import { treeEnvelopeSchema } from '@vsite/builder-components';
import type { EditorStore, TreeEnvelope } from '@vsite/builder-core';
import { getErrorCode } from '@vsite/shared';
import { getErrorMessage } from '../lib/error-messages';
import { useMediaMap } from './use-media-map';

export type PreviewPageState =
  | { status: 'loading' }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; tree: TreeEnvelope };

/**
 * Nguồn cây cho một trang trong preview (WEBSITE-003 T4, R5):
 * - trang đang sửa → `store.tree` (đúng cái đang thấy, kể cả phần chưa autosave), KHÔNG gọi API;
 * - trang khác → draft đã lưu trên server (`useGetPageDraft`), parse bằng cùng Zod với editor.
 * `pageId = null` (vd. shop chưa có trang NotFound) → cây rỗng.
 */
export function usePreviewPage(opts: {
  shopId: string;
  pageId: string | null;
  editingPageId: string;
  store: EditorStore;
}) {
  const { shopId, pageId, editingPageId, store } = opts;
  const storeTree = useStore(store, (s) => s.tree);
  const isEditing = pageId === editingPageId;

  const query = useGetPageDraft(shopId, pageId ?? '', {
    query: {
      enabled: pageId !== null && !isEditing,
      retry: false,
      staleTime: 0,
      refetchOnWindowFocus: false,
      // Cùng query key với editor (gcTime: 0 ở EditorShell). TanStack lấy gcTime = max các observer, nên preview mà giữ
      // mặc định 5 phút sẽ để lại bản draft cũ trong cache → mở lại editor trang đó trong 5 phút ra cây + version cũ (409 giả).
      gcTime: 0,
    },
  });

  const parsed = useMemo(
    () => (query.data ? treeEnvelopeSchema.safeParse(query.data.tree) : undefined),
    [query.data],
  );

  let state: PreviewPageState;
  if (pageId === null) {
    state = { status: 'ready', tree: { schemaVersion: 1, id: 'root', type: 'Page', children: [] } };
  } else if (isEditing) {
    state = storeTree ? { status: 'ready', tree: storeTree } : { status: 'loading' };
  } else if (query.isError) {
    state = {
      status: 'error',
      message: getErrorMessage(getErrorCode(query.error)),
      retry: () => void query.refetch(),
    };
  } else if (parsed && !parsed.success) {
    state = { status: 'error', message: 'Dữ liệu trang không đọc được.', retry: () => void query.refetch() };
  } else if (parsed?.success && !query.isFetching) {
    state = { status: 'ready', tree: parsed.data };
  } else {
    state = { status: 'loading' };
  }

  const { mediaMap } = useMediaMap(shopId, state.status === 'ready' ? state.tree : null);
  return { state, mediaMap };
}
