import { siteSnapshotSchema } from '@vsite/builder-components';
import { act } from 'react';
import { hydrateRoot, type Root } from 'react-dom/client';
import { renderToString } from 'react-dom/server';
import { afterEach, beforeEach, describe, expect, it, vi, type MockInstance } from 'vitest';
import themeTokensConfig from '../../../../config/theme-tokens.json';
import { SiteLoaderKind, type SiteLoaderData } from '../server/load-site-core';
import { SitePage } from './site-page';

/**
 * T10 (WEBSITE-005): HTML do server render từ loader data, client hydrate cùng cây + cùng loader data →
 * React không được cảnh báo lệch (console.error / onRecoverableError). R8: loader chạy 23:59:59 31/12 (giờ VN,
 * năm VN như `load-site-core`), client hydrate lúc 00:00:01 năm sau → năm © vẫn là của loader.
 */

// ---- Fixture snapshot dựng tay qua schema (A12) — có Section lồng, Hero dùng ảnh trong `media`, RichText.
const tree = (children: unknown[]) => ({ schemaVersion: 1, id: 'root', type: 'Page', children });
const richText = (id: string, html: string) => ({
  type: 'RichText',
  variant: 'RichText01',
  id,
  props: { content: html, align: 'left' },
});
const hero = (id: string) => ({
  type: 'Hero',
  variant: 'Hero01',
  id,
  props: { title: 'Thư giãn tại Spa ABC', subtitle: 'Mở cửa 8h–20h', image: { imageId: 'img_hero' }, align: 'center' },
});
const section = (id: string, children: unknown[]) => ({
  type: 'Section',
  variant: 'Section01',
  id,
  props: {},
  children,
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

const SNAPSHOT = siteSnapshotSchema.parse({
  schemaVersion: 1,
  version: 3,
  defaultLocale: 'vi',
  locales: ['vi'],
  theme: { presetKey: null, tokens: themeTokensConfig.defaults },
  navigation: { items: [] },
  seo: { defaultTitle: null, defaultDescription: null, ogImageId: null, faviconAssetId: null },
  tracking: {},
  media: { img_hero: { storageKey: 's/abc/hero_1600x900.jpg', preset: '1600x900,cover' } },
  pages: [
    page('p-home', '', 'Trang chủ', [
      hero('c_hero01'),
      section('c_sect01', [richText('c_text01', '<p>Dịch vụ <strong>massage</strong> thư giãn</p>')]),
    ]),
    page('p-nf', null, 'Không tìm thấy', [richText('c_notf01', '<p>Trang riêng không tồn tại</p>')], 'NotFound'),
  ],
});

type SiteData = Extract<SiteLoaderData, { kind: 'site' }>;

const LOADER_CLOCK = new Date('2026-12-31T16:59:59Z'); // server chạy loader
const CLIENT_CLOCK = new Date('2027-01-01T00:00:01Z'); // trình duyệt hydrate, 2 giây sau — đã sang năm

/** Loader data đúng như server trả (đã qua serialize → JSON thuần, nên clone bằng JSON). */
function loaderData(path: string): SiteData {
  const data: SiteData = {
    kind: SiteLoaderKind.Site,
    shop: { name: 'Spa ABC', logoUrl: '/media/s/abc/logo.png' },
    snapshot: SNAPSHOT as SiteData['snapshot'],
    publishedAt: '2026-12-31T20:00:00+00:00',
    path,
    year: LOADER_CLOCK.getUTCFullYear(),
    origin: 'https://spa-abc.vsite.vn',
  };
  return JSON.parse(JSON.stringify(data)) as SiteData;
}

let errorSpy: MockInstance<typeof console.error>;
let warnSpy: MockInstance<typeof console.warn>;
let container: HTMLDivElement;
let root: Root | undefined;

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] }); // chỉ giả Date — scheduler của React vẫn chạy bằng timer thật
  errorSpy = vi.spyOn(console, 'error');
  warnSpy = vi.spyOn(console, 'warn');
  container = document.createElement('div');
  document.body.appendChild(container);
});

afterEach(() => {
  act(() => root?.unmount());
  root = undefined;
  container.remove();
  vi.useRealTimers();
  vi.restoreAllMocks();
});

/** Server render (đồng hồ loader) → đổi đồng hồ sang client → hydrate cùng cây với loader data đã serialize. */
function ssrThenHydrate(data: SiteData) {
  vi.setSystemTime(LOADER_CLOCK);
  const serverHtml = renderToString(<SitePage data={data} />);
  container.innerHTML = serverHtml;
  const domBefore = container.innerHTML; // chuẩn hoá theo serializer của DOM để so sau hydrate

  vi.setSystemTime(CLIENT_CLOCK);
  const clientData = JSON.parse(JSON.stringify(data)) as SiteData;
  const recoverable: unknown[] = [];
  act(() => {
    root = hydrateRoot(container, <SitePage data={clientData} />, {
      onRecoverableError: (error) => recoverable.push(error),
    });
  });
  return { serverHtml, domBefore, recoverable };
}

describe('Hydrate Shop Site (T10, R8)', () => {
  it('trang chủ: hydrate không cảnh báo, DOM giữ nguyên HTML server', () => {
    const { serverHtml, domBefore, recoverable } = ssrThenHydrate(loaderData('/'));

    expect(serverHtml).toContain('Thư giãn tại Spa ABC');
    expect(serverHtml).toContain('/media/s/abc/hero_1600x900.jpg');
    expect(recoverable).toEqual([]);
    expect(errorSpy).not.toHaveBeenCalled();
    expect(warnSpy).not.toHaveBeenCalled();
    expect(container.innerHTML).toBe(domBefore); // hydrate không vá lại DOM
  });

  it('R8: loader 23:59:59 31/12/2026, client 00:00:01 2027 → © vẫn 2026, không lệch hydrate', () => {
    const { serverHtml, recoverable } = ssrThenHydrate(loaderData('/'));

    expect(new Date().getUTCFullYear()).toBe(2027); // đồng hồ client thật sự đã sang năm
    expect(serverHtml).toContain('© 2026 Spa ABC');
    expect(container.textContent).toContain('© 2026 Spa ABC');
    expect(container.textContent).not.toContain('© 2027');
    expect(recoverable).toEqual([]);
    expect(errorSpy).not.toHaveBeenCalled();
  });

  it('trang NotFound của shop (path không khớp): hydrate không cảnh báo', () => {
    const { serverHtml, recoverable } = ssrThenHydrate(loaderData('/khong-co'));

    expect(serverHtml).toContain('Trang riêng không tồn tại');
    expect(recoverable).toEqual([]);
    expect(errorSpy).not.toHaveBeenCalled();
  });
});
