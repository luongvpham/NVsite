// @ts-check
import js from '@eslint/js';
import tseslint from 'typescript-eslint';

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
);
