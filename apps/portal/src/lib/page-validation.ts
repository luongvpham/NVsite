import { z } from 'zod';
import type { CreatePageRequest, UpdatePageRequest } from '@vsite/api-sdk';
import localesConfig from '../../../../config/locales.json';
import systemPagesConfig from '../../../../config/system-pages.json';
import { reservedRoutes } from './reserved-routes';
import { getErrorMessage } from './error-messages';

/**
 * Luật slug trang Composable — phản chiếu `backend/src/Vsite.Application/Website/Validation/SlugRules.cs`
 * và `Page.cs` để khỏi round-trip 422 vô ích. 422/409 từ BE vẫn luôn là nguồn sự thật cuối.
 *
 * "Chiếm chỗ" (kiểm trước, so không phân biệt hoa/thường): mã trong `config/locales.json` (kể cả locale
 * chưa bật, #98), MỌI route trong `config/system-pages.json` (kể cả loại chưa seed), và
 * `reservedShopSitePaths` của reserved-routes (#24). **Không** dùng `reservedPaths` — danh sách đó dành cho
 * slug SHOP, dùng cho trang sẽ chặn oan `blog`/`about`.
 */
const MAX_SLUG = 120;
const MAX_TITLE = 160;
const MAX_SEO_TITLE = 160;
const MAX_SEO_DESCRIPTION = 320;
const SLUG_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/;

const taken = new Set<string>([
  ...localesConfig,
  ...Object.values(systemPagesConfig as Record<string, { routes: string[] }>).flatMap((page) => page.routes),
  ...reservedRoutes.reservedShopSitePaths,
].map((slug) => slug.toLowerCase()));

export type PageSlugProblem = 'PAGE_SLUG_RESERVED' | 'PAGE_SLUG_INVALID';

/** `null` = hợp lệ. Thứ tự giống BE: chiếm chỗ trước, rồi mới tới sai khuôn (nên `EN` báo "dành riêng"). */
export function checkPageSlug(slug: string): PageSlugProblem | null {
  if (taken.has(slug.toLowerCase())) return 'PAGE_SLUG_RESERVED';
  if (slug.length === 0 || slug.length > MAX_SLUG || !SLUG_PATTERN.test(slug)) return 'PAGE_SLUG_INVALID';
  return null;
}

export interface PageFormValues {
  slug: string;
  title: string;
  seoTitle: string;
  seoDescription: string;
  ogImageId: string | null;
  noIndex: boolean;
  isEnabled: boolean;
}

export interface PageFormLocks {
  /** Trang chủ (slug rỗng) và System page: không đổi được slug. */
  slugLocked: boolean;
  /** NotFound: `noIndex` bắt buộc (`PAGE_NOINDEX_REQUIRED`). */
  noIndexLocked?: boolean;
}

export function buildPageFormSchema({ slugLocked, noIndexLocked = false }: PageFormLocks) {
  return z
    .object({
      slug: z.string(),
      title: z
        .string()
        .trim()
        .min(1, 'Vui lòng nhập tên trang')
        .max(MAX_TITLE, `Tên trang tối đa ${String(MAX_TITLE)} ký tự`),
      seoTitle: z.string().max(MAX_SEO_TITLE, `Tiêu đề SEO tối đa ${String(MAX_SEO_TITLE)} ký tự`),
      seoDescription: z.string().max(MAX_SEO_DESCRIPTION, `Mô tả SEO tối đa ${String(MAX_SEO_DESCRIPTION)} ký tự`),
      ogImageId: z.string().uuid().nullable(),
      noIndex: z.boolean(),
      isEnabled: z.boolean(),
    })
    .superRefine((values, ctx) => {
      if (!slugLocked) {
        const problem = checkPageSlug(values.slug);
        if (problem) ctx.addIssue({ code: 'custom', path: ['slug'], message: getErrorMessage(problem) });
      }
      if (noIndexLocked && !values.noIndex) {
        ctx.addIssue({ code: 'custom', path: ['noIndex'], message: getErrorMessage('PAGE_NOINDEX_REQUIRED') });
      }
    });
}

/** Chuỗi rỗng/chỉ khoảng trắng → `null` (contract: SEO là nullable). */
function blankToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

export function toCreatePageRequest(values: PageFormValues): CreatePageRequest {
  return {
    slug: values.slug.trim(),
    title: values.title.trim(),
    seoTitle: blankToNull(values.seoTitle),
    seoDescription: blankToNull(values.seoDescription),
    ogImageId: values.ogImageId,
    noIndex: values.noIndex,
    isEnabled: values.isEnabled,
  };
}

/** `PUT` thay toàn bộ → luôn đủ field. `slug = null` nghĩa là không đổi (trang chủ/System). */
export function toUpdatePageRequest(values: PageFormValues, { slugLocked }: PageFormLocks): UpdatePageRequest {
  return {
    slug: slugLocked ? null : values.slug.trim(),
    title: values.title.trim(),
    seoTitle: blankToNull(values.seoTitle),
    seoDescription: blankToNull(values.seoDescription),
    ogImageId: values.ogImageId,
    noIndex: values.noIndex,
    isEnabled: values.isEnabled,
  };
}
