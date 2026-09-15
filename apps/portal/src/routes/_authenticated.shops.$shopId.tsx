import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Button } from '@vsite/ui';
import { ShopKind, ShopStatus, useGetShopsShopId, usePatchShopsShopId, type ShopDto } from '@vsite/api-sdk';
import { getErrorCode } from '@vsite/shared';
import { FormField, FormSelect } from '../components/form-field';
import { getErrorMessage } from '../lib/error-messages';
import { updateShopFormSchema, type UpdateShopFormValues } from '../lib/shop-validation';

export const Route = createFileRoute('/_authenticated/shops/$shopId')({
  component: EditShopPage,
});

function EditShopPage() {
  const { shopId } = Route.useParams();
  const shopQuery = useGetShopsShopId(shopId);

  return (
    <main className="mx-auto max-w-sm p-6">
      <h1 className="text-xl font-semibold text-foreground">Sửa shop</h1>

      {shopQuery.isPending && <p className="mt-6 text-muted-foreground">Đang tải...</p>}

      {shopQuery.isError && (
        <p className="mt-6 text-destructive" role="alert">
          {getErrorMessage(getErrorCode(shopQuery.error))}
        </p>
      )}

      {shopQuery.isSuccess && <EditShopForm shopId={shopId} shop={shopQuery.data} />}
    </main>
  );
}

function EditShopForm({ shopId, shop }: { shopId: string; shop: ShopDto }) {
  const navigate = useNavigate();
  const {
    register,
    handleSubmit,
    watch,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<UpdateShopFormValues>({
    resolver: zodResolver(updateShopFormSchema),
    defaultValues: {
      name: shop.name,
      slug: shop.slug,
      kind: shop.kind,
      externalUrl: shop.externalUrl ?? '',
      status: shop.status,
    },
  });

  const kind = watch('kind');

  const updateShopMutation = usePatchShopsShopId({
    mutation: {
      onSuccess: () => {
        void navigate({ to: '/shops' });
      },
      onError: (error) => {
        setError('root', { message: getErrorMessage(getErrorCode(error)) });
      },
    },
  });

  // Full-replace, không phải partial patch — gửi đủ 5 field kể cả field không đổi
  // (Docs/tasks/SHOP-001/brief.md, đúng UpdateShopRequest trong contract).
  const submitForm = handleSubmit((values) => {
    updateShopMutation.mutate({
      shopId,
      data: { ...values, externalUrl: values.kind === ShopKind.ExternalOnly ? values.externalUrl : null },
    });
  });

  return (
    <form
      className="mt-6 space-y-4"
      onSubmit={(event) => {
        void submitForm(event);
      }}
      noValidate
    >
      <FormField id="name" label="Tên shop" type="text" required error={errors.name?.message} {...register('name')} />
      <FormField id="slug" label="Slug" type="text" required error={errors.slug?.message} {...register('slug')} />
      <FormSelect
        id="kind"
        label="Loại"
        error={errors.kind?.message}
        options={[
          { value: ShopKind.Hosted, label: 'Website trên vsite' },
          { value: ShopKind.ExternalOnly, label: 'Chỉ liên kết ngoài' },
        ]}
        {...register('kind')}
      />
      {kind === ShopKind.ExternalOnly && (
        <FormField
          id="externalUrl"
          label="URL bên ngoài"
          type="url"
          required
          error={errors.externalUrl?.message}
          {...register('externalUrl')}
        />
      )}
      <FormSelect
        id="status"
        label="Trạng thái"
        error={errors.status?.message}
        options={[
          { value: ShopStatus.Draft, label: 'Nháp' },
          { value: ShopStatus.Active, label: 'Đang hoạt động' },
          { value: ShopStatus.Suspended, label: 'Tạm ngưng' },
          { value: ShopStatus.Closed, label: 'Đã đóng' },
        ]}
        {...register('status')}
      />
      {errors.root && (
        <p className="text-sm text-destructive" role="alert">
          {errors.root.message}
        </p>
      )}
      <Button type="submit" disabled={isSubmitting} className="w-full">
        {isSubmitting ? 'Đang lưu...' : 'Lưu'}
      </Button>
    </form>
  );
}
