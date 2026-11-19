import { useIsMutating } from '@tanstack/react-query';
import { getPublishWebsiteMutationOptions, getRollbackPublicationMutationOptions } from '@vsite/api-sdk';

/**
 * Tín hiệu chéo publish ⇄ rollback (R6) — đọc từ trạng thái mutation của TanStack Query theo key sinh sẵn,
 * không copy sang store khác (#20). Dùng để khoá nút của bên kia trong lúc một bên đang chạy.
 */
export function useIsPublishing(): boolean {
  return useIsMutating({ mutationKey: getPublishWebsiteMutationOptions().mutationKey }) > 0;
}

export function useIsRollingBack(): boolean {
  return useIsMutating({ mutationKey: getRollbackPublicationMutationOptions().mutationKey }) > 0;
}
