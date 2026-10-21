import { describe, expect, it } from 'vitest';
import { getErrorMessage } from './error-messages';

describe('getErrorMessage', () => {
  it('map SHOP_SLUG_ALREADY_TAKEN, SHOP_ACCESS_DENIED, SHOP_OWNER_REQUIRED thành 3 thông báo khác nhau', () => {
    const slugTaken = getErrorMessage('SHOP_SLUG_ALREADY_TAKEN');
    const accessDenied = getErrorMessage('SHOP_ACCESS_DENIED');
    const ownerRequired = getErrorMessage('SHOP_OWNER_REQUIRED');

    expect(slugTaken).not.toBe(accessDenied);
    expect(accessDenied).not.toBe(ownerRequired);
    expect(slugTaken).not.toBe(ownerRequired);
    expect(slugTaken).toMatch(/slug/i);
    expect(accessDenied).toMatch(/quyền truy cập/);
    expect(ownerRequired).toMatch(/chủ shop/);
  });

  it('mọi error_code Website trong WEBSITE-002/brief.md có thông báo riêng (không rơi về fallback)', () => {
    const websiteCodes = [
      'WEBSITE_OWNER_REQUIRED',
      'WEBSITE_NOT_FOUND',
      'WEBSITE_ALREADY_EXISTS',
      'WEBSITE_REQUIRES_HOSTED_SHOP',
      'SHOP_NOT_FOUND',
      'PAGE_NOT_FOUND',
      'PAGE_SLUG_TAKEN',
      'PAGE_SLUG_RESERVED',
      'PAGE_SLUG_INVALID',
      'PAGE_NOINDEX_REQUIRED',
      'PAGE_SYSTEM_SLUG_FIXED',
      'PAGE_HOME_SLUG_FIXED',
      'PAGE_NOT_DELETABLE',
      'CONCURRENCY_CONFLICT',
      'DRAFT_CONFLICT',
      'TREE_INVALID',
      'TREE_BODY_TOO_LARGE',
      'RICHTEXT_UNSAFE',
      'LINK_URL_UNSAFE',
      'MEDIA_INVALID_IMAGE_REFERENCE',
      'SCHEMA_VERSION_UNSUPPORTED',
      'THEME_INVALID',
      'THEME_BODY_TOO_LARGE',
    ];
    const fallback = getErrorMessage(undefined);
    for (const code of websiteCodes) {
      expect(getErrorMessage(code), code).not.toBe(fallback);
    }
  });

  it('trả fallback message cho error_code không nhận diện được hoặc undefined', () => {
    expect(getErrorMessage(undefined)).toBe('Có lỗi xảy ra, vui lòng thử lại.');
    expect(getErrorMessage('SOME_UNKNOWN_CODE')).toBe('Có lỗi xảy ra, vui lòng thử lại.');
  });
});
