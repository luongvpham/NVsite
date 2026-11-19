// Entry point riêng cho MSW handlers — không import từ index.ts chính để tránh kéo
// msw/@faker-js/faker vào bundle production (apps/web SSR, apps/portal build).
// Danh sách handler sinh bởi `pnpm gen:api` (scripts/lib/barrel.mjs, TOOLING-004).
export * from './generated/mocks';
