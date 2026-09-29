import { type ChangeEvent, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { getErrorCode } from '@vsite/shared';
import { mediaUrl } from '@vsite/builder-components';
import {
  getGetShopsShopIdQueryKey,
  useGetShopsShopIdMediaAssets,
  usePutShopsShopIdLogo,
  type ShopLogoDto,
} from '@vsite/api-sdk';
import { getErrorMessage, HEIC_UNSUPPORTED_GUIDANCE } from '../../lib/error-messages';
import { ACCEPT_ATTR, fileSchema } from '../../lib/media-validation';

/** Phái sinh dùng để hiện logo trên trang sửa shop (F4 brief — "hiện logo qua phái sinh 320x96,inside"). */
const LOGO_DISPLAY_PRESET = '320x96,inside';

export interface ShopLogoSectionProps {
  shopId: string;
  /** `ShopDto.logoId` — theo thiết kế brief F4, đây là id của chính bản phái sinh `320x96,inside`
   * (không phải id bản gốc trong thư viện): `GET .../media/assets?ids=` trả thẳng MediaAssetDto có
   * `storageKey` dùng được ngay, không cần bước tra "derivatives của asset gốc" (endpoint đó không
   * tồn tại trong 9 endpoint media — xem báo cáo). */
  logoId: string | null;
  isOwner: boolean;
}

/**
 * F4 — logo trong trang sửa shop. Nút tải logo chỉ hiện khi `isOwner` (role Owner, xem
 * `lib/shop-role.ts`). Ba trạng thái đủ: loading (đang tra logoId → asset), error (tra lỗi),
 * empty (shop chưa có logo).
 */
export function ShopLogoSection({ shopId, logoId, isOwner }: ShopLogoSectionProps) {
  const queryClient = useQueryClient();
  const [clientError, setClientError] = useState<string | null>(null);
  // Hiện ngay ảnh vừa upload từ response, không đợi round-trip GET assets?ids= lần nữa (#71-style:
  // response mutation là nguồn dữ liệu tức thời; invalidate query bên dưới để lần tải trang sau
  // logoId mới khớp cache).
  const [justUploadedStorageKey, setJustUploadedStorageKey] = useState<string | null>(null);

  const assetsQuery = useGetShopsShopIdMediaAssets(
    shopId,
    { ids: logoId ? [logoId] : [] },
    { query: { enabled: !!logoId } },
  );

  const uploadMutation = usePutShopsShopIdLogo({
    mutation: {
      onSuccess: (result: ShopLogoDto) => {
        const derivative = result.derivatives.find((d) => d.preset === LOGO_DISPLAY_PRESET);
        if (!derivative) {
          // Không nên xảy ra (BE luôn sinh đủ phái sinh cho preset của Shop, config/image-presets.json)
          // — vẫn hiện được bằng bản gốc thay vì chặn UI hoàn toàn, giống quy ước cảnh báo của resolveImage.
          console.warn(`[ShopLogoSection] response logo upload thiếu phái sinh "${LOGO_DISPLAY_PRESET}"`);
        }
        setJustUploadedStorageKey(derivative?.storageKey ?? result.libraryAsset.storageKey);
        setClientError(null);
        void queryClient.invalidateQueries({ queryKey: getGetShopsShopIdQueryKey(shopId) });
      },
    },
  });

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0] ?? null;
    event.target.value = '';
    setClientError(null);
    if (!file) return;
    const result = fileSchema.safeParse(file);
    if (!result.success) {
      setClientError(result.error.issues[0]?.message ?? 'Ảnh không hợp lệ');
      return;
    }
    uploadMutation.mutate({ shopId, data: { file } });
  }

  const errorCode = getErrorCode(uploadMutation.error);
  const isHeicError = errorCode === 'MEDIA_HEIC_UNSUPPORTED';
  const displayStorageKey = justUploadedStorageKey ?? assetsQuery.data?.[0]?.storageKey ?? null;
  const isLoadingExisting = !justUploadedStorageKey && !!logoId && assetsQuery.isPending;
  const isErrorExisting = !justUploadedStorageKey && !!logoId && assetsQuery.isError;
  const isEmpty = !justUploadedStorageKey && !logoId;

  return (
    <section className="mt-8 border-t border-border pt-6">
      <h2 className="text-sm font-semibold text-foreground">Logo</h2>

      {isLoadingExisting && <p className="mt-2 text-sm text-muted-foreground">Đang tải logo...</p>}

      {isErrorExisting && (
        <p className="mt-2 text-sm text-destructive" role="alert">
          {getErrorMessage(getErrorCode(assetsQuery.error))}
        </p>
      )}

      {!justUploadedStorageKey && !!logoId && assetsQuery.isSuccess && !displayStorageKey && (
        <p className="mt-2 text-sm text-destructive" role="alert">
          Không tìm thấy ảnh logo.
        </p>
      )}

      {isEmpty && <p className="mt-2 text-sm text-muted-foreground">Shop chưa có logo.</p>}

      {displayStorageKey && (
        <img
          src={mediaUrl(displayStorageKey)}
          alt="Logo shop"
          className="mt-2 max-h-24 max-w-[320px] rounded border border-border object-contain"
        />
      )}

      {isOwner && (
        <div className="mt-3">
          <label className="cursor-pointer">
            <span className="inline-block rounded-md border border-border px-3 py-2 text-sm hover:bg-muted">
              {uploadMutation.isPending ? 'Đang tải lên...' : 'Tải logo mới'}
            </span>
            <input
              type="file"
              accept={ACCEPT_ATTR}
              className="hidden"
              onChange={handleFileChange}
              disabled={uploadMutation.isPending}
            />
          </label>

          {clientError && (
            <p className="mt-1 text-sm text-destructive" role="alert">
              {clientError}
            </p>
          )}

          {uploadMutation.isError && (
            <div role="alert" className="mt-2 rounded border border-destructive/50 bg-destructive/10 p-3 text-sm text-destructive">
              <p>{getErrorMessage(errorCode)}</p>
              {isHeicError && <p className="mt-1">{HEIC_UNSUPPORTED_GUIDANCE}</p>}
            </div>
          )}
        </div>
      )}
    </section>
  );
}
