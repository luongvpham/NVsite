import { useCallback, useEffect, useMemo, useState, type MouseEvent, type ReactNode } from 'react';
import { useStore } from 'zustand';
import { Button } from '@vsite/ui';
import { getErrorCode } from '@vsite/shared';
import { useGetShop, useGetWebsite, useListPages } from '@vsite/api-sdk';
import { createResolveUrl, matchSiteUrl, type SitePageRef, type SiteUrlOptions } from '@vsite/builder-components';
import {
  RenderContextProvider,
  RenderTree,
  SiteLayout,
  type DecorateNode,
  type PageRoot,
} from '@vsite/builder-renderer';
import type { EditorStore } from '@vsite/builder-core';
import { themeStyle } from '@vsite/theme-engine';
import { getErrorMessage } from '../../lib/error-messages';
import { usePreviewPage } from '../../hooks/use-preview-page';

/** G5 — chuỗi ảo, không phải route thật của Portal: lớp phủ không đổi URL trình duyệt. */
export const PREVIEW_BASE = '/__preview';

type Notice = 'disabled' | 'systemMissing' | 'deleted';

const NOTICE_TEXT: Record<Notice, string> = {
  disabled: 'Trang này đang tắt — khách sẽ thấy trang 404',
  systemMissing: 'Trang hệ thống này chưa được tạo — khách sẽ thấy trang 404',
  deleted: 'Liên kết trỏ tới trang đã xoá',
};

/** Một "màn" đang xem: trang nào + (nếu là NotFound do link hỏng) lý do. */
interface View {
  pageId: string | null;
  notice?: Notice;
}

/** Bọc mỗi node bằng `id` để link `anchor` (#nodeId) cuộn tới được; `display: contents` giữ nguyên bố cục. */
const decorateWithId: DecorateNode = (node, rendered) => (
  <div id={node.id} style={{ display: 'contents' }}>
    {rendered}
  </div>
);

/**
 * Xem trước toàn trang (Bước 6): lớp phủ toàn màn hình trong editor. Khung SiteLayout + theme đang chỉnh +
 * nội dung trang (trang đang sửa đọc từ store, trang khác đọc draft server). Chỉ đọc — không gọi API ghi nào.
 */
