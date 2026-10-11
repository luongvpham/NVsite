import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    // Node, không jsdom: client của createApiClient chạy ở server (SSR) — test đúng môi trường đó.
    environment: 'node',
    include: ['tests/**/*.test.ts'],
  },
});
