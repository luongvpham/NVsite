import { useMemo, useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { Button, Dialog } from '@vsite/ui';
import { getErrorCode } from '@vsite/shared';
import { mediaUrl } from '@vsite/builder-components';
import {
  getListPagesQueryKey,
  useCreatePage,
  useGetAssetsByIds,
  useUpdatePage,
  type MediaAssetDto,
  type PageDto,
} from '@vsite/api-sdk';
import { getErrorMessage } from '../../lib/error-messages';
import {
  buildPageFormSchema,
  toCreatePageRequest,
  toUpdatePageRequest,
  type PageFormLocks,
  type PageFormValues,
} from '../../lib/page-validation';
import { FormField } from '../form-field';
import { MediaLibraryPicker } from '../media/media-library-picker';

export interface PageFormDialogProps {
  shopId: string;
  /** Có `page` = sửa; không có = tạo mới. */
  page?: PageDto;
  open: boolean;
  onClose: () => void;
}

/** Luật khoá theo loại trang (brief WEBSITE-002 "System page"): trang chủ + System không đổi slug; NotFound bắt buộc noindex. */
function locksOf(page: PageDto | undefined): Required<PageFormLocks> {
  return {
    slugLocked: page ? page.isHomePage || page.kind === 'System' : false,
    noIndexLocked: page?.systemType === 'NotFound',
  };
}

/**
 * Tạo/sửa metadata một trang (Flow 2–3). Lỗi BE gắn đúng ô theo `error_code`; mã không có ô để gắn
 * (vd. `PAGE_SYSTEM_SLUG_FIXED`) hiện ở alert chung. Chỉ dùng trong `OwnerOnlyGate` nên picker luôn là Owner.
 */
export function PageFormDialog({ shopId, page, open, onClose }: PageFormDialogProps) {
  const queryClient = useQueryClient();
  const { slugLocked, noIndexLocked } = locksOf(page);
  const locks = { slugLocked, noIndexLocked };
  const schema = useMemo(() => buildPageFormSchema({ slugLocked, noIndexLocked }), [slugLocked, noIndexLocked]);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [pickedAsset, setPickedAsset] = useState<MediaAssetDto | null>(null);
  const [conflict, setConflict] = useState(false);

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    setError,
    formState: { errors },
  } = useForm<PageFormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      slug: page?.slug ?? '',
      title: page?.title ?? '',
      seoTitle: page?.seoTitle ?? '',
      seoDescription: page?.seoDescription ?? '',
      ogImageId: page?.ogImageId ?? null,
      noIndex: locks.noIndexLocked ? true : (page?.noIndex ?? false),
      isEnabled: page?.isEnabled ?? true,
    },
  });

  const ogImageId = watch('ogImageId');
  const ogQuery = useGetAssetsByIds(shopId, { ids: ogImageId ? [ogImageId] : [] }, { query: { enabled: Boolean(ogImageId) && pickedAsset?.id !== ogImageId } });
  const ogAsset = pickedAsset?.id === ogImageId ? pickedAsset : ogQuery.data?.find((asset) => asset.id === ogImageId);

  function refreshList() {
    return queryClient.invalidateQueries({ queryKey: getListPagesQueryKey(shopId) });
  }

  function handleError(error: unknown) {
    const code = getErrorCode(error);
    const message = getErrorMessage(code);
    switch (code) {
      case 'PAGE_SLUG_TAKEN':
      case 'PAGE_SLUG_RESERVED':
      case 'PAGE_SLUG_INVALID':
        // Trang chủ/System không có ô slug mở → rơi về alert chung.
        setError(locks.slugLocked ? 'root' : 'slug', { message });
        return;
      case 'PAGE_NOINDEX_REQUIRED':
        setError('noIndex', { message });
        return;
      case 'MEDIA_INVALID_IMAGE_REFERENCE':
        setError('ogImageId', { message });
        return;
      case 'CONCURRENCY_CONFLICT':
        setConflict(true);
        setError('root', { message });
        return;
      default:
        setError('root', { message });
    }
  }

  const createMutation = useCreatePage({
    mutation: {
      onSuccess: () => {
        void refreshList();
        onClose();
      },
      onError: handleError,
    },
  });
  const updateMutation = useUpdatePage({
    mutation: {
      onSuccess: () => {
        void refreshList();
        onClose();
      },
      onError: handleError,
    },
  });
  const isPending = createMutation.isPending || updateMutation.isPending;

  const submit = handleSubmit((values) => {
    if (page) {
      updateMutation.mutate({
        shopId,
        pageId: page.id,
        data: toUpdatePageRequest({ ...values, noIndex: locks.noIndexLocked || values.noIndex }, locks),
      });
    } else {
      createMutation.mutate({ shopId, data: toCreatePageRequest(values) });
    }
  });

  return (
    // Picker đang mở thì Escape chỉ đóng picker (hai Dialog cùng nghe `keydown` ở document).
    <Dialog open={open} onClose={pickerOpen ? noop : onClose} title={page ? 'Sửa trang' : 'Tạo trang mới'}>
      <form
        className="space-y-4"
        noValidate
        onSubmit={(event) => {
          void submit(event);
        }}
      >
        <FormField id="page-title" label="Tên trang" type="text" required error={errors.title?.message} {...register('title')} />
        <FormField
          id="page-slug"
          label="Đường dẫn"
          type="text"
          required={!locks.slugLocked}
          disabled={locks.slugLocked}
          error={errors.slug?.message}
          {...register('slug')}
        />
        {locks.slugLocked && (
          <p className="-mt-2 text-xs text-muted-foreground">Trang chủ và trang hệ thống không đổi được đường dẫn.</p>
        )}
        <FormField id="page-seo-title" label="Tiêu đề SEO" type="text" error={errors.seoTitle?.message} {...register('seoTitle')} />
        <div>
          <label className="block text-sm font-medium" htmlFor="page-seo-description">
            Mô tả SEO
          </label>
          <textarea
            id="page-seo-description"
            rows={3}
            className="mt-1 w-full rounded-md border border-border px-3 py-2"
            aria-invalid={Boolean(errors.seoDescription)}
            {...register('seoDescription')}
          />
          {errors.seoDescription && (
            <p className="mt-1 text-sm text-destructive" role="alert">
              {errors.seoDescription.message}
            </p>
          )}
        </div>

        <div className="space-y-2">
          <p className="text-sm font-medium">Ảnh chia sẻ (Open Graph)</p>
          {ogImageId && ogAsset && (
            <img src={mediaUrl(ogAsset.storageKey)} alt="Ảnh chia sẻ" className="max-h-28 rounded border border-border object-contain" />
          )}
          {ogImageId && !ogAsset && (
            <p className="text-xs text-muted-foreground">
              {ogQuery.isPending ? 'Đang tải ảnh...' : 'Không hiển thị được ảnh đã chọn.'}
            </p>
          )}
          {!ogImageId && <p className="text-xs text-muted-foreground">Chưa chọn ảnh.</p>}
          <div className="flex gap-2">
            <Button type="button" variant="outline" size="sm" onClick={() => { setPickerOpen(true); }}>
              Chọn ảnh chia sẻ
            </Button>
            {ogImageId && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => {
                  setPickedAsset(null);
                  setValue('ogImageId', null);
                }}
              >
                Bỏ ảnh
              </Button>
            )}
          </div>
          {errors.ogImageId && (
            <p className="text-sm text-destructive" role="alert">
              {errors.ogImageId.message}
            </p>
          )}
        </div>

        <div className="space-y-2">
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" disabled={locks.noIndexLocked} {...register('noIndex')} />
            Ẩn khỏi công cụ tìm kiếm
          </label>
          {errors.noIndex && (
            <p className="text-sm text-destructive" role="alert">
              {errors.noIndex.message}
            </p>
          )}
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" {...register('isEnabled')} />
            Hiển thị trang
          </label>
        </div>

        {errors.root && (
          <div className="space-y-2">
            <p className="text-sm text-destructive" role="alert">
              {errors.root.message}
            </p>
            {conflict && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => {
                  void refreshList();
                  onClose();
                }}
              >
                Tải lại
              </Button>
            )}
          </div>
        )}

        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={onClose}>
            Huỷ
          </Button>
          <Button type="submit" disabled={isPending}>
            {isPending ? 'Đang lưu...' : page ? 'Lưu' : 'Tạo trang'}
          </Button>
        </div>
      </form>

      {pickerOpen && (
        <MediaLibraryPicker
          shopId={shopId}
          mode="library"
          isOwner
          open
          onClose={() => { setPickerOpen(false); }}
          onSelect={(asset) => {
            setPickedAsset(asset);
            setValue('ogImageId', asset.id, { shouldDirty: true });
            setPickerOpen(false);
          }}
        />
      )}
    </Dialog>
  );
}

function noop() {
  /* picker đang mở — bỏ qua yêu cầu đóng form */
}
