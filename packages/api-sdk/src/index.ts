export * from './mutator/axios-instance';

// Barrel sinh bởi `pnpm gen:api` (scripts/lib/barrel.mjs, TOOLING-004) — thêm module/tag mới không cần sửa file này.
// Model trùng giữa các module (ProblemDetails…) chỉ export một bản, nếu giống hệt nhau; khác nhau thì gen:api fail.
export * from './generated/index';
