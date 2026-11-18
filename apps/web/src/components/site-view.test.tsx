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

function snapshot(pages: unknown[]) {
  return siteSnapshotSchema.parse({
    schemaVersion: 1,
    version: 1,
    defaultLocale: 'vi',
    locales: ['vi'],
    theme: { presetKey: null, tokens: { ...themeTokensConfig.defaults, colors: { ...themeTokensConfig.defaults.colors, primary: '#123456' } } },
    navigation: { items: [] },
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
const data = (path: string, pages: unknown[] = PAGES): SiteData => ({
  kind: SiteLoaderKind.Site,
  shop: { name: 'Spa ABC', logoUrl: '/media/s/logo.png' },
  snapshot: snapshot(pages) as SiteData['snapshot'],
  publishedAt: '2026-11-01T10:00:00+00:00',
  path,
  year: 2027,
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
});
