import { type MouseEvent, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Button, Dialog } from '@vsite/ui';
import { getErrorCode } from '@vsite/shared';
import { mediaUrl } from '@vsite/builder-components';
import {
  getGetAssetReferencesQueryOptions,
  getListLibraryQueryKey,
  getGetMediaUsageQueryKey,
  useDeleteFromLibrary,
  useListLibrary,
  useGetMediaUsage,
  useUploadToLibrary,
  useCloneFromLibrary,
  type MediaAssetDto,
} from '@vsite/api-sdk';
import { getErrorMessage, HEIC_UNSUPPORTED_GUIDANCE } from '../../lib/error-messages';
import { formatBytes } from '../../lib/format-bytes';
import { ACCEPT_ATTR, fileSchema, fitOfPreset, isHeicFile } from '../../lib/media-validation';

const PAGE_SIZE = 24;

export interface MediaLibraryPickerProps {
  shopId: string;
  /** Preset của slot đang chọn ảnh cho — dùng để gọi clone (#71: luôn clone, không đặt id Library vào tree). */
  preset: string;
  /** Fail-safe: mặc định `false` — ẩn nút xoá cho tới khi caller (F5) xác nhận role Owner thật,
   * chỉ bật khi biết chắc là Owner, không suy đoán. */
  isOwner?: boolean;
  open: boolean;
  onClose: () => void;
  onSelect: (asset: MediaAssetDto) => void;
}

/**
 * F3 — picker Media Library: danh sách phân trang, upload thẳng vào thư viện, chọn ảnh → clone vào
 * slot, xoá (cảnh báo tham chiếu nhưng không chặn), hiện dung lượng đã dùng.
 */
