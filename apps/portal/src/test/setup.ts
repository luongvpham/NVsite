import '@testing-library/jest-dom/vitest';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { server } from './msw-server';

// jsdom không hiện thực window.scrollTo — TanStack Router gọi nó khi scroll-restoration chạy sau
// mỗi lần điều hướng, chỉ log noise, không phải lỗi test thật.
window.scrollTo = () => {};

// jsdom không hiện thực createObjectURL/revokeObjectURL — dialog upload-to-slot dùng nó để xem
// trước ảnh vừa chọn trước khi gửi (MEDIA-001 F2).
URL.createObjectURL = () => 'blob:mock-preview';
URL.revokeObjectURL = () => {};

beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' });
});
afterEach(() => {
  server.resetHandlers();
});
afterAll(() => {
  server.close();
});
