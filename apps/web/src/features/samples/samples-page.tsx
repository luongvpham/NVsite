import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import {
  createSampleBody,
  getListSamplesQueryKey,
  useCreateSample,
  useListSamples,
} from '@vsite/api-sdk';
import { getErrorCode } from '@vsite/shared';
import { Button } from '@vsite/ui';
import { useForm } from 'react-hook-form';
import type { z } from 'zod';

type CreateSampleForm = z.infer<typeof createSampleBody>;

/**
 * Màn hình chứng minh pipeline Bước 1 — throwaway, xoá cùng lúc với Sample module
 * (xem docs/tasks/CLEANUP-SAMPLE.md). Không phải màn hình sản phẩm thật.
 */
export function SamplesPage() {
  const queryClient = useQueryClient();
  const listQuery = useListSamples({ page: 1, pageSize: 20 });
  const createMutation = useCreateSample();

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<CreateSampleForm>({
    resolver: zodResolver(createSampleBody),
    defaultValues: { name: '', status: 'Draft' },
  });

  const onSubmit = handleSubmit(async (values) => {
    await createMutation.mutateAsync({ data: values });
    reset();
    await queryClient.invalidateQueries({ queryKey: getListSamplesQueryKey({ page: 1, pageSize: 20 }) });
  });

  return (
    <main className="mx-auto max-w-2xl p-8">
      <h1 className="text-xl font-semibold">Samples (proof-of-pipeline)</h1>

      <form onSubmit={onSubmit} className="mt-6 space-y-3 rounded-md border border-border p-4">
        <div>
          <label className="block text-sm font-medium" htmlFor="shopId">
            Shop ID (uuid)
          </label>
          <input id="shopId" className="mt-1 w-full rounded border border-border px-2 py-1" {...register('shopId')} />
          {errors.shopId ? <p className="mt-1 text-sm text-destructive">{errors.shopId.message}</p> : null}
        </div>

        <div>
          <label className="block text-sm font-medium" htmlFor="name">
            Name
          </label>
          <input id="name" className="mt-1 w-full rounded border border-border px-2 py-1" {...register('name')} />
          {errors.name ? <p className="mt-1 text-sm text-destructive">{errors.name.message}</p> : null}
        </div>

        <Button type="submit" disabled={isSubmitting}>
          Tạo sample
        </Button>

        {createMutation.isError ? (
          <p className="text-sm text-destructive">Lỗi: {getErrorCode(createMutation.error) ?? 'unknown_error'}</p>
        ) : null}
      </form>

      <section className="mt-8">
        {listQuery.isLoading ? <p>Đang tải…</p> : null}

        {listQuery.isError ? (
          <p className="text-destructive">Lỗi: {getErrorCode(listQuery.error) ?? 'unknown_error'}</p>
        ) : null}

        {listQuery.data && listQuery.data.items.length === 0 ? <p className="text-muted-foreground">Chưa có sample nào.</p> : null}

        {listQuery.data && listQuery.data.items.length > 0 ? (
          <ul className="mt-2 divide-y divide-border">
            {listQuery.data.items.map((item) => (
              <li key={item.id} className="py-2">
                <span className="font-medium">{item.name}</span>{' '}
                <span className="text-sm text-muted-foreground">({item.status})</span>
              </li>
            ))}
          </ul>
        ) : null}
      </section>
    </main>
  );
}
