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

  it('trả fallback message cho error_code không nhận diện được hoặc undefined', () => {
    expect(getErrorMessage(undefined)).toBe('Có lỗi xảy ra, vui lòng thử lại.');
    expect(getErrorMessage('SOME_UNKNOWN_CODE')).toBe('Có lỗi xảy ra, vui lòng thử lại.');
  });
});
