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
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
  },
});
