import { tanstackStart } from '@tanstack/react-start/plugin/vite';
import tailwindcss from '@tailwindcss/vite';
import viteReact from '@vitejs/plugin-react';
import { nitro } from 'nitro/vite';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [
    tanstackStart(), // MUST come before react()
    nitro(),
    viteReact(),
    tailwindcss(),
  ],
  server: {
    // Website shop dev chạy theo Host `{slug}.vsite.local:3000` (hosts file, xem
    // backend/docs/modules/identity.md). Vite mặc định CHẶN mọi hostname lạ bằng 403
    // ("Blocked request. This host is not allowed"), nên phải cho phép cả hậu tố này — nếu không
    // /media/… và mọi trang shop đều không mở được ở domain shop khi dev.
    allowedHosts: ['.vsite.local'],
    proxy: {
      // Ảnh không đi qua API — phục vụ tại /media/{storageKey} trên mọi domain, kể cả
      // {slug}.vsite.local:3000 (brief MEDIA-001). changeOrigin: false — giữ nguyên Host header
      // gốc (cùng cơ chế như apps/portal/vite.config.ts).
      // Dùng `server.proxy` của Vite, KHÔNG dùng `nitro({ devProxy })`: đã thử chạy thật, với
      // TanStack Start + nitro/vite thì devProxy không chặn được request — /media/… bị SSR của app
      // trả 404 HTML thay vì tới API.
      '/media': { target: 'http://localhost:5270', changeOrigin: false },
      // API (Quyết định #91) — gọi từ trình duyệt sau hydrate. SSR loader KHÔNG đi qua proxy này:
      // ở server dùng createApiClient({ baseURL tuyệt đối, headers: Host gốc }) của @vsite/api-sdk.
      '/api': { target: 'http://localhost:5270', changeOrigin: false },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
  },
});
