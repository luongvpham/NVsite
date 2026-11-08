import { defineConfig } from 'vitest/config';

// Engine thuần TS — chạy ở Node, không cần jsdom (kiểm "không đụng window/document").
export default defineConfig({
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
});
