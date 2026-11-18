// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { isReservedShopSitePath, selectPage } from './select-page';

const pg = (id: string, slug: string | null, systemType: string | null = null) => ({ id, slug, systemType });
const HOME = pg('p-home', '');
const CONTACT = pg('p-contact', 'lien-he', 'Contact');
const NF = pg('p-nf', null, 'NotFound');
const snap = (...pages: ReturnType<typeof pg>[]) => ({ pages });
const full = snap(HOME, CONTACT, NF);

describe('selectPage — path lạ (T5)', () => {
  const sneaky = snap(HOME, pg('p-m', 'media'), pg('p-M', 'Media'), pg('p-pct', '50%'), NF);
  it.each(['/%256dedia/x', '/%2Fmedia', '/%252Fmedia', '/Media/x', '/MEDIA', '/Media', '/50%'])(
    '%s → notFound',
    (p) => {
      expect(selectPage(sneaky, p)).toEqual({ kind: 'notFound', page: NF });
    },
  );
});

describe('selectPage (A10, R6, R7)', () => {
  it('"/" → trang chủ slug ""', () => {
    expect(selectPage(full, '/')).toEqual({ kind: 'page', page: HOME });
  });
  it('khớp slug chính xác, kể cả có "/" cuối (A10)', () => {
    expect(selectPage(full, '/lien-he')).toEqual({ kind: 'page', page: CONTACT });
    expect(selectPage(full, '/lien-he/')).toEqual({ kind: 'page', page: CONTACT });
  });
  it('phân biệt hoa/thường', () => {
    expect(selectPage(full, '/Lien-He')).toEqual({ kind: 'notFound', page: NF });
  });
  it('decode %-escape', () => {
    const vi = pg('p-vi', 'lien-hệ');
    expect(selectPage(snap(HOME, vi), '/lien-h%E1%BB%87')).toEqual({ kind: 'page', page: vi });
  });
  it.each(['/media/x', '/api/x', '/assets', '/static/a.css', '/media', '/%6dedia/x'])(
    'reserved %s → notFound (R6)',
    (p) => {
      const sneaky = snap(HOME, pg('p-m', 'media'), pg('p-a', 'api'), pg('p-s', 'static'), pg('p-as', 'assets'), NF);
      expect(selectPage(sneaky, p).kind).toBe('notFound');
    },
  );
  it.each(['//', '///', '/a/b', '/lien-he//', '/%2e%2e', '/..', '/%E0%A4%A', '/a%2Fb', ''])(
    '%s → notFound, không ném',
    (p) => {
      expect(() => selectPage(full, p)).not.toThrow();
      expect(selectPage(full, p)).toEqual({ kind: 'notFound', page: NF });
    },
  );
  it('slug không có → notFound', () => {
    expect(selectPage(full, '/khong-co')).toEqual({ kind: 'notFound', page: NF });
  });
  it('R7: trang chủ vắng → "/" là notFound', () => {
    expect(selectPage(snap(CONTACT, NF), '/')).toEqual({ kind: 'notFound', page: NF });
  });
  it('không có trang NotFound → page null', () => {
    expect(selectPage(snap(HOME), '/x')).toEqual({ kind: 'notFound', page: null });
  });
  it('trang slug null (NotFound) không bao giờ khớp path', () => {
    expect(selectPage(full, '/null').kind).toBe('notFound');
  });
});

describe('isReservedShopSitePath (plan §2 Phát hiện 2)', () => {
  it.each(['/media/x', '/media', '/media/', '/api/x', '/assets/a.js', '/static/x', '/Media/x', '/MEDIA', '/%6dedia/x', '/%6Dedia'])(
    '%s → true',
    (p) => {
      expect(isReservedShopSitePath(p)).toBe(true);
    },
  );
  it.each(['/', '/lien-he', '/mediathek', '/media-kit', '/x/media', '//', '/%E0%A4%A', '', '/%2Fmedia'])('%s → false, không ném', (p) => {
    expect(isReservedShopSitePath(p)).toBe(false);
  });
});
