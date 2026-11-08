import { useCallback, useEffect, useMemo, useState } from 'react';
import type { MediaRef } from '@vsite/builder-components';
import { useGetAssetsByIds, type MediaAssetDto } from '@vsite/api-sdk';
import { collectImageIds, type NodeLike } from '../lib/collect-image-ids';

/**
 * `mediaMap` cho canvas: cây chỉ giữ `imageId`, map tra ra `storageKey` thật (#71, #74).
 *
 * - Ảnh vừa chọn/upload trong phiên điền thẳng từ response (`addAsset`) — không round-trip.
 * - `imageId` có trong cây mà chưa có trong map (mở trang lần đầu, undo về ảnh cũ) tra bằng
 *   `GET /shops/{shopId}/media/assets?ids=`. Id tra hụt (ảnh đã xoá) vẫn đánh dấu "đã hỏi" để không lặp.
 */
export function useMediaMap(shopId: string, tree: NodeLike | null) {
  const [mediaMap, setMediaMap] = useState<Record<string, MediaRef>>({});
  const [requestedIds, setRequestedIds] = useState<ReadonlySet<string>>(new Set());

  const missingIds = useMemo(() => {
    if (!tree) return [];
    return Array.from(collectImageIds(tree)).filter((id) => !(id in mediaMap) && !requestedIds.has(id));
  }, [tree, mediaMap, requestedIds]);

  const assetsQuery = useGetAssetsByIds(shopId, { ids: missingIds }, { query: { enabled: missingIds.length > 0 } });

  useEffect(() => {
    if (!assetsQuery.isSuccess) return;
    const fetchedIds = missingIds;
    setMediaMap((prev) => {
      const next = { ...prev };
      for (const asset of assetsQuery.data) {
        next[asset.id] = { storageKey: asset.storageKey, preset: asset.preset };
      }
      return next;
    });
    setRequestedIds((prev) => new Set([...prev, ...fetchedIds]));
    // missingIds đổi cùng queryKey với assetsQuery.data nên chỉ cần theo dõi data.
  }, [assetsQuery.isSuccess, assetsQuery.data]);

  const addAsset = useCallback((asset: MediaAssetDto) => {
    setMediaMap((prev) => ({ ...prev, [asset.id]: { storageKey: asset.storageKey, preset: asset.preset } }));
  }, []);

  return { mediaMap, addAsset };
}
