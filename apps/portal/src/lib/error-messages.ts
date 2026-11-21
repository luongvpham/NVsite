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
  MEDIA_INVALID_IMAGE_REFERENCE: 'Có ảnh không còn hợp lệ (đã xoá hoặc không thuộc shop này), vui lòng chọn lại ảnh.',
  // Website (WEBSITE-002/brief.md — bảng endpoint + "Quy ước FE phải biết")
  WEBSITE_OWNER_REQUIRED: 'Chỉ chủ shop (Owner) mới được quản lý website.',
  SHOP_NOT_FOUND: 'Không tìm thấy shop.',
  WEBSITE_NOT_FOUND: 'Shop này chưa có website.',
  WEBSITE_ALREADY_EXISTS: 'Shop này đã có website.',
  WEBSITE_REQUIRES_HOSTED_SHOP:
    'Chỉ shop loại "Website trên vsite" mới tạo được website. Đổi loại shop ở màn Sửa shop rồi thử lại.',
  PAGE_NOT_FOUND: 'Không tìm thấy trang (có thể đã bị xoá).',
  PAGE_SLUG_TAKEN: 'Đường dẫn này đã có trang khác dùng.',
  PAGE_SLUG_RESERVED: 'Đường dẫn này được hệ thống dành riêng, hãy chọn đường dẫn khác.',
  PAGE_SLUG_INVALID: 'Đường dẫn không hợp lệ: chỉ dùng chữ thường không dấu, số và dấu gạch ngang (vd. gioi-thieu).',
  PAGE_NOINDEX_REQUIRED: 'Trang "Không tìm thấy" luôn phải ẩn khỏi công cụ tìm kiếm.',
  PAGE_SYSTEM_SLUG_FIXED: 'Không đổi được đường dẫn của trang hệ thống.',
  PAGE_HOME_SLUG_FIXED: 'Không đổi được đường dẫn của trang chủ.',
  PAGE_NOT_DELETABLE: 'Trang này không xoá được.',
  CONCURRENCY_CONFLICT: 'Dữ liệu vừa được thay đổi ở nơi khác. Tải lại rồi thử lại.',
  DRAFT_CONFLICT: 'Trang vừa được lưu ở tab hoặc thiết bị khác.',
  TREE_INVALID: 'Nội dung trang có chỗ chưa hợp lệ.',
  TREE_BODY_TOO_LARGE: 'Trang quá lớn để lưu. Bớt khối hoặc ảnh rồi thử lại.',
  RICHTEXT_UNSAFE: 'Đoạn văn bản có nội dung không an toàn (script, nhúng…) và bị từ chối.',
  LINK_URL_UNSAFE: 'Liên kết không an toàn. Chỉ nhận https, http, mailto, tel, đường dẫn nội bộ hoặc #neo.',
  SCHEMA_VERSION_UNSUPPORTED: 'Phiên bản dữ liệu trang không được hỗ trợ. Tải lại trang.',
  THEME_INVALID: 'Giao diện có giá trị chưa hợp lệ.',
  THEME_BODY_TOO_LARGE: 'Cấu hình giao diện quá lớn để lưu.',
  // Publish / rollback (WEBSITE-004/brief.md §5, §7)
  PUBLISH_BLOCKED: 'Chưa xuất bản được: còn trang có lỗi cần sửa trước.',
  PUBLISH_CONFLICT: 'Có thao tác khác vừa chạy trên website (xuất bản, quay về bản cũ hoặc lưu trang). Bấm Xuất bản lại.',
  ROLLBACK_CONFLICT: 'Có thao tác khác vừa chạy trên website. Danh sách bản đã được tải lại, hãy bấm lại.',
  PUBLICATION_NOT_FOUND: 'Không tìm thấy bản xuất bản này (có thể đã bị xoá). Danh sách bản đã được tải lại.',
  REQUIRED_PROP_MISSING: 'Khối còn thiếu nội dung bắt buộc.',
  // Menu website (WEBSITE-007/brief.md "Màn Menu (F4)" — bảng mã lỗi)
  NAVIGATION_INVALID: 'Menu còn chỗ chưa hợp lệ — xem các dòng được tô đỏ.',
  NAVIGATION_CONFLICT: 'Menu vừa được sửa ở nơi khác. Đã tải lại bản mới nhất.',
  NAVIGATION_BODY_INVALID: 'Không lưu được menu do dữ liệu gửi lên sai cấu trúc. Tải lại trang rồi thử lại.',
  NAVIGATION_BODY_TOO_LARGE: 'Menu quá lớn để lưu. Bớt mục rồi thử lại.',
  NAV_PAGE_NOT_FOUND: 'Trang này không còn tồn tại — xoá mục khỏi menu.',
  NAV_PAGE_DUPLICATE: 'Trang này đã có trong menu (mỗi trang chỉ xuất hiện một lần).',
  NAV_PAGE_NOT_ALLOWED: 'Trang "Không tìm thấy" không đưa vào menu được.',
  NAV_URL_UNSAFE: 'Địa chỉ liên kết không hợp lệ. Chỉ nhận https, http, mailto, tel, đường dẫn nội bộ hoặc #neo.',
  NAV_LABEL_REQUIRED: 'Liên kết cần có nhãn.',
  NAV_LABEL_TOO_LONG: 'Nhãn tối đa 60 ký tự.',
  NAV_TOO_MANY_ITEMS: 'Quá nhiều mục: tối đa 12 mục cấp 1 và 10 mục mỗi nhóm con.',
  NAV_TOO_DEEP: 'Menu tối đa 3 cấp.',
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
