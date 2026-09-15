import '@testing-library/jest-dom/vitest';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { server } from './msw-server';

// jsdom không hiện thực window.scrollTo — TanStack Router gọi nó khi scroll-restoration chạy sau
// mỗi lần điều hướng, chỉ log noise, không phải lỗi test thật.
window.scrollTo = () => {};

beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' });
});
afterEach(() => {
  server.resetHandlers();
});
afterAll(() => {
  server.close();
});
