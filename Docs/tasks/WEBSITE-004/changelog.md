# WEBSITE-004 — changelog

## S0-T1

`requiresProps` vào `tree-rules.json` (Bước 8b, S0, codegen).

**Thay đổi**
- `packages/builder-components/scripts/gen-registry.ts` — `generateTreeRules` nay xuất thêm
  `requiresProps: { [variantKey]: string[] }` (mảng đã sort) bên cạnh `variants` đang có. Additive,
  `schemaVersion` của `tree-rules.json` giữ nguyên 1. Nguồn là `variant.requiresProps` vốn đã có trong
  manifest (được invariant #3/§6.1 bảo đảm là tập con của `usesProps`), nên không sửa manifest nào.
- `packages/builder-components/tests/gen-registry.test.ts` — thêm field `requiresProps` vào type
  `TypeRule` và một test: `Hero.requiresProps.Hero01 = ['image','title']`, `Hero02 = ['title']`; mọi key
  của `requiresProps` khớp `variants`; mỗi mảng đã sort.

**Kiểm chứng**
- `pnpm --filter @vsite/builder-components test -- --run gen-registry` → 21/21 passed (TDD: FAIL trước
  khi hiện thực, PASS sau).
- `pnpm gen:registry` → OK, check-additive xanh. `generated/tree-rules.json` có `requiresProps` cho cả
  5 manifest. `generated/` gitignored nên không vào commit; `registry.lock.json` không đổi (không sửa
  manifest).

**Không làm**: không sửa manifest, không đụng `registry.lock.json`, không thêm artifact mới (đó là S0-T2).

## S0-T2

Vỏ `SiteSnapshot` v1 bằng Zod + JSON Schema (Bước 8b, S0, codegen, #94, #97, #60).

**Thay đổi**
- `packages/builder-components/meta/envelope-schema.ts` — thêm `buildSiteSnapshotZod(locales, themeConfig)`
  và `buildSiteSnapshotJsonSchema(locales, themeConfig)` đúng shape plan §4. `tree` của mỗi trang tái dùng
  vỏ Tree sẵn có (JSON Schema nâng `node` lên `$defs` gốc, `treeEnvelope`/`themeTokens` là `$defs` riêng,
  tham chiếu bằng `$ref`); `theme.tokens` tái dùng vỏ Tokens. `additionalProperties:false` ở mọi cấp vỏ;
  `tracking` là map mở (như `props`), `navigation.items` chỉ kiểm là mảng (NavigationConfig thật là Bước 7),
  `media[*]` = `{ storageKey, preset|null }`. Thêm instance `siteSnapshotSchema` + type `SiteSnapshot`.
- `packages/builder-components/scripts/gen-registry.ts` — ghi thêm artifact
  `generated/site-snapshot.schema.json` (cạnh `tree-envelope`/`theme-tokens`); log "12 artifact".
- `packages/builder-components/src/index.ts` — export `siteSnapshotSchema` + type `SiteSnapshot`.
- `packages/builder-components/tests/site-snapshot-schema.test.ts` (mới) — 31 ca khẳng định Zod ↔ JSON
  Schema đồng ý: ví dụ §4 parse được; thiếu `pages[].id` → lỗi; `slug:null` hợp lệ; key lạ ở gốc/các cấp
  → lỗi; `navigation.items` rỗng hợp lệ; `theme.tokens` sai → lỗi; `defaultLocale`/`locales` ngoài config
  → lỗi; dựng từ locales khác thì schema đổi theo (một nguồn, #17).
- `packages/builder-components/tests/gen-registry.test.ts` — thêm `site-snapshot.schema.json` vào danh sách
  `GENERATED_FILES` (kiểm determinism giữa hai lần chạy).

**Kiểm chứng**
- `pnpm --filter @vsite/builder-components test` → 535/535 passed (TDD: FAIL trước khi `gen:registry`, PASS sau).
- `pnpm gen:registry` → OK, 12 artifact, check-additive xanh, `registry.lock.json` không đổi.
- `pnpm --filter @vsite/builder-components typecheck` sạch · `pnpm build` → 4/4 tasks OK (gồm `@vsite/web`
  dùng index export mới).
- `generated/` gitignored nên `site-snapshot.schema.json` không vào commit — dựng lại bằng `pnpm gen:registry`.

**Giả định**: `defaultLocale`/`locales` ràng buộc bằng enum từ `config/locales.json` (một nguồn, #17), đồng
bộ với khoá `i18n` của vỏ Tree; `kind`/`systemType` chỉ kiểm là chuỗi (enum của chúng thuộc tầng BE, không
đưa vào vỏ cấu trúc).
