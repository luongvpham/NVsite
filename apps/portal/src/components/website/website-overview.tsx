import { useQueryClient } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { Button } from '@vsite/ui';
import { getErrorCode } from '@vsite/shared';
import { getGetWebsiteQueryKey, getListPagesQueryKey, useCreateWebsite, useGetWebsite } from '@vsite/api-sdk';
import { getErrorMessage } from '../../lib/error-messages';
import { PageList } from './page-list';

/**
 * Màn tổng quan Website (Flow 1). Chưa có website = `getWebsite` 404 `WEBSITE_NOT_FOUND` — **không**
 * dùng `listPages` để phát hiện (chưa có website nó trả `200 []`, brief "Tạo website").
 */
export function WebsiteOverview({ shopId }: { shopId: string }) {
  // 404 là trạng thái bình thường (chưa tạo) — không retry.
  const websiteQuery = useGetWebsite(shopId, { query: { retry: false } });

  if (websiteQuery.isPending) return <p className="text-muted-foreground">Đang tải website...</p>;

  if (websiteQuery.isError) {
    const code = getErrorCode(websiteQuery.error);
    if (code === 'WEBSITE_NOT_FOUND') return <CreateWebsitePanel shopId={shopId} />;
    return (
      <div className="space-y-3">
        <p role="alert" className="text-destructive">
          {getErrorMessage(code)}
        </p>
        <Button type="button" variant="outline" size="sm" onClick={() => void websiteQuery.refetch()}>
          Thử lại
        </Button>
      </div>
    );
  }

  return (
    <PageList
      shopId={shopId}
      renderOpenEditor={(page) => (
        <Link
          to="/shops/$shopId/website/pages/$pageId"
          params={{ shopId, pageId: page.id }}
          className="rounded-md border border-border px-2.5 py-1 text-xs font-medium text-foreground hover:bg-muted"
        >
          Soạn nội dung
        </Link>
      )}
    />
  );
}

function CreateWebsitePanel({ shopId }: { shopId: string }) {
  const queryClient = useQueryClient();

  const refreshWebsite = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: getGetWebsiteQueryKey(shopId) }),
      queryClient.invalidateQueries({ queryKey: getListPagesQueryKey(shopId) }),
    ]);

  const createMutation = useCreateWebsite({
    mutation: {
      onSuccess: () => refreshWebsite(),
      onError: (error) => {
        // Tab khác vừa tạo → coi như đã có, tải lại thay vì báo lỗi.
        if (getErrorCode(error) === 'WEBSITE_ALREADY_EXISTS') void refreshWebsite();
      },
    },
  });

  const errorCode = createMutation.isError ? getErrorCode(createMutation.error) : undefined;
  const showError = createMutation.isError && errorCode !== 'WEBSITE_ALREADY_EXISTS';

  return (
    <div className="space-y-3 rounded-md border border-dashed border-border p-6 text-center">
      <p className="text-foreground">Shop này chưa có website.</p>
      <p className="text-sm text-muted-foreground">
        Tạo website để có trang chủ, trang Liên hệ và trang Không tìm thấy dựng sẵn, rồi bắt đầu thêm nội dung.
      </p>
      {showError && (
        <p role="alert" className="text-sm text-destructive">
          {getErrorMessage(errorCode)}
        </p>
      )}
      <Button type="button" disabled={createMutation.isPending} onClick={() => {
          createMutation.mutate({ shopId });
        }}>
        {createMutation.isPending ? 'Đang tạo...' : 'Tạo website'}
      </Button>
    </div>
  );
}
