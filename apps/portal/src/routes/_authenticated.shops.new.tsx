import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Button } from '@vsite/ui';
import { ShopKind, usePostShops } from '@vsite/api-sdk';
import { getErrorCode } from '@vsite/shared';
import { FormField, FormSelect } from '../components/form-field';
import { getErrorMessage } from '../lib/error-messages';
import { createShopFormSchema, type CreateShopFormValues } from '../lib/shop-validation';

export const Route = createFileRoute('/_authenticated/shops/new')({
  component: CreateShopPage,
});

function CreateShopPage() {
  const navigate = useNavigate();
  const {
    register,
    handleSubmit,
    watch,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<CreateShopFormValues>({
    resolver: zodResolver(createShopFormSchema),
    defaultValues: { name: '', slug: '', kind: ShopKind.Hosted, externalUrl: '' },
  });

  const kind = watch('kind');

  const createShopMutation = usePostShops({
    mutation: {
      onSuccess: (shop) => {
        void navigate({ to: '/shops/$shopId', params: { shopId: shop.id } });
      },
      onError: (error) => {
        setError('root', { message: getErrorMessage(getErrorCode(error)) });
      },
    },
  });

  const submitForm = handleSubmit((values) => {
    createShopMutation.mutate({
      data: { ...values, externalUrl: values.kind === ShopKind.ExternalOnly ? values.externalUrl : null },
    });
  });

  return (
    <main className="mx-auto max-w-sm p-6">
      <h1 className="text-xl font-semibold text-foreground">Tạo shop</h1>
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
        {errors.root && (
          <p className="text-sm text-destructive" role="alert">
            {errors.root.message}
          </p>
        )}
        <Button type="submit" disabled={isSubmitting} className="w-full">
          {isSubmitting ? 'Đang tạo...' : 'Tạo shop'}
        </Button>
      </form>
    </main>
  );
}
