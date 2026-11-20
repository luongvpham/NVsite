import { Link } from '@tanstack/react-router';
import type { PublishBlockedError, PublishBlockedParams } from '@vsite/shared';
import { getErrorMessage } from '../../lib/error-messages';

export interface PublishErrorsProps {
  shopId: string;
  params: PublishBlockedParams;
  /** Trang đang mở trong editor: lỗi thuộc trang này hiện "trang này" thay vì link vô ích. */
  currentPageId?: string;
}

interface Group {
  pageId: string | null;
  title: string | null;
  errors: PublishBlockedError[];
}

/** Lỗi cấp website (`pageId: null`) đứng đầu; các trang giữ thứ tự xuất hiện đầu tiên. */
function groupErrors(errors: PublishBlockedError[]): Group[] {
  const groups = new Map<string | null, Group>();
  for (const error of errors) {
    const existing = groups.get(error.pageId);
    if (existing) existing.errors.push(error);
    else groups.set(error.pageId, { pageId: error.pageId, title: error.pageTitle, errors: [error] });
  }
  const all = [...groups.values()];
  return [...all.filter((g) => g.pageId === null), ...all.filter((g) => g.pageId !== null)];
}

export function PublishErrors({ shopId, params, currentPageId }: PublishErrorsProps) {
  const groups = groupErrors(params.errors);
  return (
    <div className="space-y-2">
      <ul aria-label="Lỗi chặn xuất bản" className="space-y-3">
        {groups.map((group) => (
          <li key={group.pageId ?? 'website'} data-testid="publish-error-group" className="text-sm">
            <div className="flex items-center gap-2 font-medium text-foreground">
              <span>{group.pageId === null ? 'Cài đặt website' : (group.title ?? 'Trang')}</span>
              {group.pageId !== null &&
                (group.pageId === currentPageId ? (
                  <span className="font-normal text-muted-foreground">trang này</span>
                ) : (
                  <Link
                    to="/shops/$shopId/website/pages/$pageId"
                    params={{ shopId, pageId: group.pageId }}
                    className="font-normal text-primary hover:underline"
                  >
                    Mở trang
                  </Link>
                ))}
            </div>
            <ul className="mt-1 list-disc space-y-0.5 pl-5 text-destructive">
              {group.errors.map((error, index) => (
                <li key={`${error.path}-${error.code}-${index}`}>
                  {error.message === '' ? getErrorMessage(error.code) : error.message}
                </li>
              ))}
            </ul>
          </li>
        ))}
      </ul>
      {params.truncated && (
        <p className="text-sm text-muted-foreground">
          Còn lỗi khác chưa hiển thị — sửa các lỗi trên rồi xuất bản lại.
        </p>
      )}
    </div>
  );
}
