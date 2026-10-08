import { type ChangeEvent, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { getErrorCode } from '@vsite/shared';
import { getGetShopsQueryKey, useGetShopsShopId, usePutShopsShopIdLogo } from '@vsite/api-sdk';
import { getErrorMessage, HEIC_UNSUPPORTED_GUIDANCE } from '../../lib/error-messages';
import { ACCEPT_ATTR, fileSchema } from '../../lib/media-validation';

export interface ShopLogoSectionProps {
  shopId: string;
  isOwner: boolean;
}

/**
 * F4/F6 — logo trong trang sửa shop. Nguồn duy nhất của ảnh là `ShopDto.logoUrl` (BE trả sẵn URL
 * đã có tiền tố `/media/`, #88) — dùng nguyên trạng, KHÔNG bọc `mediaUrl()`. Sau khi upload thành
 * công, refetch shop query để đọc `logoUrl` mới từ server (server state là nguồn duy nhất).
 * Nút tải logo chỉ hiện khi `isOwner`.
 */
export function ShopLogoSection({ shopId, isOwner }: ShopLogoSectionProps) {
  const queryClient = useQueryClient();
  const [clientError, setClientError] = useState<string | null>(null);
  const shopQuery = useGetShopsShopId(shopId);

  const uploadMutation = usePutShopsShopIdLogo({
    mutation: {
      onSuccess: async () => {
        setClientError(null);
        // Giữ mutation ở trạng thái pending tới khi logoUrl mới về, để UI không nhấp nháy ảnh cũ.
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: shopQuery.queryKey }),
          queryClient.invalidateQueries({ queryKey: getGetShopsQueryKey() }),
        ]);
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
  const logoUrl = shopQuery.data?.logoUrl ?? null;

  return (
    <section className="mt-8 border-t border-border pt-6">
      <h2 className="text-sm font-semibold text-foreground">Logo</h2>

      {shopQuery.isPending && <p className="mt-2 text-sm text-muted-foreground">Đang tải logo...</p>}

      {shopQuery.isError && !shopQuery.data && (
        <p className="mt-2 text-sm text-destructive" role="alert">
          {getErrorMessage(getErrorCode(shopQuery.error))}
        </p>
      )}

      {shopQuery.data && !logoUrl && <p className="mt-2 text-sm text-muted-foreground">Chưa có logo</p>}

      {logoUrl && (
        <img
          src={logoUrl}
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
