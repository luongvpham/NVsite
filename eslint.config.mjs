// @ts-check
import js from '@eslint/js';
import tseslint from 'typescript-eslint';

const APPS_IMPORT_PATTERNS = [
  {
    group: ['@vsite/portal', '@vsite/portal/*', '@vsite/web', '@vsite/web/*', 'apps/**', '**/apps/**'],
    message: 'packages/ không import từ apps/ — chiều phụ thuộc một hướng (CLAUDE.md root).',
  },
];

export default tseslint.config(
  {
    ignores: [
      '**/dist/**',
      '**/.output/**',
      '**/.turbo/**',
      '**/node_modules/**',
      'packages/api-sdk/src/**',
      'packages/builder-components/generated/**',
      'backend/**',
      'DesignIdeal/**',
      // Build-tool config files — không phải app logic, không cần type-aware lint đầy đủ,
      // và projectService không tự nhận các file này (không nằm trong "include" của tsconfig
      // package chứa nó) nên sẽ luôn báo "Parsing error" nếu không ignore ở đây.
      '**/vite.config.ts',
      '**/vitest.config.ts',
      '**/orval.config.ts',
      '**/tailwind-preset.js',
      // Script Node thuần (.mjs) của tooling package — không thuộc tsconfig nào (TOOLING-001).
      'packages/*/scripts/**/*.mjs',
    ],
  },
  js.configs.recommended,
  ...tseslint.configs.strictTypeChecked,
  {
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
      '@typescript-eslint/restrict-template-expressions': ['error', { allowNumber: true }],
    },
  },
  {
    // §9 "test rẻ mà đáng" — enforce isomorphic invariant (#23) bằng lint, không chỉ CLAUDE.md.
    // builder-components/builder-renderer phải chạy được server-side (SSR ở apps/web sau này);
    // window/document chỉ được dùng trong effect sau hydrate, không phải trong logic render chính.
    files: ['packages/builder-components/src/**/*.{ts,tsx}', 'packages/builder-renderer/src/**/*.{ts,tsx}'],
    ignores: ['**/*.test.{ts,tsx}'],
    rules: {
      'no-restricted-globals': ['error', { name: 'window', message: 'Isomorphic (#23) — không dùng window trong logic render chính.' }, { name: 'document', message: 'Isomorphic (#23) — không dùng document trong logic render chính.' }],
    },
  },
  {
    // Ranh giới kiến trúc: `builder-core` (Operations Engine + store) CHỈ dùng bởi apps/portal. builder-renderer phải chạy
    // ở apps/web (chỉ render), builder-components là model mà chính builder-core import (import ngược = vòng phụ thuộc).
    files: [
      'packages/builder-renderer/**/*.{ts,tsx}',
      'packages/builder-components/**/*.{ts,tsx}',
      'apps/web/**/*.{ts,tsx}',
    ],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['@vsite/builder-core', '@vsite/builder-core/*'],
              message: 'builder-core chỉ dùng bởi apps/portal — renderer/components/apps/web không được import (CLAUDE.md root, builder-renderer/CLAUDE.md).',
            },
            ...APPS_IMPORT_PATTERNS,
          ],
        },
      ],
    },
  },
  {
    // `packages/` không import từ `apps/` (CLAUDE.md root) — chiều phụ thuộc một hướng (WEBSITE-003 T3, nợ TOOLING-001).
    // Renderer/components đã có block riêng ở trên (kèm luật builder-core) nên loại ra để rule không bị ghi đè.
    files: ['packages/**/*.{ts,tsx}'],
    ignores: ['packages/builder-renderer/**', 'packages/builder-components/**'],
    rules: {
      'no-restricted-imports': ['error', { patterns: APPS_IMPORT_PATTERNS }],
    },
  },
);
