import { Button } from '@vsite/ui';
import { getErrorCode } from '@vsite/shared';
import { PageKind, useListPages, type PageDto } from '@vsite/api-sdk';
import { getErrorMessage } from '../../lib/error-messages';

function kindLabel(page: PageDto): string {
  if (page.isHomePage) return 'Trang chủ';
  if (page.kind === PageKind.System) return 'Trang hệ thống';
  return 'Trang nội dung';
}

/** `slug` rỗng = trang chủ, `null` = NotFound (không có URL riêng). */
function slugLabel(page: PageDto): string {
  if (page.slug === null) return '(không có đường dẫn)';
  return `/${page.slug}`;
}

/** Danh sách trang (Flow 2, F1 chỉ đọc). Không phân trang; sắp theo `sortOrder` (chỉ đọc). */
export function PageList({ shopId }: { shopId: string }) {
  const pagesQuery = useListPages(shopId);

  if (pagesQuery.isPending) return <p className="text-muted-foreground">Đang tải danh sách trang...</p>;

  if (pagesQuery.isError) {
    return (
      <div className="space-y-3">
        <p role="alert" className="text-destructive">
          {getErrorMessage(getErrorCode(pagesQuery.error))}
        </p>
        <Button type="button" variant="outline" size="sm" onClick={() => void pagesQuery.refetch()}>
          Thử lại
        </Button>
      </div>
    );
  }

  const pages = [...pagesQuery.data].sort((a, b) => a.sortOrder - b.sortOrder);
  const hasContentPage = pages.some((p) => p.kind === PageKind.Composable && !p.isHomePage);

  return (
    <div className="space-y-4">
      {!hasContentPage && (
        <p className="rounded-md bg-muted px-4 py-3 text-sm text-muted-foreground">
          Chưa có trang nội dung nào ngoài trang chủ. Tạo thêm trang (vd. Dịch vụ, Giới thiệu) để khách xem.
        </p>
      )}
      <ul aria-label="Danh sách trang" className="divide-y divide-border rounded-md border border-border">
        {pages.map((page) => (
          <li key={page.id} className="flex items-center justify-between px-4 py-3">
            <div>
              <p className="font-medium text-foreground">{page.title}</p>
              <p className="text-sm text-muted-foreground">
                {slugLabel(page)} · {kindLabel(page)}
              </p>
            </div>
            <div className="flex items-center gap-2 text-xs">
              {!page.isEnabled && <span className="rounded-full bg-muted px-2 py-0.5 text-muted-foreground">Đang tắt</span>}
              {page.isDirty && (
                <span className="rounded-full bg-primary/10 px-2 py-0.5 text-primary">Có thay đổi chưa xuất bản</span>
              )}
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
