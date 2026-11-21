import { useCallback, useEffect, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import {
  getGetNavigationQueryKey,
  getGetWebsiteQueryKey,
  getListPagesQueryKey,
  getListPublicationsQueryKey,
  usePublishWebsite,
  type PublicationDto,
} from '@vsite/api-sdk';
import { getErrorCode, getProblemParams, isPublishBlockedParams, type PublishBlockedParams } from '@vsite/shared';
import { getErrorMessage } from '../lib/error-messages';

/** Lỗi publish đã phân loại: `blocked` (422 kèm danh sách lỗi), `conflict` (409), `other` (còn lại). */
export type PublishFailure =
  | { kind: 'blocked'; message: string; params: PublishBlockedParams }
  | { kind: 'conflict'; message: string }
  | { kind: 'other'; message: string };

export function classifyPublishError(error: unknown): PublishFailure {
  const code = getErrorCode(error);
  const message = getErrorMessage(code);
  if (code === 'PUBLISH_BLOCKED') {
    const params = getProblemParams(error, isPublishBlockedParams);
    if (params) return { kind: 'blocked', message, params };
  }
  if (code === 'PUBLISH_CONFLICT' || code === 'ROLLBACK_CONFLICT') return { kind: 'conflict', message };
  return { kind: 'other', message };
}

/**
 * Bọc `usePublishWebsite`. Không tự thử lại (409 phải hiện cho người dùng). Settle ⇒ làm mới
 * website + danh sách trang + lịch sử bản. Dữ liệu sống ở TanStack Query (#20).
 */
export function usePublishWebsiteFlow(shopId: string, options: { onSettled?: () => void } = {}) {
  const queryClient = useQueryClient();
  // Callback gắn ở tầng hook (không per-call) nên vẫn chạy khi component unmount hoặc sau reset().
  const onSettledRef = useRef(options.onSettled);
  useEffect(() => {
    onSettledRef.current = options.onSettled;
  });
  const mutation = usePublishWebsite({
    mutation: {
      retry: false,
      onSettled: () => {
        void queryClient.invalidateQueries({ queryKey: getGetWebsiteQueryKey(shopId) });
        void queryClient.invalidateQueries({ queryKey: getListPagesQueryKey(shopId) });
        void queryClient.invalidateQueries({ queryKey: getListPublicationsQueryKey(shopId) });
        // Publish lần đầu tạo hàng menu ⇒ tab Menu đang mở với `version: null` sẽ 409 nếu không nạp lại.
        void queryClient.invalidateQueries({ queryKey: getGetNavigationQueryKey(shopId) });
        onSettledRef.current?.();
      },
    },
  });
  const { mutate, reset: resetMutation } = mutation;

  /** `note` đã trim; rỗng ⇒ `null`. `options.onSettled` gọi một lần mỗi lần thử xong (thành công hoặc lỗi). */
  const publish = useCallback(
    (note: string) => {
      const trimmed = note.trim();
      mutate({ shopId, data: { note: trimmed === '' ? null : trimmed } });
    },
    [mutate, shopId],
  );

  const result: PublicationDto | null = mutation.data ?? null;
  const failure: PublishFailure | null = mutation.isError ? classifyPublishError(mutation.error) : null;

  return { publish, isPending: mutation.isPending, result, failure, reset: resetMutation };
}
