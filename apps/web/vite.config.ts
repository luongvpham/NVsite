import { tanstackStart } from '@tanstack/react-start/plugin/vite';
import tailwindcss from '@tailwindcss/vite';
import viteReact from '@vitejs/plugin-react';
import { nitro } from 'nitro/vite';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [
    tanstackStart(), // MUST come before react()
    nitro({
      devProxy: {
        // Ảnh không đi qua API — phục vụ tại /media/{storageKey} trên mọi domain, kể cả
        // {slug}.vsite.local:3000 (brief MEDIA-001). changeOrigin: false — giữ nguyên Host header
        // gốc, đúng cơ chế Host-based tenant resolve dùng chung toàn hệ (xem apps/portal/vite.config.ts).
        '/media': { target: 'http://localhost:5270', changeOrigin: false },
      },
    }),
    viteReact(),
    tailwindcss(),
  ],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
  },
});
