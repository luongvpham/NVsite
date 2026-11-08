import { useState } from 'react';
import type { ImagePickerValue, MediaRef } from '@vsite/builder-components';
import { mediaUrl } from '@vsite/builder-components';
import { useListShops, type MediaAssetDto } from '@vsite/api-sdk';
import { isShopOwner } from '../../lib/shop-role';
import { MediaLibraryPicker } from './media-library-picker';
import { UploadSlotDialog } from './upload-slot-dialog';

/**
 * Control ảnh thật cho Inspector — cắm vào `Inspector.renderMediaPicker`. Hai chế độ theo
 * acceptance "08 §9": upload trực tiếp (UploadSlotDialog) hoặc chọn từ thư viện + clone
 * (MediaLibraryPicker, luôn tạo bản clone mới — #71). Cả hai trả về `MediaAssetDto` dùng ngay
 * để điền `mediaMap` (không cần round-trip `assets?ids=` cho ảnh vừa chọn trong phiên này).
 *
 * Chuyển từ `routes/dev-registry.tsx` (đã xoá, WEBSITE-002 F6).
 */
export function ImagePickerControl({
  shopId,
  preset,
  value,
  onChange,
  mediaMap,
  onAssetResolved,
}: {
  shopId: string;
  preset: string;
  value: ImagePickerValue;
  onChange: (value: ImagePickerValue) => void;
  mediaMap: Record<string, MediaRef>;
  onAssetResolved: (asset: MediaAssetDto) => void;
}) {
  const [mode, setMode] = useState<'closed' | 'upload' | 'library'>('closed');
  // Nút Xoá của picker chỉ hiện cho Owner: signal duy nhất là roleCode của `GET /shops` (cùng cách màn sửa shop).
  const shopsQuery = useListShops();
  const isOwner = isShopOwner(shopsQuery.data, shopId);
  const hasShopId = shopId.trim().length > 0;
  const currentRef = value.imageId ? mediaMap[value.imageId] : undefined;

  return (
    <div className="space-y-2">
      {value.imageId ? (
        currentRef ? (
          <img
            src={mediaUrl(currentRef.storageKey)}
            alt={value.alt ?? ''}
            className="max-h-32 rounded border border-border object-contain"
          />
        ) : (
          <p className="text-xs text-muted-foreground">imageId: {value.imageId} (chưa tra được storageKey)</p>
        )
      ) : (
        <p className="text-xs text-muted-foreground">Chưa chọn ảnh.</p>
      )}

      <div className="flex gap-2">
        <button
          type="button"
          className="rounded border border-border px-2 py-1 text-xs disabled:opacity-50"
          disabled={!hasShopId}
          onClick={() => {
            setMode('upload');
          }}
        >
          Tải ảnh lên
        </button>
        <button
          type="button"
          className="rounded border border-border px-2 py-1 text-xs disabled:opacity-50"
          disabled={!hasShopId}
          onClick={() => {
            setMode('library');
          }}
        >
          Chọn từ thư viện
        </button>
      </div>

      <input
        className="w-full rounded border border-border px-2 py-1 text-xs"
        placeholder="Alt text (tuỳ chọn)"
        value={value.alt ?? ''}
        onChange={(e) => {
          onChange({ ...value, alt: e.target.value });
        }}
      />

      {mode === 'upload' && (
        <UploadSlotDialog
          shopId={shopId}
          preset={preset}
          open
          onClose={() => {
            setMode('closed');
          }}
          onUploaded={(asset) => {
            onAssetResolved(asset);
            onChange({ ...value, imageId: asset.id });
            setMode('closed');
          }}
        />
      )}
      {mode === 'library' && (
        <MediaLibraryPicker
          shopId={shopId}
          preset={preset}
          isOwner={isOwner}
          open
          onClose={() => {
            setMode('closed');
          }}
          onSelect={(asset) => {
            onAssetResolved(asset);
            onChange({ ...value, imageId: asset.id });
            setMode('closed');
          }}
        />
      )}
    </div>
  );
}
