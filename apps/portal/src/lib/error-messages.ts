/**
 * Map error_code (ProblemDetails) -> thông báo tiếng Việt cho người dùng cuối.
 * Nguồn error_code: Docs/tasks/SHOP-001/brief.md + Docs/tasks/IDENTITY-001/brief.md mục "Ràng buộc".
 */
const ERROR_MESSAGES: Record<string, string> = {
  // Identity
  EMAIL_ALREADY_REGISTERED: 'Email này đã được đăng ký.',
  EMAIL_ALREADY_REGISTERED_AT_SHOP: 'Email này đã được đăng ký cho shop này.',
  VERIFY_TOKEN_INVALID: 'Link xác minh không hợp lệ hoặc đã hết hạn.',
  RESET_TOKEN_INVALID: 'Link đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.',
  UNAUTHORIZED: 'Email hoặc mật khẩu không đúng.',
  // Shop
  SHOP_SLUG_ALREADY_TAKEN: 'Đường dẫn (slug) này đã được sử dụng, hãy chọn đường dẫn khác.',
  INSUFFICIENT_SCOPE: 'Bạn không có quyền thực hiện thao tác này.',
  SHOP_ACCESS_DENIED: 'Bạn không có quyền truy cập shop này.',
  SHOP_OWNER_REQUIRED: 'Chỉ chủ shop mới được sửa thông tin này.',
  // Chung
  VALIDATION_ERROR: 'Dữ liệu chưa hợp lệ, vui lòng kiểm tra lại.',
};

const FALLBACK_MESSAGE = 'Có lỗi xảy ra, vui lòng thử lại.';

export function getErrorMessage(errorCode: string | undefined): string {
  if (!errorCode) return FALLBACK_MESSAGE;
  return ERROR_MESSAGES[errorCode] ?? FALLBACK_MESSAGE;
}
