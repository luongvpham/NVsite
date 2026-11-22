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

  it('mã lỗi publish/rollback trong WEBSITE-004/brief.md §5, §7 có thông báo riêng', () => {
    const fallback = getErrorMessage(undefined);
    for (const code of [
      'PUBLISH_BLOCKED',
      'PUBLISH_CONFLICT',
      'ROLLBACK_CONFLICT',
      'PUBLICATION_NOT_FOUND',
      'REQUIRED_PROP_MISSING',
    ]) {
      expect(getErrorMessage(code), code).not.toBe(fallback);
    }
    expect(getErrorMessage('PUBLISH_CONFLICT')).not.toBe(getErrorMessage('ROLLBACK_CONFLICT'));
  });

  it('mã lỗi menu trong WEBSITE-007/brief.md (F4) có thông báo riêng', () => {
    const fallback = getErrorMessage(undefined);
    for (const code of [
      'NAVIGATION_INVALID',
      'NAVIGATION_CONFLICT',
      'NAVIGATION_BODY_INVALID',
      'NAVIGATION_BODY_TOO_LARGE',
      'NAV_PAGE_NOT_FOUND',
      'NAV_PAGE_DUPLICATE',
      'NAV_PAGE_NOT_ALLOWED',
      'NAV_URL_UNSAFE',
      'NAV_LABEL_REQUIRED',
      'NAV_LABEL_TOO_LONG',
      'NAV_TOO_MANY_ITEMS',
    ]) {
      expect(getErrorMessage(code), code).not.toBe(fallback);
    }
  });

  it('trả fallback message cho error_code không nhận diện được hoặc undefined', () => {
    expect(getErrorMessage(undefined)).toBe('Có lỗi xảy ra, vui lòng thử lại.');
    expect(getErrorMessage('SOME_UNKNOWN_CODE')).toBe('Có lỗi xảy ra, vui lòng thử lại.');
  });
});
