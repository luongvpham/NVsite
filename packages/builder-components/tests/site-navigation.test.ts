import { describe, expect, it } from 'vitest';
import { createResolveUrl, resolveNavigation } from '../src/index';

const P1 = '11111111-1111-1111-1111-111111111111';
const P2 = '22222222-2222-2222-2222-222222222222';
const P3 = '33333333-3333-3333-3333-333333333333';
const PNF = '44444444-4444-4444-4444-444444444444';

const pages = [
  { id: P1, title: 'Trang chủ', slug: '', systemType: 'Home' },
  { id: P2, title: 'Dịch vụ', slug: 'dich-vu', systemType: null },
  { id: P3, title: 'Liên hệ', slug: 'lien-he', systemType: 'Contact' },
  { id: PNF, title: '404', slug: null, systemType: 'NotFound' },
];
const resolve = createResolveUrl({
  basePath: '',
  locale: 'vi',
  defaultLocale: 'vi',
  pages: pages.map((p) => ({ ...p, isEnabled: true })),
});
const run = (items: Parameters<typeof resolveNavigation>[0], r = resolve) => resolveNavigation(items, pages, r);

describe('resolveNavigation', () => {
  it('page: label null → title, label override, href qua resolveUrl, order giữ nguyên', () => {
    const out = run([
      { key: `page:${P2}`, label: null },
      { key: `page:${P1}`, label: 'Home' },
      { key: `page:${P3}`, label: null },
    ]);
    expect(out).toEqual([
      { label: 'Dịch vụ', href: '/dich-vu', external: false, children: [] },
      { label: 'Home', href: '/', external: false, children: [] },
      { label: 'Liên hệ', href: '/lien-he', external: false, children: [] },
    ]);
  });

  it('href đến từ createResolveUrl (tiền tố basePath/locale)', () => {
    const r = createResolveUrl({
      basePath: '/preview/x',
      locale: 'en',
      defaultLocale: 'vi',
      pages: pages.map((p) => ({ ...p, isEnabled: true })),
    });
    expect(run([{ key: `page:${P2}`, label: null }], r)[0]?.href).toBe('/preview/x/en/dich-vu');
  });

  it('so khớp page id không phân biệt hoa/thường', () => {
    const out = run([{ key: `page:${P2.toUpperCase()}`, label: null }]);
    expect(out[0]).toMatchObject({ label: 'Dịch vụ', href: '/dich-vu' });
  });

  it('R5: trang thiếu bị bỏ; trang NotFound (slug null) bị bỏ', () => {
    const out = run([
      { key: 'page:99999999-9999-9999-9999-999999999999', label: 'Ma' },
      { key: `page:${PNF}`, label: null },
      { key: `page:${P1}`, label: null },
    ]);
    expect(out.map((i) => i.label)).toEqual(['Trang chủ']);
  });

  it('R5: cha thiếu ⇒ bỏ cả nhóm kể cả cháu', () => {
    const out = run([
      {
        key: 'page:99999999-9999-9999-9999-999999999999',
        label: null,
        children: [{ key: `page:${P2}`, label: null, children: [{ key: `page:${P3}`, label: null }] }],
      },
      { key: `page:${P1}`, label: null },
    ]);
    expect(out).toHaveLength(1);
    expect(out[0]?.label).toBe('Trang chủ');
  });

  it('con thiếu chỉ bỏ con đó; 3 cấp được giữ', () => {
    const out = run([
      {
        key: `page:${P1}`,
        label: null,
        children: [
          { key: 'page:99999999-9999-9999-9999-999999999999', label: null },
          { key: `page:${P2}`, label: null, children: [{ key: `page:${P3}`, label: 'LH' }] },
        ],
      },
    ]);
    expect(out[0]?.children).toHaveLength(1);
    expect(out[0]?.children[0]?.children).toEqual([{ label: 'LH', href: '/lien-he', external: false, children: [] }]);
  });

  it('hidden ⇒ bỏ cùng cả cây con', () => {
    const out = run([
      { key: `page:${P1}`, label: null, hidden: true, children: [{ key: `page:${P2}`, label: null }] },
      { key: `page:${P3}`, label: null, children: [{ key: `page:${P2}`, label: null, hidden: true }] },
    ]);
    expect(out).toHaveLength(1);
    expect(out[0]?.children).toEqual([]);
  });

  describe('custom', () => {
    it('an toàn: http(s) ⇒ external, đường dẫn nội bộ / mailto ⇒ không external', () => {
      const out = run([
        { key: 'custom', label: 'A', url: 'https://vsite.vn/x' },
        { key: 'custom', label: 'B', url: '  HTTP://vsite.vn' },
        { key: 'custom', label: 'C', url: '/noi-bo' },
        { key: 'custom', label: 'D', url: 'mailto:a@b.vn' },
        { key: 'custom', label: 'E', url: '#neo' },
      ]);
      expect(out.map((i) => i.external)).toEqual([true, true, false, false, false]);
      expect(out[0]?.href).toBe('https://vsite.vn/x');
    });

    it.each(['javascript:alert(1)', '  javascript:alert(1)', 'java&#x09;script:alert(1)', '//evil.com', '', 'data:text/html,x'])(
      'R3: URL độc bị lọc lại: %j',
      (url) => {
        expect(run([{ key: 'custom', label: 'X', url }])).toEqual([]);
      },
    );

    it('thiếu url hoặc nhãn rỗng/null ⇒ bỏ', () => {
      expect(run([{ key: 'custom', label: 'X' }])).toEqual([]);
      expect(run([{ key: 'custom', label: null, url: '/a' }])).toEqual([]);
      expect(run([{ key: 'custom', label: '   ', url: '/a' }])).toEqual([]);
    });
  });

  it('key dạng khác ⇒ bỏ', () => {
    expect(run([{ key: 'product:1', label: 'x' }, { key: 'page:', label: 'x' }, { key: 'junk', label: 'x' }])).toEqual([]);
  });
});
