import {
  getErrorCode,
  getProblemParams,
  isDraftConflictParams,
  isProblemDetails,
  isTreeValidationParams,
  type DraftConflictParams,
  type TreeValidationParams,
} from '@vsite/shared';

/** Kết cục của một lần `PUT draft` thất bại — mỗi loại có cách xử lý riêng (Flow 5). */
export type SaveFailure =
  /** 409: bản khác đã lưu trước. Dừng autosave, hỏi người dùng. `params` có thể thiếu nếu BE đổi hình dạng — dialog vẫn hiện. */
  | { kind: 'conflict'; params: DraftConflictParams | undefined }
  /** 422: cây sai cấu trúc/luật. `params` thiếu → chỉ hiện banner theo mã. */
  | { kind: 'invalid'; code: string; params: TreeValidationParams | undefined }
  /** 413 (hoặc mã `TREE_BODY_TOO_LARGE`): quá lớn, gửi lại cũng vô ích cho tới khi cây nhỏ đi. */
  | { kind: 'too-large' }
  /** Mạng đứt, 5xx, 401 sau refresh hỏng, lỗi lạ: giữ "Chưa lưu", thử lại khi đổi tiếp hoặc bấm Thử lại. */
  | { kind: 'transient' };

/** Phân loại lỗi mà `savePageDraft` ném (mutator ném thẳng ProblemDetails của BE, hoặc lỗi gốc khi không có response). */
export function classifySaveError(error: unknown): SaveFailure {
  const code = getErrorCode(error);
  const status = isProblemDetails(error) ? error.status : undefined;

  if (code === 'DRAFT_CONFLICT' || status === 409) {
    return { kind: 'conflict', params: getProblemParams(error, isDraftConflictParams) };
  }
  if (code === 'TREE_BODY_TOO_LARGE' || status === 413) return { kind: 'too-large' };
  if (status === 422 && code) {
    return { kind: 'invalid', code, params: getProblemParams(error, isTreeValidationParams) };
  }
  // 422 mang mã tập tin cây nhưng thiếu status (ProblemDetails tối thiểu) vẫn là lỗi nội dung, không phải lỗi tạm.
  if (code === 'TREE_INVALID' || code === 'RICHTEXT_UNSAFE' || code === 'LINK_URL_UNSAFE' || code === 'SCHEMA_VERSION_UNSUPPORTED') {
    return { kind: 'invalid', code, params: getProblemParams(error, isTreeValidationParams) };
  }
  return { kind: 'transient' };
}
