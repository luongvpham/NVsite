// Entry point riêng cho MSW handlers — không import từ index.ts chính để tránh kéo
// msw/@faker-js/faker vào bundle production (apps/web SSR, apps/portal build).
export * from './generated/sample/sample/sample.msw';
