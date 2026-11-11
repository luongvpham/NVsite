import emptyHome from '../../../builder-components/tests/fixtures/tree/valid-empty-home.json';
import home from '../../../builder-components/tests/fixtures/tree/valid-home.json';
import linkUrls from '../../../builder-components/tests/fixtures/tree/valid-link-urls.json';
import richText from '../../../builder-components/tests/fixtures/tree/valid-richtext-reformatted.json';
import systemContact from '../../../builder-components/tests/fixtures/tree/valid-system-contact.json';
import type { ComponentNode } from '@vsite/builder-components';
import { themeStyle } from '@vsite/theme-engine';
import { RenderContextProvider } from '../../src/context';
import { RenderTree, type PageRoot } from '../../src/renderTree';
import { SiteLayout } from '../../src/site-layout';

type TreeFixture = { tree?: ComponentNode | PageRoot };

/**
 * Fixture `valid-*.json` có `tree` (bỏ cặp stored/incoming của trang System). Liệt kê tay vì renderer không có
 * @types/node để glob thư mục — thêm fixture `valid-*` mới ở builder-components thì thêm một dòng ở đây.
 */
const fixtures: Record<string, TreeFixture> = {
  'valid-empty-home.json': emptyHome as TreeFixture,
  'valid-home.json': home as TreeFixture,
  'valid-link-urls.json': linkUrls as TreeFixture,
  'valid-richtext-reformatted.json': richText as TreeFixture,
  'valid-system-contact.json': systemContact as TreeFixture,
};

export function loadValidTrees(): { name: string; tree: ComponentNode | PageRoot }[] {
  return Object.entries(fixtures).flatMap(([name, json]) => (json.tree ? [{ name, tree: json.tree }] : []));
}

const pages = [
  { id: 'p-home', slug: '', systemType: null, isEnabled: true },
  { id: 'p-contact', slug: 'lien-he', systemType: 'Contact', isEnabled: true },
];

/** Đúng cấu trúc preview/8b: theme → SiteLayout → RenderTree. */
export function SiteApp({ tree }: { tree: ComponentNode | PageRoot }) {
  return (
    <div style={themeStyle()}>
      <RenderContextProvider>
        <SiteLayout shop={{ name: 'Spa ABC', logoUrl: '/media/s/logo.png' }} homeHref="/" year={2026}>
          <RenderTree tree={tree} basePath="" locale="vi" defaultLocale="vi" pages={pages} />
        </SiteLayout>
      </RenderContextProvider>
    </div>
  );
}
