import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { Button } from '@vsite/ui';
import { getErrorCode } from '@vsite/shared';
import {
  getGetWebsiteQueryKey,
  getListPagesQueryKey,
  useCreateWebsite,
  useGetShop,
  useGetWebsite,
  useListPublications,
  WebsiteStatus,
  type WebsiteDto,
} from '@vsite/api-sdk';
import { getErrorMessage } from '../../lib/error-messages';
import { shopSiteUrl } from '../../lib/site-url';
import { PageList } from './page-list';
import { PublicationHistory } from './publication-history';
import { PublishDialog } from './publish-dialog';

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
    <div className="space-y-6">
      <PublishStatus shopId={shopId} website={websiteQuery.data} />
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
      <PublicationHistory shopId={shopId} />
    </div>
  );
}

/** Khối trạng thái xuất bản: nhãn, nút Xuất bản (dialog F3), link "Mở site". */
function PublishStatus({ shopId, website }: { shopId: string; website: WebsiteDto }) {
  const [dialogOpen, setDialogOpen] = useState(false);
  const shopQuery = useGetShop(shopId);
  const publicationsQuery = useListPublications(shopId);

  const hasPublication = website.currentPublicationId !== null;
  const siteUrl = shopQuery.data ? shopSiteUrl(shopQuery.data.slug) : null;
  const currentVersion = publicationsQuery.data?.find((p) => p.id === website.currentPublicationId)?.version;

  let label: string;
  if (website.status === WebsiteStatus.Suspended) {
    // R8: vẫn xuất bản được, nhưng phải nói rõ site công khai đang không hiển thị.
    label = 'Đang bị đình chỉ — site công khai không hiển thị nội dung';
  } else if (website.status === WebsiteStatus.Published && hasPublication) {
    label = currentVersion === undefined ? 'Đã xuất bản' : `Đang chạy bản v${String(currentVersion)}`;
  } else {
    label = 'Chưa xuất bản';
  }

  return (
    <div className="flex items-center justify-between gap-3 rounded-md border border-border px-4 py-3">
      <p className="font-medium text-foreground">{label}</p>
      <div className="flex items-center gap-2">
        {siteUrl && hasPublication && (
          <a
            href={siteUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="rounded-md border border-border px-3 py-1.5 text-sm font-medium hover:bg-muted"
          >
            Mở site
          </a>
        )}
        <Button
          type="button"
          size="sm"
          onClick={() => {
            setDialogOpen(true);
          }}
        >
          Xuất bản
        </Button>
      </div>
      <PublishDialog
        shopId={shopId}
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        siteUrl={hasPublication ? siteUrl : null}
      />
    </div>
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
