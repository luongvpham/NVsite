import { useState } from 'react';
import { CropFrame } from './crop-frame';
import { useQueryClient } from '@tanstack/react-query';
import { Button, Dialog } from '@vsite/ui';
import { getErrorCode } from '@vsite/shared';
import { mediaUrl } from '@vsite/builder-components';
import {
  getGetAssetReferencesQueryOptions,
  getListLibraryQueryKey,
  getGetMediaUsageQueryKey,
  MediaReferenceKind,
  useDeleteFromLibrary,
  useListLibrary,
  useGetMediaUsage,
  useUploadToLibrary,
  useCloneFromLibrary,
  type MediaAssetDto,
  type MediaReferenceDto,
} from '@vsite/api-sdk';
import { getErrorMessage, HEIC_UNSUPPORTED_GUIDANCE } from '../../lib/error-messages';
import { formatBytes } from '../../lib/format-bytes';
import { ACCEPT_ATTR, fileSchema, fitOfPreset, isHeicFile } from '../../lib/media-validation';

const PAGE_SIZE = 24;

/** Một dòng "ảnh đang dùng ở đâu" cho cảnh báo xoá. `pageTitle` có thể thiếu (trang đã xoá mềm) → không in `null`. */
function describeReference(reference: MediaReferenceDto): string {
  switch (reference.kind) {
    case MediaReferenceKind.ShopLogo:
      return 'Logo shop';
    case MediaReferenceKind.PageDraft:
      return reference.pageTitle ? `Trang ${reference.pageTitle}` : 'Một trang';
  }
}

interface MediaLibraryPickerBaseProps {
  shopId: string;
  /** Fail-safe: mặc định `false` — ẩn nút xoá cho tới khi caller (F5) xác nhận role Owner thật,
   * chỉ bật khi biết chắc là Owner, không suy đoán. */
  isOwner?: boolean;
  open: boolean;
  onClose: () => void;
  onSelect: (asset: MediaAssetDto) => void;
}

/**
 * - `clone` (mặc định): chọn ảnh cho một slot trong cây → luôn clone ra bản riêng theo `preset` (#71: không
 *   đặt id Library vào tree).
 * - `library`: trả thẳng bản **Library**, không clone, không cần preset — dành cho `ogImageId` của
 *   Page/Website (brief WEBSITE-002 "Media": OG là bản Library, ảnh trong cây mới là Clone/Direct).
 */
export type MediaLibraryPickerProps = MediaLibraryPickerBaseProps &
  ({ mode?: 'clone'; preset: string } | { mode: 'library'; preset?: undefined });

/**
 * F3 — picker Media Library: danh sách phân trang, upload thẳng vào thư viện, chọn ảnh → clone vào
 * slot, xoá (cảnh báo tham chiếu nhưng không chặn), hiện dung lượng đã dùng.
 */
export function MediaLibraryPicker({ shopId, mode = 'clone', preset = '', isOwner = false, open, onClose, onSelect }: MediaLibraryPickerProps) {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [uploadHeic, setUploadHeic] = useState(false);
  const [selectedForClone, setSelectedForClone] = useState<MediaAssetDto | null>(null);
  const [cloneFocal, setCloneFocal] = useState({ x: 0.5, y: 0.5 });
  const [pendingDelete, setPendingDelete] = useState<{ asset: MediaAssetDto; references: MediaReferenceDto[] } | null>(null);
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

  function handlePick(asset: MediaAssetDto) {
    if (mode === 'library') {
      onSelect(asset);
      return;
    }
    openCloneStep(asset);
  }

  function openCloneStep(asset: MediaAssetDto) {
    setSelectedForClone(asset);
    setCloneFocal({ x: asset.focalPointX, y: asset.focalPointY });
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
      setPendingDelete({ asset, references: referencesResult.references });
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
          {/* #100: mọi route media chỉ Owner — non-Owner bấm sẽ ăn 403, nên ẩn hẳn (brief WEBSITE-002). */}
          {isOwner ? (
            <label className="cursor-pointer">
              <span className="inline-block rounded-md border border-border px-3 py-2 text-sm hover:bg-muted">
                Tải ảnh lên thư viện
              </span>
              <input type="file" accept={ACCEPT_ATTR} className="hidden" onChange={handleUploadChange} />
            </label>
          ) : (
            <span />
          )}

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
                    onClick={() => { handlePick(asset); }}
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
            <CropFrame
              src={mediaUrl(selectedForClone.storageKey)}
              alt="Xem trước"
              preset={preset}
              focal={cloneFocal}
              onFocalChange={setCloneFocal}
              imageClassName="block max-h-56 w-auto max-w-full"
            />
            {showFocalPicker && (
              <p className="text-xs text-muted-foreground">Kéo khung để chọn vùng giữ lại.</p>
            )}
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
              {pendingDelete.references.length > 0
                ? `Ảnh này đang được dùng ở ${String(pendingDelete.references.length)} nơi. Ảnh sẽ ẩn khỏi thư viện; các nơi đang dùng vẫn hiển thị bình thường.`
                : 'Xoá ảnh này khỏi thư viện?'}
            </p>
            {pendingDelete.references.length > 0 && (
              <ul className="list-disc pl-5 text-sm">
                {pendingDelete.references.map((reference, index) => (
                  <li key={`${reference.kind}-${reference.pageId ?? index}`}>{describeReference(reference)}</li>
                ))}
              </ul>
            )}
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
