import { describe, expect, it } from 'vitest';
import { reservedRoutes } from './reserved-routes';

/**
 * FE và BE phải đọc CÙNG MỘT file config/reserved-routes.json (Quyết định #24).
 * Test này khẳng định phía FE đọc đúng file và có cùng nội dung bắt buộc mà
 * backend/tests/ArchitectureTests/ReservedRoutesTests.cs cũng khẳng định phía BE.
 */
describe('reserved-routes.json (FE)', () => {
  it('has the entries required by Quyết định #24 (shop) and #25 (admin)', () => {
    expect(reservedRoutes.reservedPaths).toContain('shop');
    expect(reservedRoutes.reservedSubdomains).toContain('admin');
  });
});
