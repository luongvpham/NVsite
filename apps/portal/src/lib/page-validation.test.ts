import { describe, expect, it } from 'vitest';
import {
  buildPageFormSchema,
  checkPageSlug,
  toCreatePageRequest,
  toUpdatePageRequest,
  type PageFormValues,
} from './page-validation';

const base: PageFormValues = {
  slug: 'dich-vu',
  title: 'Dịch vụ',
  seoTitle: '',
  seoDescription: '',
  ogImageId: null,
  noIndex: false,
  isEnabled: true,
};

describe('checkPageSlug — khớp SlugRules của BE (backend/.../SlugRules.cs)', () => {
  it('slug hợp lệ → null', () => {
    expect(checkPageSlug('dich-vu')).toBeNull();
    expect(checkPageSlug('a')).toBeNull();
    expect(checkPageSlug('bang-gia-2026')).toBeNull();
  });

  it('mã locale (vi, en) bị giữ chỗ, không phân biệt hoa/thường, và báo RESERVED trước INVALID', () => {
    expect(checkPageSlug('en')).toBe('PAGE_SLUG_RESERVED');
    expect(checkPageSlug('EN')).toBe('PAGE_SLUG_RESERVED');
    expect(checkPageSlug('vi')).toBe('PAGE_SLUG_RESERVED');
  });

  it('route của System page (kể cả loại chưa seed) bị giữ chỗ', () => {
    for (const slug of ['lien-he', 'san-pham', 'danh-muc', 'tim-kiem', 'dat-lich']) {
      expect(checkPageSlug(slug), slug).toBe('PAGE_SLUG_RESERVED');
    }
  });

  it('reservedShopSitePaths bị giữ chỗ, nhưng KHÔNG dùng reservedPaths (blog/about vẫn được)', () => {
    for (const slug of ['media', 'api', 'assets', 'static']) {
      expect(checkPageSlug(slug), slug).toBe('PAGE_SLUG_RESERVED');
    }
    expect(checkPageSlug('blog')).toBeNull();
    expect(checkPageSlug('about')).toBeNull();
  });

  it('sai khuôn → INVALID: hoa, dấu, gạch đầu/cuối/đôi, ký tự lạ, rỗng, dài quá 120', () => {
    for (const slug of ['Dich-Vu', 'dịch-vụ', '-a', 'a-', 'a--b', 'a b', 'a/b', 'a_b', '']) {
      expect(checkPageSlug(slug), JSON.stringify(slug)).toBe('PAGE_SLUG_INVALID');
    }
    expect(checkPageSlug('a'.repeat(120))).toBeNull();
    expect(checkPageSlug('a'.repeat(121))).toBe('PAGE_SLUG_INVALID');
  });
});

describe('buildPageFormSchema', () => {
  it('hợp lệ khi slug mở khoá', () => {
    expect(buildPageFormSchema({ slugLocked: false }).safeParse(base).success).toBe(true);
  });

  it('slug reserved/sai khuôn báo lỗi đúng ô slug', () => {
    const schema = buildPageFormSchema({ slugLocked: false });
    const reserved = schema.safeParse({ ...base, slug: 'lien-he' });
    expect(reserved.success).toBe(false);
    expect(reserved.error?.issues[0]?.path).toEqual(['slug']);
    const invalid = schema.safeParse({ ...base, slug: 'Dịch Vụ' });
    expect(invalid.error?.issues[0]?.path).toEqual(['slug']);
  });

  it('slug khoá (trang chủ/System) → không kiểm slug', () => {
    const schema = buildPageFormSchema({ slugLocked: true });
    expect(schema.safeParse({ ...base, slug: '' }).success).toBe(true);
    expect(schema.safeParse({ ...base, slug: 'lien-he' }).success).toBe(true);
  });

  it('title bắt buộc (trim) và ≤160; seoTitle ≤160; seoDescription ≤320', () => {
    const schema = buildPageFormSchema({ slugLocked: false });
    expect(schema.safeParse({ ...base, title: '   ' }).success).toBe(false);
    expect(schema.safeParse({ ...base, title: 'a'.repeat(161) }).success).toBe(false);
    expect(schema.safeParse({ ...base, title: 'a'.repeat(160) }).success).toBe(true);
    expect(schema.safeParse({ ...base, seoTitle: 'a'.repeat(161) }).success).toBe(false);
    expect(schema.safeParse({ ...base, seoDescription: 'a'.repeat(321) }).success).toBe(false);
    expect(schema.safeParse({ ...base, seoDescription: 'a'.repeat(320) }).success).toBe(true);
  });

  it('noIndex = false không hợp lệ cho NotFound (noIndexLocked)', () => {
    const schema = buildPageFormSchema({ slugLocked: true, noIndexLocked: true });
    expect(schema.safeParse({ ...base, noIndex: false }).success).toBe(false);
    expect(schema.safeParse({ ...base, noIndex: true }).success).toBe(true);
  });
});

describe('toCreatePageRequest / toUpdatePageRequest', () => {
  it('chuỗi SEO rỗng → null, trim title/slug', () => {
    expect(toCreatePageRequest({ ...base, title: '  Dịch vụ ', slug: 'dich-vu', seoTitle: '  ', seoDescription: 'mô tả' })).toEqual({
      slug: 'dich-vu',
      title: 'Dịch vụ',
      seoTitle: null,
      seoDescription: 'mô tả',
      ogImageId: null,
      noIndex: false,
      isEnabled: true,
    });
  });

  it('update: slug khoá → gửi slug = null (không đổi); mở khoá → gửi slug', () => {
    expect(toUpdatePageRequest({ ...base, slug: 'lien-he' }, { slugLocked: true }).slug).toBeNull();
    expect(toUpdatePageRequest(base, { slugLocked: false }).slug).toBe('dich-vu');
  });

  it('update thay toàn bộ: luôn gửi đủ các field', () => {
    const body = toUpdatePageRequest({ ...base, ogImageId: '11111111-1111-4111-8111-111111111111', noIndex: true }, { slugLocked: false });
    expect(Object.keys(body).sort()).toEqual(
      ['isEnabled', 'noIndex', 'ogImageId', 'seoDescription', 'seoTitle', 'slug', 'title'].sort(),
    );
  });
});
