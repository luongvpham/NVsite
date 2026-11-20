// @ts-check
import js from '@eslint/js';
import tseslint from 'typescript-eslint';

const BUILDER_CORE_IMPORT_PATTERN = {
  group: ['@vsite/builder-core', '@vsite/builder-core/*'],
  message: 'builder-core chỉ dùng bởi apps/portal — renderer/components/apps/web không được import (CLAUDE.md root, builder-renderer/CLAUDE.md).',
};

// apps/web chỉ đọc site ĐÃ publish (`GET /api/site`, 05 §7, #108) — không bao giờ đọc/ghi draft (website, trang,
// theme, publication) của module website. Danh sách = mọi export runtime của generated/website/website/website.ts
// + schema Zod của các endpoint đó trong website.zod.ts + mock MSW của website.msw.ts (review I2, WEBSITE-005).
// Được phép: createApiClient, getPublishedSite*, getPublishedSiteResponse, type PublishedSite*/PublishedShopDto, site.msw.
// Orval sinh endpoint website mới → thêm tên vào đây.
const WEBSITE_DRAFT_SDK_NAMES = [
  // website.ts — hàm gọi API
  'createWebsite', 'getWebsite', 'listPages', 'createPage', 'updatePage', 'deletePage', 'getPageDraft', 'savePageDraft',
  'getTheme', 'updateTheme', 'publishWebsite', 'listPublications', 'rollbackPublication',
  // website.ts — hook React Query
  'useCreateWebsite', 'useGetWebsite', 'useListPages', 'useCreatePage', 'useUpdatePage', 'useDeletePage', 'useGetPageDraft',
  'useSavePageDraft', 'useGetTheme', 'useUpdateTheme', 'usePublishWebsite', 'useListPublications', 'useRollbackPublication',
  // website.ts — query key/options, mutation options
  'getGetWebsiteQueryKey', 'getGetWebsiteQueryOptions', 'getListPagesQueryKey', 'getListPagesQueryOptions',
  'getGetPageDraftQueryKey', 'getGetPageDraftQueryOptions', 'getGetThemeQueryKey', 'getGetThemeQueryOptions',
  'getListPublicationsQueryKey', 'getListPublicationsQueryOptions', 'getCreateWebsiteMutationOptions',
  'getCreatePageMutationOptions', 'getUpdatePageMutationOptions', 'getDeletePageMutationOptions',
  'getSavePageDraftMutationOptions', 'getUpdateThemeMutationOptions', 'getPublishWebsiteMutationOptions',
  'getRollbackPublicationMutationOptions',
  // website.zod.ts — schema của các endpoint draft
  'createWebsiteParams', 'getWebsiteParams', 'getWebsiteResponse', 'listPagesParams', 'listPagesResponseItem',
  'listPagesResponse', 'createPageParams', 'createPageBodyNoIndexDefault', 'createPageBodyIsEnabledDefault', 'createPageBody', 'updatePageParams',
  'updatePageBody', 'updatePageResponse', 'deletePageParams', 'getPageDraftParams', 'getPageDraftResponse',
  'savePageDraftParams', 'savePageDraftBody', 'savePageDraftResponse', 'getThemeParams', 'getThemeResponse',
  'updateThemeParams', 'updateThemeBody', 'updateThemeResponse', 'publishWebsiteParams', 'publishWebsiteBody',
  'listPublicationsParams', 'listPublicationsResponseItem', 'listPublicationsResponse', 'rollbackPublicationParams',
  'rollbackPublicationResponse',
];
const WEBSITE_DRAFT_MSW_NAMES = [
  'getWebsiteMock', 'getCreateWebsiteResponseMock', 'getGetWebsiteResponseMock', 'getListPagesResponseMock',
  'getCreatePageResponseMock', 'getUpdatePageResponseMock', 'getGetPageDraftResponseMock', 'getSavePageDraftResponseMock',
  'getGetThemeResponseMock', 'getUpdateThemeResponseMock', 'getPublishWebsiteResponseMock',
  'getListPublicationsResponseMock', 'getRollbackPublicationResponseMock', 'getCreateWebsiteMockHandler',
  'getGetWebsiteMockHandler', 'getListPagesMockHandler', 'getCreatePageMockHandler', 'getUpdatePageMockHandler',
  'getDeletePageMockHandler', 'getGetPageDraftMockHandler', 'getSavePageDraftMockHandler', 'getGetThemeMockHandler',
  'getUpdateThemeMockHandler', 'getPublishWebsiteMockHandler', 'getListPublicationsMockHandler',
  'getRollbackPublicationMockHandler',
];
const NO_DRAFT_MESSAGE =
  'apps/web chỉ đọc site đã publish (GET /api/site, 05 §7, #108) — không import API draft/website editing (review I2, WEBSITE-005).';

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
    // #19: enum TS bị cấm — đẩy quy ước lên lint thay vì chỉ ghi ở CLAUDE.md (#17). Repo chưa có `no-restricted-syntax`
    // nào khác; thêm selector mới thì gộp vào đây (flat config ghi đè chứ không gộp rule cùng tên).
    files: ['**/*.{ts,tsx,mts,cts}'],
    rules: {
      'no-restricted-syntax': [
        'error',
        { selector: 'TSEnumDeclaration', message: 'Không dùng enum TS — dùng const object + union type (Quyết định #19).' },
      ],
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
    // apps/web có block riêng ngay dưới (thêm luật cấm draft) — flat config ghi đè chứ không gộp rule cùng tên.
    files: ['packages/builder-renderer/**/*.{ts,tsx}', 'packages/builder-components/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': ['error', { patterns: [BUILDER_CORE_IMPORT_PATTERN, ...APPS_IMPORT_PATTERNS] }],
    },
  },
  {
    // apps/web: cấm builder-core (như trên) + cấm API draft của module website (review I2, WEBSITE-005).
    files: ['apps/web/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          paths: [
            { name: '@vsite/api-sdk', importNames: WEBSITE_DRAFT_SDK_NAMES, message: NO_DRAFT_MESSAGE },
            { name: '@vsite/api-sdk/mocks', importNames: WEBSITE_DRAFT_MSW_NAMES, message: NO_DRAFT_MESSAGE },
          ],
          patterns: [
            BUILDER_CORE_IMPORT_PATTERN,
            ...APPS_IMPORT_PATTERNS,
            { group: ['**/api-sdk/src/**'], message: 'Chỉ import @vsite/api-sdk qua barrel (index/mocks), không đi tắt vào src/generated.' },
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
