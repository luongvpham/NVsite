export type OperationResult = { ok: true } | { ok: false; code: string; message: string };

/**
 * Mã lỗi của Engine. **Dùng ĐÚNG tên mã BE** (`packages/builder-components/tests/fixtures/tree/README.md`) để một bảng
 * thông báo lỗi dùng cho cả hai nguồn; mã chỉ-FE (không bao giờ gửi/nhận từ BE) ghi rõ bên dưới.
 */
export const OperationErrorCode = {
  // ── Chỉ FE ──
  /** Op sai hình dạng (không qua `operationSchema`). */
  InvalidOperation: 'INVALID_OPERATION',
  /** `nodeId`/`parentId`/`toParentId` không có trong cây. */
  TargetNotFound: 'TARGET_NOT_FOUND',
  /** `index`/`toIndex` ngoài khoảng cho phép. */
  IndexOutOfRange: 'INDEX_OUT_OF_RANGE',
  /** Thả một node vào chính nó hoặc hậu duệ của nó. */
  MoveIntoDescendant: 'MOVE_INTO_DESCENDANT',
  /** Store chưa nạp draft. */
  NoDraft: 'NO_DRAFT',
  // ── Trùng mã BE ──
  NodeTypeUnknown: 'NODE_TYPE_UNKNOWN',
  NodeVariantUnknown: 'NODE_VARIANT_UNKNOWN',
  NodeIdDuplicate: 'NODE_ID_DUPLICATE',
  NodeNotAllowedInPageKind: 'NODE_NOT_ALLOWED_IN_PAGE_KIND',
  NodeChildrenNotAccepted: 'NODE_CHILDREN_NOT_ACCEPTED',
  NodeChildTypeNotAllowed: 'NODE_CHILD_TYPE_NOT_ALLOWED',
  NodeMaxPerPageExceeded: 'NODE_MAX_PER_PAGE_EXCEEDED',
  PropUnknown: 'PROP_UNKNOWN',
  PropInvalid: 'PROP_INVALID',
  I18nNotEnabled: 'I18N_NOT_ENABLED',
  BindingSourceNotAllowed: 'BINDING_SOURCE_NOT_ALLOWED',
  LinkUrlUnsafe: 'LINK_URL_UNSAFE',
  SystemPageStructureChanged: 'SYSTEM_PAGE_STRUCTURE_CHANGED',
  SystemPagePropNotEditable: 'SYSTEM_PAGE_PROP_NOT_EDITABLE',
  TreeTooDeep: 'TREE_TOO_DEEP',
  TreeTooManyNodes: 'TREE_TOO_MANY_NODES',
  ThemeTokenInvalid: 'THEME_TOKEN_INVALID',
  ThemeBodyTooLarge: 'THEME_BODY_TOO_LARGE',
} as const;
export type OperationErrorCode = (typeof OperationErrorCode)[keyof typeof OperationErrorCode];

/** Thông báo cho người dùng cuối (tiếng Việt). `detail` chen thêm chi tiết khi có (tên type, prop…). */
const MESSAGES: Record<OperationErrorCode, string> = {
  INVALID_OPERATION: 'Thao tác không hợp lệ.',
  TARGET_NOT_FOUND: 'Không tìm thấy khối cần thao tác (có thể đã bị xoá).',
  INDEX_OUT_OF_RANGE: 'Vị trí đặt khối không hợp lệ.',
  MOVE_INTO_DESCENDANT: 'Không thể đặt một khối vào bên trong chính nó.',
  NO_DRAFT: 'Chưa mở bản nháp của trang.',
  NODE_TYPE_UNKNOWN: 'Loại khối không tồn tại.',
  NODE_VARIANT_UNKNOWN: 'Kiểu hiển thị của khối không tồn tại.',
  NODE_ID_DUPLICATE: 'Mã khối bị trùng trong trang.',
  NODE_NOT_ALLOWED_IN_PAGE_KIND: 'Loại khối này không dùng được cho loại trang hiện tại.',
  NODE_CHILDREN_NOT_ACCEPTED: 'Khối này không chứa được khối con.',
  NODE_CHILD_TYPE_NOT_ALLOWED: 'Khối này không nhận loại khối con đó.',
  NODE_MAX_PER_PAGE_EXCEEDED: 'Loại khối này chỉ được dùng tối đa một lần mỗi trang.',
  PROP_UNKNOWN: 'Thuộc tính không tồn tại với khối này.',
  PROP_INVALID: 'Giá trị thuộc tính không hợp lệ.',
  I18N_NOT_ENABLED: 'Đa ngôn ngữ chưa được bật.',
  BINDING_SOURCE_NOT_ALLOWED: 'Nguồn dữ liệu này không được phép dùng cho khối.',
  LINK_URL_UNSAFE: 'Liên kết không an toàn. Chỉ nhận https, http, mailto, tel, đường dẫn nội bộ hoặc #neo.',
  SYSTEM_PAGE_STRUCTURE_CHANGED: 'Trang hệ thống có cấu trúc cố định: không thêm, xoá hay di chuyển khối.',
  SYSTEM_PAGE_PROP_NOT_EDITABLE: 'Thuộc tính này không sửa được trên trang hệ thống.',
  TREE_TOO_DEEP: 'Trang lồng khối quá sâu.',
  TREE_TOO_MANY_NODES: 'Trang có quá nhiều khối.',
  THEME_TOKEN_INVALID: 'Giá trị giao diện không hợp lệ.',
  THEME_BODY_TOO_LARGE: 'Cấu hình giao diện quá lớn.',
};

export function fail(code: OperationErrorCode, detail?: string): OperationResult & { ok: false } {
  return { ok: false, code, message: detail ? `${MESSAGES[code]} (${detail})` : MESSAGES[code] };
}

export const OK: OperationResult & { ok: true } = { ok: true };
