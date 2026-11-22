// @vitest-environment node
import { siteSnapshotSchema } from '@vsite/builder-components';
import { renderToString } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import themeTokensConfig from '../../../../config/theme-tokens.json';
import { SiteLoaderKind, type SiteLoaderData } from '../server/load-site-core';
import { SiteView } from './site-view';

/** Fixture snapshot dựng tay qua schema (A12). */
const tree = (children: unknown[]) => ({ schemaVersion: 1, id: 'root', type: 'Page', children });
const richText = (id: string, html: string) => ({
  type: 'RichText',
  variant: 'RichText01',
  id,
  props: { content: html, align: 'left' },
});
const page = (id: string, slug: string | null, title: string, children: unknown[], systemType: string | null = null) => ({
  id,
  slug,
  kind: slug === null ? 'System' : 'Composable',
  systemType,
  title,
  seo: { title: null, description: null, ogImageId: null, noIndex: false },
  tree: tree(children),
});

function snapshot(pages: unknown[], navItems: unknown[] = []) {
  return siteSnapshotSchema.parse({
    schemaVersion: 1,
    version: 1,
    defaultLocale: 'vi',
    locales: ['vi'],
    theme: { presetKey: null, tokens: { ...themeTokensConfig.defaults, colors: { ...themeTokensConfig.defaults.colors, primary: '#123456' } } },
    navigation: { items: navItems },
    seo: { defaultTitle: null, defaultDescription: null, ogImageId: null, faviconAssetId: null },
    tracking: {},
    media: {},
    pages,
  });
}

const PAGES = [
  page('p-home', '', 'Trang chủ', [richText('c_home1', '<p>Nội dung trang chủ XYZ</p>')]),
  page('p-contact', 'lien-he', 'Liên hệ', [richText('c_contact1', '<p>Nội dung liên hệ QWE</p>')], 'Contact'),
  page('p-nf', null, 'Không tìm thấy', [richText('c_notfound1', '<p>Trang riêng của shop không tồn tại</p>')], 'NotFound'),
];

type SiteData = Extract<SiteLoaderData, { kind: 'site' }>;
const data = (path: string, pages: unknown[] = PAGES, navItems: unknown[] = []): SiteData => ({
  kind: SiteLoaderKind.Site,
  shop: { name: 'Spa ABC', logoUrl: '/media/s/logo.png' },
  snapshot: snapshot(pages, navItems) as SiteData['snapshot'],
  publishedAt: '2026-11-01T10:00:00+00:00',
  path,
  year: 2027,
  origin: 'https://spa-abc.vsite.vn',
});
const html = (d: SiteData) => renderToString(<SiteView data={d} />);

describe('SiteView SSR (#103)', () => {
  it('trang chủ: nội dung cây + layout (tên shop, © năm từ loader) + theme', () => {
    const out = html(data('/'));
    expect(out).toContain('Nội dung trang chủ XYZ');
    expect(out).not.toContain('Nội dung liên hệ');
    expect(out).toContain('Spa ABC');
    expect(out).toContain('© 2027 Spa ABC');
    expect(out).toContain('/media/s/logo.png');
    expect(out).toContain('#123456');
  });
  it('chọn trang theo slug, kể cả "/" cuối', () => {
    expect(html(data('/lien-he'))).toContain('Nội dung liên hệ QWE');
    expect(html(data('/lien-he/'))).toContain('Nội dung liên hệ QWE');
  });
  it('không khớp → trang NotFound của shop, trong layout', () => {
    const out = html(data('/khong-co'));
    expect(out).toContain('Trang riêng của shop không tồn tại');
    expect(out).toContain('© 2027 Spa ABC');
  });
  it('R7: thiếu trang chủ → NotFound của shop', () => {
    expect(html(data('/', PAGES.slice(1)))).toContain('Trang riêng của shop không tồn tại');
  });
  it('R6: /media/x không ra trang site', () => {
    expect(html(data('/media/x'))).not.toContain('Nội dung trang chủ XYZ');
  });
  it('không có trang NotFound → placeholder tối thiểu', () => {
    const out = html(data('/khong-co', PAGES.slice(0, 2)));
    expect(out).toContain('Không tìm thấy trang');
    expect(out).not.toContain('Nội dung');
  });

  describe('menu (WEBSITE-007, #113)', () => {
    const G_HOME = '11111111-1111-4111-8111-111111111111';
    const G_CONTACT = '22222222-2222-4222-8222-222222222222';
    const G_GONE = '33333333-3333-4333-8333-333333333333';
    const MENU_PAGES = [
      page(G_HOME, '', 'Trang chủ', [richText('c_home1', '<p>Home</p>')]),
      page(G_CONTACT, 'lien-he', 'Liên hệ', [richText('c_contact1', '<p>Contact</p>')], 'Contact'),
      page('p-nf', null, 'Không tìm thấy', [richText('c_notfound1', '<p>NF</p>')], 'NotFound'),
    ];
    const navPage = (id: string, label: string | null, children?: unknown[]) => ({
      key: `page:${id}`,
      label,
      ...(children ? { children } : {}),
    });
    const navHtml = (items: unknown[]) => html(data('/', MENU_PAGES, items));

    it('hiện menu cấp 1 và con, href đi qua resolveUrl', () => {
      const out = navHtml([navPage(G_HOME, 'Nhà', [navPage(G_CONTACT, 'Gặp chúng tôi')])]);
      expect(out).toContain('Nhà');
      expect(out).toContain('Gặp chúng tôi');
      expect(out).toContain('href="/lien-he"');
      expect(out).toContain('href="/"');
    });
    it('R6: items rỗng → header không menu, không lỗi', () => {
      const out = navHtml([]);
      expect(out).toContain('Spa ABC');
      expect(out).not.toContain('<nav');
    });
    it('mục mồ côi (trang không có trong snapshot.pages) không render', () => {
      const out = navHtml([navPage(G_GONE, 'Mồ côi XYZ'), navPage(G_CONTACT, 'Liên hệ menu')]);
      expect(out).not.toContain('Mồ côi XYZ');
      expect(out).toContain('Liên hệ menu');
    });
  });
});
