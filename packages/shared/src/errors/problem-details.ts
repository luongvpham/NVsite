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
};

export function isProblemDetails(value: unknown): value is ProblemDetails {
  return typeof value === 'object' && value !== null && 'error_code' in value;
}

export function getErrorCode(value: unknown): string | undefined {
  return isProblemDetails(value) ? value.error_code : undefined;
}
