/**
 * Lỗi từ BE luôn là ProblemDetails (RFC 7807) có error_code (Quyết định #19).
 * FE map error_code -> message hiển thị, không parse text tự do.
 */
export type ProblemDetails = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  error_code?: string;
  errors?: Record<string, string[]>;
  /**
   * Dữ liệu có cấu trúc đi kèm một số mã lỗi (BE `AppException.ErrorParams`). Hình dạng phụ thuộc `error_code`
   * nên để `unknown` — đọc qua `getProblemParams` với type guard, đừng cast.
   */
  params?: unknown;
};

export function isProblemDetails(value: unknown): value is ProblemDetails {
  return typeof value === 'object' && value !== null && 'error_code' in value;
}

export function getErrorCode(value: unknown): string | undefined {
  return isProblemDetails(value) ? value.error_code : undefined;
}

/** Một lỗi cấu trúc trong tài liệu JSON (cây/theme): `path` là JSON Pointer ('' = gốc/vỏ), `code` là mã con (vd. `PROP_INVALID`). */
export type TreeValidationError = {
  path: string;
  code: string;
  message: string;
};

/** `params` của 422 `TREE_INVALID` / `THEME_INVALID` / `SCHEMA_VERSION_UNSUPPORTED`. BE trả tối đa 50 lỗi; `truncated` = còn nữa. */
export type TreeValidationParams = {
  errors: TreeValidationError[];
  truncated: boolean;
};

/** `params` của 409 `DRAFT_CONFLICT`. Không kèm tree — muốn hoà giải thì `GET draft`. */
export type DraftConflictParams = {
  currentVersion: number;
  updatedAt: string;
  updatedByUserId: string | null;
};

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null;

function isTreeValidationError(value: unknown): value is TreeValidationError {
  return (
    isRecord(value) &&
    typeof value.path === 'string' &&
    typeof value.code === 'string' &&
    typeof value.message === 'string'
  );
}

export function isTreeValidationParams(value: unknown): value is TreeValidationParams {
  return (
    isRecord(value) &&
    Array.isArray(value.errors) &&
    value.errors.every(isTreeValidationError) &&
    typeof value.truncated === 'boolean'
  );
}

export function isDraftConflictParams(value: unknown): value is DraftConflictParams {
  return (
    isRecord(value) &&
    typeof value.currentVersion === 'number' &&
    typeof value.updatedAt === 'string' &&
    (value.updatedByUserId === null || typeof value.updatedByUserId === 'string')
  );
}

/**
 * Đọc `params` của ProblemDetails theo type guard; `undefined` nếu không phải ProblemDetails, thiếu `params`
 * hoặc sai hình dạng (không cast mù — nội dung `params` do BE quyết định theo `error_code`).
 */
export function getProblemParams<T>(value: unknown, guard: (params: unknown) => params is T): T | undefined {
  if (!isRecord(value)) return undefined;
  const params = value.params;
  return guard(params) ? params : undefined;
}
