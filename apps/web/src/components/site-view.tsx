import { RenderContextProvider } from '@vsite/builder-components';
import { decorateWithAnchor, RenderTree, SiteLayout, useAnchorScroll, type PageRoot } from '@vsite/builder-renderer';
import { themeStyle } from '@vsite/theme-engine';
import { selectPage } from '../lib/select-page';
import type { SiteLoaderData } from '../server/load-site-core';

/**
 * Render snapshot đã publish bằng `builder-renderer` (WEBSITE-005 T5, #103). Cùng cấu trúc preview của Portal:
 * theme → SiteLayout → RenderContextProvider → RenderTree. Isomorphic (#23): không window/document/Date —
 * năm © từ loader data. Chọn trang bằng `selectPage` (cùng hàm loader dùng để đặt status 404).
 */
export type SiteViewData = Extract<SiteLoaderData, { kind: 'site' }>;

export function SiteView({ data }: Readonly<{ data: SiteViewData }>) {
  useAnchorScroll(); // anchor #nodeId: cuộn tới wrapper `display: contents` sau hydrate / hashchange
  const { snapshot, shop, year, path } = data;
  const selection = selectPage(snapshot, path);

  // Shop không có trang NotFound (hiếm: BE seed sẵn) → placeholder tối thiểu; T8 làm đẹp.
  if (!selection.page) {
    return (
      <main>
        <h1>Không tìm thấy trang</h1>
      </main>
    );
  }

  const pages = snapshot.pages.map((p) => ({ id: p.id, slug: p.slug, systemType: p.systemType, isEnabled: true }));
  return (
    <div style={themeStyle(snapshot.theme.tokens)}>
      <SiteLayout shop={{ name: shop.name, logoUrl: shop.logoUrl }} homeHref="/" year={year}>
        <RenderContextProvider value={{ mediaMap: snapshot.media }}>
          <RenderTree
            tree={selection.page.tree as unknown as PageRoot}
            basePath=""
            locale={snapshot.defaultLocale}
            defaultLocale={snapshot.defaultLocale}
            pages={pages}
            decorateNode={decorateWithAnchor}
          />
        </RenderContextProvider>
      </SiteLayout>
    </div>
  );
}
