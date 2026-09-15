import { createFileRoute, Link } from '@tanstack/react-router';
import { useGetShops } from '@vsite/api-sdk';
import { getErrorCode } from '@vsite/shared';
import { getErrorMessage } from '../lib/error-messages';

export const Route = createFileRoute('/_authenticated/shops/')({
  component: ShopListPage,
});

const KIND_LABEL: Record<string, string> = {
  Hosted: 'Website trên vsite',
  ExternalOnly: 'Chỉ liên kết ngoài',
};

const STATUS_LABEL: Record<string, string> = {
  Draft: 'Nháp',
  Active: 'Đang hoạt động',
  Suspended: 'Tạm ngưng',
  Closed: 'Đã đóng',
};

function ShopListPage() {
  const shopsQuery = useGetShops();

  return (
    <main className="mx-auto max-w-3xl p-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-foreground">Shop của tôi</h1>
        <Link
          to="/shops/new"
          className="inline-flex items-center justify-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:opacity-90"
        >
          Tạo shop
        </Link>
      </div>

      {shopsQuery.isPending && <p className="mt-6 text-muted-foreground">Đang tải...</p>}

      {shopsQuery.isError && (
        <p className="mt-6 text-destructive" role="alert">
          {getErrorMessage(getErrorCode(shopsQuery.error))}
        </p>
      )}

      {shopsQuery.isSuccess && shopsQuery.data.length === 0 && (
        <p className="mt-6 text-muted-foreground">Bạn chưa có shop nào. Tạo shop đầu tiên để bắt đầu.</p>
      )}

      {shopsQuery.isSuccess && shopsQuery.data.length > 0 && (
        <ul className="mt-6 divide-y divide-border rounded-md border border-border">
          {shopsQuery.data.map((shop) => (
            <li key={shop.id}>
              <Link
                to="/shops/$shopId"
                params={{ shopId: shop.id }}
                className="flex items-center justify-between px-4 py-3 hover:bg-muted"
              >
                <div>
                  <p className="font-medium text-foreground">{shop.name}</p>
                  <p className="text-sm text-muted-foreground">
                    {shop.slug} · {KIND_LABEL[shop.kind] ?? shop.kind}
                  </p>
                </div>
                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                  <span>{shop.roleCode}</span>
                  <span className="rounded-full bg-muted px-2 py-0.5">{STATUS_LABEL[shop.status] ?? shop.status}</span>
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}
