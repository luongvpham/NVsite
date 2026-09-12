// Entry point riêng cho MSW handlers — không import từ index.ts chính để tránh kéo
// msw/@faker-js/faker vào bundle production (apps/web SSR, apps/portal build).
// Sample đã bị xoá (docs/tasks/CLEANUP-SAMPLE.md) — module thật đầu tiên (Identity, Bước 3) thêm
// lại `export * from './generated/{module}/...msw'` ở đây.
