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
  // Media (MEDIA-001 brief.md mục "Mã lỗi FE phải xử lý")
  MEDIA_FILE_TOO_LARGE: 'Ảnh vượt quá 10 MB, vui lòng chọn ảnh nhỏ hơn.',
  MEDIA_MULTIPART_REQUIRED: 'Không gửi được ảnh lên máy chủ, vui lòng thử lại.',
  MEDIA_HEIC_UNSUPPORTED: 'Định dạng HEIC chưa được hỗ trợ.',
  MEDIA_UNSUPPORTED_FORMAT: 'Định dạng ảnh không được hỗ trợ. Chỉ nhận JPEG, PNG, WEBP.',
  MEDIA_TOO_MANY_PIXELS: 'Ảnh có kích thước (số điểm ảnh) quá lớn.',
  MEDIA_CORRUPT_IMAGE: 'File ảnh bị lỗi, vui lòng thử ảnh khác.',
  MEDIA_FILE_MISSING: 'Không tìm thấy file để tải lên.',
  MEDIA_UNKNOWN_PRESET: 'Preset ảnh không hợp lệ.',
  MEDIA_OWNER_REQUIRED: 'Chỉ chủ shop (Owner) mới được thực hiện thao tác này.',
};

const FALLBACK_MESSAGE = 'Có lỗi xảy ra, vui lòng thử lại.';

/** Hướng dẫn riêng cho MEDIA_HEIC_UNSUPPORTED — chi tiết hơn message một dòng ở ERROR_MESSAGES,
 * hiện thêm bên dưới trong dialog upload (text cuối cùng để người duyệt Gate 2 chỉnh, theo
 * brief F2/F3 — `Docs/tasks/MEDIA-001/brief.md`). */
export const HEIC_UNSUPPORTED_GUIDANCE =
  'Trên iPhone: Cài đặt → Camera → Định dạng → Tương thích nhất, hoặc chọn ảnh qua nút Chọn ảnh';

export function getErrorMessage(errorCode: string | undefined): string {
  if (!errorCode) return FALLBACK_MESSAGE;
  return ERROR_MESSAGES[errorCode] ?? FALLBACK_MESSAGE;
}
