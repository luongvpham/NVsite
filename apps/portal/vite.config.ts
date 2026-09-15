import { readFileSync } from 'node:fs';
import path from 'node:path';
import { tanstackRouter } from '@tanstack/router-plugin/vite';
import tailwindcss from '@tailwindcss/vite';
import viteReact from '@vitejs/plugin-react';
import { defineConfig, type Plugin } from 'vite';

/**
 * TẠM THỜI — chỉ phục vụ /_dev/dev-hero-preview (xoá cùng lúc ở Sub-phase B 2.9).
 * ctx.resolveImage() ở Bước 2 là stub trả về URL `/_dev/placeholder/{preset}.svg` (07 §7.1) —
 * middleware này sinh SVG placeholder đúng kích thước preset để xem được bằng mắt lúc dev,
 * KHÔNG phải image proxy thật (đó là Bước 4, không đổi chữ ký resolveImage).
 */
function devPlaceholderImagePlugin(): Plugin {
  const presets = JSON.parse(
    readFileSync(path.resolve(import.meta.dirname, '..', '..', 'config', 'image-presets.json'), 'utf8'),
  ) as Record<string, { width: number | null; height: number | null }>;

  return {
    name: 'dev-placeholder-image',
    configureServer(server) {
      server.middlewares.use((req, res, next) => {
        const match = req.url?.match(/^\/_dev\/placeholder\/(.+)\.svg$/);
        if (!match) return next();

        const preset = decodeURIComponent(match[1]!);
        const dims = presets[preset];
        const width = dims?.width ?? 800;
        const height = dims?.height ?? 600;

        const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">
  <rect width="100%" height="100%" fill="#cbd5e1"/>
  <text x="50%" y="50%" dominant-baseline="middle" text-anchor="middle" fill="#475569" font-family="sans-serif" font-size="24">${preset} (${width}×${height})</text>
</svg>`;

        res.setHeader('Content-Type', 'image/svg+xml');
        res.end(svg);
      });
    },
  };
}

export default defineConfig({
  plugins: [
    tanstackRouter({ target: 'react', autoCodeSplitting: true }),
    viteReact(),
    tailwindcss(),
    devPlaceholderImagePlugin(),
  ],
  server: {
    // Portal phải phục vụ tại host nhãn `admin` — nếu không TenantResolutionMiddleware resolve
    // audience thành `vsite-main` thay vì `vsite-portal` (Docs/tasks/SHOP-001/brief.md).
    // Cần thêm `127.0.0.1  admin.vsite.local` vào hosts file trước khi `pnpm dev`.
    host: 'admin.vsite.local',
    proxy: {
      // changeOrigin: false — giữ nguyên Host header gốc (admin.vsite.local) khi proxy tới BE,
      // đúng cơ chế Host-based tenant resolve dùng chung toàn hệ. changeOrigin: true sẽ ghi đè
      // Host thành target (localhost:5270) và làm audience sai.
      '/auth': { target: 'http://localhost:5270', changeOrigin: false },
      '/shops': { target: 'http://localhost:5270', changeOrigin: false },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
  },
});