export function SitePreview({
  shopId,
  pageId,
  store,
  onClose,
}: {
  shopId: string;
  /** Trang đang sửa trong editor. */
  pageId: string;
  store: EditorStore;
  onClose: () => void;
}) {
  const websiteQuery = useGetWebsite(shopId, { query: { retry: false, staleTime: 0 } });
  const pagesQuery = useListPages(shopId, { query: { retry: false, staleTime: 0 } });
  const shopQuery = useGetShop(shopId);
  const themeDraft = useStore(store, (s) => s.themeDraft);

  const [history, setHistory] = useState<View[]>([]);
  const [view, setView] = useState<View>({ pageId });
  // Năm © tính một lần ở client (G6) — không đọc đồng hồ trong lúc render renderer.
  const [year] = useState(() => new Date().getFullYear());

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [onClose]);

  const pages = pagesQuery.data;
  const website = websiteQuery.data;
  const refs = useMemo<SitePageRef[]>(
    () => (pages ?? []).map((p) => ({ id: p.id, slug: p.slug, systemType: p.systemType, isEnabled: p.isEnabled })),
    [pages],
  );
  const urlOptions = useMemo<SiteUrlOptions | null>(
    () =>
      website
        ? { basePath: PREVIEW_BASE, locale: website.defaultLocale, defaultLocale: website.defaultLocale, pages: refs }
        : null,
    [website, refs],
  );
  const notFoundId = pages?.find((p) => p.systemType === 'NotFound')?.id ?? null;

  const navigate = (next: View) => {
    setHistory((h) => [...h, view]);
    setView(next);
  };

  const onClickCapture = (e: MouseEvent<HTMLElement>) => {
    if (!(e.target instanceof Element)) return;
    const anchor = e.target.closest('a');
    if (!anchor || !urlOptions) return;
    // R4: link trong preview không bao giờ điều hướng thật / rời editor.
    e.preventDefault();
    e.stopPropagation();
    const href = anchor.getAttribute('href') ?? '';
    const match = matchSiteUrl(href, urlOptions);
    switch (match.kind) {
      case 'external':
        window.open(href, '_blank', 'noopener,noreferrer');
        return;
      case 'anchor':
        {
          // Wrapper `display: contents` không có box nên scrollIntoView lên nó không làm gì — cuộn tới phần tử con đầu tiên.
          const wrapper = document.getElementById(match.nodeId);
          const target = wrapper?.firstElementChild ?? wrapper;
          // jsdom không có scrollIntoView — kiểm ở runtime.
          // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
          target?.scrollIntoView?.({ behavior: 'smooth' });
        }
        return;
      case 'page':
        navigate(match.enabled ? { pageId: match.pageId } : { pageId: notFoundId, notice: 'disabled' });
        return;
      case 'systemMissing':
        navigate({ pageId: notFoundId, notice: 'systemMissing' });
        return;
      case 'deleted':
        setView((v) => ({ ...v, notice: 'deleted' }));
        return;
    }
  };

  const goBack = useCallback(() => {
    setHistory((h) => {
      const previous = h[h.length - 1];
      if (previous) setView(previous);
      return h.slice(0, -1);
    });
  }, []);

  const loadError = websiteQuery.error ?? pagesQuery.error ?? shopQuery.error;
  const loading = websiteQuery.isPending || pagesQuery.isPending || shopQuery.isPending;
  const shop = shopQuery.data;

  let body: ReactNode;
  if (loadError) {
    body = (
      <div className="space-y-3 p-6">
        <p role="alert" className="text-destructive">
          {getErrorMessage(getErrorCode(loadError))}
        </p>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => {
            void websiteQuery.refetch();
            void pagesQuery.refetch();
            void shopQuery.refetch();
          }}
        >
          Thử lại
        </Button>
      </div>
    );
  } else if (loading || !urlOptions || !shop) {
    body = <p className="p-6 text-muted-foreground">Đang tải trang</p>;
  } else {
    const homeId = pages?.find((p) => p.isHomePage)?.id ?? website?.homePageId;
    const homeHref = homeId ? createResolveUrl(urlOptions)({ kind: 'page', pageId: homeId }) : '#';
    body = (
      <div style={themeStyle(themeDraft)}>
        <SiteLayout shop={{ name: shop.name, logoUrl: shop.logoUrl }} homeHref={homeHref} year={year}>
          <PreviewBody
            key={view.pageId ?? 'none'}
            shopId={shopId}
            pageId={view.pageId}
            editingPageId={pageId}
            store={store}
            urlOptions={urlOptions}
          />
        </SiteLayout>
      </div>
    );
  }

  return (
    <div role="dialog" aria-modal="true" aria-label="Xem trước trang" className="fixed inset-0 z-50 flex flex-col bg-background">
      <div className="flex items-center gap-2 border-b border-border px-4 py-2">
        {history.length > 0 && (
          <Button type="button" variant="outline" size="sm" onClick={goBack}>
            ← Trang trước
          </Button>
        )}
        <span className="text-sm text-muted-foreground">Xem trước — khách sẽ thấy như thế này</span>
        <Button type="button" variant="outline" size="sm" className="ml-auto" onClick={onClose}>
          Đóng xem trước
        </Button>
      </div>
      {view.notice && (
        <p role="status" className="border-b border-border bg-muted px-4 py-2 text-sm text-foreground">
          {NOTICE_TEXT[view.notice]}
        </p>
      )}
      <div className="min-h-0 flex-1 overflow-y-auto" onClickCapture={onClickCapture}>
        {body}
      </div>
    </div>
  );
}

function PreviewBody({
  shopId,
  pageId,
  editingPageId,
  store,
  urlOptions,
}: {
  shopId: string;
  pageId: string | null;
  editingPageId: string;
  store: EditorStore;
  urlOptions: SiteUrlOptions;
}) {
  const { state, mediaMap } = usePreviewPage({ shopId, pageId, editingPageId, store });

  if (state.status === 'loading') return <p className="p-6 text-muted-foreground">Đang tải trang</p>;
  if (state.status === 'error') {
    return (
      <div className="space-y-3 p-6">
        <p role="alert" className="text-destructive">
          {state.message}
        </p>
        <Button type="button" variant="outline" size="sm" onClick={state.retry}>
          Thử lại
        </Button>
      </div>
    );
  }
  return (
    <RenderContextProvider value={{ mediaMap }}>
      <RenderTree
        tree={state.tree as unknown as PageRoot}
        decorateNode={decorateWithId}
        basePath={urlOptions.basePath}
        locale={urlOptions.locale}
        defaultLocale={urlOptions.defaultLocale}
        pages={urlOptions.pages}
      />
    </RenderContextProvider>
  );
}
