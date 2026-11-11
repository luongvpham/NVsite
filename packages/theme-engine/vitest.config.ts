import { defineConfig } from 'vitest/config';

// Hàm thuần, isomorphic (#23) — chạy ở Node để chứng minh không đụng window/document.
export default defineConfig({
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
});