export function MediaLibraryPicker({ shopId, preset, isOwner = false, open, onClose, onSelect }: MediaLibraryPickerProps) {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [uploadHeic, setUploadHeic] = useState(false);
  const [selectedForClone, setSelectedForClone] = useState<MediaAssetDto | null>(null);
  const [cloneFocal, setCloneFocal] = useState({ x: 0.5, y: 0.5 });
  const [pendingDelete, setPendingDelete] = useState<{ asset: MediaAssetDto; referenceCount: number } | null>(null);
  const [referencesLoading, setReferencesLoading] = useState(false);

  const libraryQuery = useListLibrary(shopId, { page, pageSize: PAGE_SIZE });
  const usageQuery = useGetMediaUsage(shopId);

  const showFocalPicker = fitOfPreset(preset) !== 'inside';

  const uploadMutation = useUploadToLibrary({
    mutation: {
      onSuccess: () => {
        setUploadError(null);
        void queryClient.invalidateQueries({ queryKey: getListLibraryQueryKey(shopId) });
        void queryClient.invalidateQueries({ queryKey: getGetMediaUsageQueryKey(shopId) });
      },
      onError: (error) => {
        setUploadError(getErrorMessage(getErrorCode(error)));
      },
    },
  });

  const cloneMutation = useCloneFromLibrary({
    mutation: {
      onSuccess: (clonedAsset) => {
        onSelect(clonedAsset);
        setSelectedForClone(null);
      },
    },
  });

  const deleteMutation = useDeleteFromLibrary({
    mutation: {
      onSuccess: () => {
        setPendingDelete(null);
        void queryClient.invalidateQueries({ queryKey: getListLibraryQueryKey(shopId) });
        void queryClient.invalidateQueries({ queryKey: getGetMediaUsageQueryKey(shopId) });
      },
    },
  });

  function handleUploadChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    const result = fileSchema.safeParse(file);
    if (!result.success) {
      setUploadError(result.error.issues[0]?.message ?? 'Ảnh không hợp lệ');
      setUploadHeic(isHeicFile(file));
      return;
    }
    setUploadError(null);
    setUploadHeic(false);
    uploadMutation.mutate({ shopId, data: { file } });
  }

  function openCloneStep(asset: MediaAssetDto) {
    setSelectedForClone(asset);
    setCloneFocal({ x: asset.focalPointX, y: asset.focalPointY });
  }

  function handleFocalClick(event: MouseEvent<HTMLDivElement>) {
    if (!showFocalPicker) return;
    const rect = event.currentTarget.getBoundingClientRect();
    const x = Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width));
    const y = Math.min(1, Math.max(0, (event.clientY - rect.top) / rect.height));
    setCloneFocal({ x, y });
  }

  function confirmClone() {
    if (!selectedForClone) return;
    cloneMutation.mutate({
      shopId,
      assetId: selectedForClone.id,
      data: {
        preset,
        focalX: showFocalPicker ? cloneFocal.x : null,
        focalY: showFocalPicker ? cloneFocal.y : null,
      },
    });
  }

  async function handleDeleteClick(asset: MediaAssetDto) {
    setReferencesLoading(true);
    try {
      const referencesResult = await queryClient.query(
        getGetAssetReferencesQueryOptions(shopId, asset.id),
      );
      setPendingDelete({ asset, referenceCount: referencesResult.references.length });
    } finally {
      setReferencesLoading(false);
    }
  }

  const items = libraryQuery.data?.items ?? [];
  const total = libraryQuery.data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <Dialog open={open} onClose={onClose} title="Thư viện ảnh" className="max-w-3xl">
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <label className="cursor-pointer">
            <span className="inline-block rounded-md border border-border px-3 py-2 text-sm hover:bg-muted">
              Tải ảnh lên thư viện
            </span>
            <input type="file" accept={ACCEPT_ATTR} className="hidden" onChange={handleUploadChange} />
          </label>

          {usageQuery.isPending && <span className="text-xs text-muted-foreground">Đang tính dung lượng...</span>}
          {usageQuery.isError && (
            <span className="text-xs text-destructive">{getErrorMessage(getErrorCode(usageQuery.error))}</span>
          )}
          {usageQuery.isSuccess && (
            <span className="text-xs text-muted-foreground">Đã dùng: {formatBytes(usageQuery.data.usedBytes)}</span>
          )}
        </div>

        {uploadMutation.isPending && <p className="text-sm text-muted-foreground">Đang tải ảnh lên...</p>}
        {uploadError && (
          <div role="alert" className="text-sm text-destructive">
            <p>{uploadError}</p>
            {uploadHeic && <p className="mt-1">{HEIC_UNSUPPORTED_GUIDANCE}</p>}
          </div>
        )}

        {libraryQuery.isPending && <p className="text-muted-foreground">Đang tải thư viện...</p>}

        {libraryQuery.isError && (
          <p role="alert" className="text-destructive">
            {getErrorMessage(getErrorCode(libraryQuery.error))}
          </p>
        )}

        {libraryQuery.isSuccess && items.length === 0 && (
          <p className="text-muted-foreground">Thư viện chưa có ảnh nào. Tải ảnh lên để bắt đầu.</p>
        )}

        {libraryQuery.isSuccess && items.length > 0 && (
          <>
            <div className="grid grid-cols-4 gap-3">
              {items.map((asset) => (
                <div key={asset.id} className="space-y-1">
                  <button
                    type="button"
                    className="block w-full overflow-hidden rounded border border-border"
                    onClick={() => { openCloneStep(asset); }}
                  >
                    <img
                      src={mediaUrl(asset.storageKey)}
                      alt={asset.altText ?? 'Ảnh thư viện'}
                      loading="lazy"
                      className="aspect-square w-full object-cover"
                    />
                  </button>
                  {isOwner && (
                    <button
                      type="button"
                      className="w-full text-xs text-destructive hover:underline"
                      disabled={referencesLoading}
                      onClick={() => {
                        void handleDeleteClick(asset);
                      }}
                    >
                      Xoá
                    </button>
                  )}
                </div>
              ))}
            </div>

            <div className="flex items-center justify-between text-sm">
              <Button type="button" variant="outline" size="sm" disabled={page <= 1} onClick={() => { setPage((p) => p - 1); }}>
                Trước
              </Button>
              <span>
                Trang {page}/{totalPages}
              </span>
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={page >= totalPages}
                onClick={() => { setPage((p) => p + 1); }}
              >
                Sau
              </Button>
            </div>
          </>
        )}

        {selectedForClone && (
          <div className="space-y-2 rounded border border-border p-3">
            <p className="text-sm font-medium">Xác nhận chọn ảnh</p>
            <div className="relative cursor-crosshair overflow-hidden rounded border border-border" onClick={handleFocalClick}>
              <img
                src={mediaUrl(selectedForClone.storageKey)}
                alt="Xem trước"
                className="block max-h-56 w-full object-contain"
              />
              {showFocalPicker && (
                <div
                  className="pointer-events-none absolute h-3 w-3 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-white bg-primary"
                  style={{ left: `${cloneFocal.x * 100}%`, top: `${cloneFocal.y * 100}%` }}
                />
              )}
            </div>
            {cloneMutation.isError && (
              <p role="alert" className="text-sm text-destructive">
                {getErrorMessage(getErrorCode(cloneMutation.error))}
              </p>
            )}
            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" onClick={() => { setSelectedForClone(null); }}>
                Huỷ
              </Button>
              <Button type="button" onClick={confirmClone} disabled={cloneMutation.isPending}>
                {cloneMutation.isPending ? 'Đang chọn...' : 'Dùng ảnh này'}
              </Button>
            </div>
          </div>
        )}

        {pendingDelete && (
          <div role="alertdialog" className="space-y-2 rounded border border-destructive/50 bg-destructive/10 p-3">
            <p className="text-sm">
              {pendingDelete.referenceCount > 0
                ? `Ảnh này đang được dùng ở ${pendingDelete.referenceCount} nơi (vd. logo shop). Ảnh sẽ ẩn khỏi thư viện; các nơi đang dùng vẫn hiển thị bình thường.`
                : 'Xoá ảnh này khỏi thư viện?'}
            </p>
            {deleteMutation.isError && (
              <p role="alert" className="text-sm text-destructive">
                {getErrorMessage(getErrorCode(deleteMutation.error))}
              </p>
            )}
            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" onClick={() => { setPendingDelete(null); }}>
                Huỷ
              </Button>
              <Button
                type="button"
                onClick={() => { deleteMutation.mutate({ shopId, assetId: pendingDelete.asset.id }); }}
                disabled={deleteMutation.isPending}
              >
                {deleteMutation.isPending ? 'Đang xoá...' : 'Xoá'}
              </Button>
            </div>
          </div>
        )}
      </div>
    </Dialog>
  );
}
