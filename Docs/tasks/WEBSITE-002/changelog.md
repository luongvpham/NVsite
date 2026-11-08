# WEBSITE-002 — Changelog (Bước 5b, FE)

> **Đang làm** — plan đã chốt 2026-10-21 ([`plan.md`](plan.md)). Ghi lệch so với thiết kế/plan theo từng
> task F1…F11 khi code.

## Đã làm

### F1 — Route Website, chặn non-Owner, tạo website, danh sách trang
- `_authenticated.shops.$shopId.tsx` thành layout (`<Outlet/>`); màn sửa shop chuyển sang
  `_authenticated.shops.$shopId.index.tsx` (URL không đổi), thêm link "Website" chỉ với Owner.
- `/shops/$shopId/website` (`_authenticated.shops.$shopId.website.index.tsx`): `OwnerOnlyGate` (non-Owner →
  "Chỉ chủ shop", không mount con → không request website), `WebsiteOverview` (404 `WEBSITE_NOT_FOUND` →
  "Tạo website"; 409 `WEBSITE_ALREADY_EXISTS` → tải lại; 422 hosted giải thích; lỗi khác + Thử lại),
  `PageList` (chỉ đọc, sắp `sortOrder`, nhãn Trang chủ/hệ thống/nội dung, Đang tắt, isDirty; gợi ý khi
  chưa có trang Composable ngoài trang chủ).
- `MediaLibraryPicker`: nút "Tải ảnh lên thư viện" ẩn khi không phải Owner (#100).
- `error-messages.ts`: thêm mã Website/Page/tree/theme của brief.
- MSW portal đăng ký `getWebsiteMock()`.
- Test: `src/routes/-website.test.tsx` (10 ca), `error-messages.test.ts` (+1), `media-library-picker.test.tsx` (+1).
  Portal 66/66 xanh, `tsc` + `eslint` sạch.
- **Smoke app thật (workflow §8) — PASS 2026-10-22** qua proxy Vite `http://admin.vsite.local:5173`, BE build
  từ `feature/WEBSITE-002` (curl + cookie jar thay trình duyệt):
  SPA `/shops/{id}/website` → 200 `text/html` · register → verify email → login (cookie `vsite_rt` set) ·
  refresh chỉ bằng cookie → 200 (F5 giữ phiên, #101) · tạo shop Hosted → `roleCode = "Owner"` ·
  `GET /website` → 404 `WEBSITE_NOT_FOUND` · `GET /pages` → `200 []` · `POST /website` → 201 · `GET /pages` →
  Trang chủ (`isHomePage`), Liên hệ (`Contact`), Không tìm thấy trang (`NotFound`) · `POST` lần 2 → 409
  `WEBSITE_ALREADY_EXISTS`. Khớp đúng các nhánh UI F1 dựa vào.
  **Chưa bấm tay giao diện trên trình duyệt** (không có công cụ trình duyệt trong session) — phần render đã
  phủ bằng test `-website.test.tsx`.
- Môi trường dev phát hiện lúc smoke (không sửa file nào): DB `vsite_dev` chưa có trên Postgres `:5433`
  (đã tạo + `dotnet ef database update`: `InitialSchema`, `AddWebsiteModule`); BE cần Redis `:6379`
  (`docker compose up -d redis`); process `Vsite.Api` cũ từ 2026-10-11 đã tắt, chạy lại bản mới.
  `docker-compose.yml` dựng Postgres `:5432` user `vsite` — **lệch** connection string trong
  `appsettings*.json` (`:5433`, `postgres`/`password`).

### F2 — Tạo / sửa / xoá trang
- `PageList`: nút "Tạo trang", mỗi dòng "Sửa" (mọi trang, kể cả trang chủ/System) và "Xoá" (**chỉ** khi
  `deletable`; hộp xác nhận `role=alertdialog`; 409 `PAGE_NOT_DELETABLE` hiện trong hộp).
- `PageFormDialog` (tạo/sửa metadata, RHF + Zod): trang chủ + System → slug khoá và gửi `slug: null`;
  NotFound → `noIndex` bật cứng + khoá; `PAGE_SLUG_TAKEN|RESERVED|INVALID` gắn ô slug,
  `PAGE_NOINDEX_REQUIRED` gắn ô noindex, `MEDIA_INVALID_IMAGE_REFERENCE` gắn mục ảnh, `CONCURRENCY_CONFLICT`
  → báo + nút "Tải lại" (làm mới danh sách, đóng dialog); mã còn lại → alert chung.
- `lib/page-validation.ts`: phản chiếu `SlugRules.cs`/`Page.cs` — chiếm chỗ (locales, route System page, `reservedShopSitePaths`;
  **không** `reservedPaths`) kiểm trước, rồi khuôn `^[a-z0-9]+(-[a-z0-9]+)*$` ≤120; title ≤160, SEO title ≤160,
  SEO description ≤320; SEO rỗng → `null`.
- `MediaLibraryPicker`: `mode="library"` trả thẳng bản Library (không clone, không cần `preset`) cho `ogImageId`
  (brief "Media"); cảnh báo xoá ảnh liệt kê từng nơi dùng ("Logo shop", "Trang {pageTitle}", thiếu tiêu đề →
  "Một trang") — Flow 7.
- Test: `page-validation.test.ts` (11), `page-form-dialog.test.tsx` (17), `page-list.test.tsx` (6),
  `media-library-picker.test.tsx` (+3). Portal 105/105, `tsc` + `eslint` sạch. Test Escape đã kiểm đột biến
  (bỏ guard → fail).
- **Kiểm với BE thật — PASS 2026-11-06** (Docker bật; BE `feature/WEBSITE-002`, qua proxy Vite
  `admin.vsite.local:5173`; 61/61 kiểm tra): tạo trang 201 + `Location`; trùng slug 409 `PAGE_SLUG_TAKEN`;
  reserved 422 `PAGE_SLUG_RESERVED` cho `lien-he en EN vi media api assets static san-pham danh-muc tim-kiem dat-lich`;
  `blog`/`about` **không** bị giữ chỗ (xác nhận FE không được dùng `reservedPaths`); sai khuôn 422 `PAGE_SLUG_INVALID`
  (`Dich-Vu a--b -a a- dịch-vụ "a b" a_b "" 121 ký tự`), đúng 120 ký tự → 201; title 161 ký tự → 422
  `VALIDATION_ERROR` (FE chặn trước nên ít gặp); `ogImageId` lạ → 422 `MEDIA_INVALID_IMAGE_REFERENCE`;
  **OG = bản Library → 201, OG = bản Clone → 422** (đúng lý do picker `mode="library"` không clone);
  trang chủ `slug:null` → 200 giữ `""`, `slug:"abc"` → 422 `PAGE_HOME_SLUG_FIXED`; System `slug:null` → 200, `slug:"x"`
  → 422 `PAGE_SYSTEM_SLUG_FIXED`; NotFound `noIndex:false` → 422 `PAGE_NOINDEX_REQUIRED`, `noIndex:true` + `slug:null`
  → 200; xoá trang chủ/Contact/NotFound → 409 `PAGE_NOT_DELETABLE`, xoá trang thường 204, xoá lần hai 404
  `PAGE_NOT_FOUND`, xoá rồi tạo lại cùng slug 201; `deletable` chỉ `true` ở trang Composable không phải trang chủ;
  người không có membership gọi website/pages/upload Library → 403 `SHOP_ACCESS_DENIED`; không token → 401.
  Chưa phủ: role Manager/Staff có membership (BE đã có test R16 ở WEBSITE-001) và `CONCURRENCY_CONFLICT` của
  `updatePage` (cần đua hai request thật).
- **Test Docker của BE — PASS 2026-11-06**: `dotnet test backend/tests/IntegrationTests --filter "Website|Media"`
  → 587 pass, 0 fail, 1 skip (có sẵn: `ShopEndpointTests.UpdateShop_Hosted_to_ExternalOnly_…`). `Docs/DOCKER-TEST-DEBT.md`
  rỗng, nhánh này không đổi dòng nào trong `backend/`.

### F3 — Nền packages: export schema cây/theme + kiểu lỗi có cấu trúc
- `builder-components/src/index.ts` export thêm `treeEnvelopeSchema`, `themeTokensSchema`, `NODE_ID_PATTERN`
  (từ `meta/envelope-schema.ts`), `opRules` (từ `generated/op-rules.ts`), `sanitizeRichText`, `isSafeLinkUrl`.
  Không sửa manifest/generated; `pnpm gen:registry` chạy lại không đổi file nào.
- `isSafeLinkUrl` chuyển từ `tests/helpers/reference-tree-validator.ts` sang `src/link-url.ts`; helper test import
  lại — một nguồn, 44 fixture cây vẫn xanh.
- `shared/src/errors/problem-details.ts`: `ProblemDetails.params?: unknown`; kiểu `TreeValidationError`,
  `TreeValidationParams` (`errors[] {path, code, message}` + `truncated`), `DraftConflictParams`
  (`currentVersion`, `updatedAt`, `updatedByUserId | null`); guard `isTreeValidationParams`,
  `isDraftConflictParams`; `getProblemParams(error, guard)` → `undefined` khi thiếu/sai hình dạng (không cast mù).
  Hình dạng đối chiếu `WebsiteExceptions.cs` (`DocumentInvalidParams`, `DraftConflictParams`).
- Test: `builder-components/tests/public-exports.test.ts` (28: vỏ cây, theme, id node, `opRules`, `sanitizeRichText`,
  `isSafeLinkUrl` kể cả mọi `invalid-link-url-*` + `valid-link-urls` của fixture dùng chung), `shared` +5 ca
  `getProblemParams`. `pnpm -r test` xanh (builder-components 279, shared 7, portal 105, renderer/api-sdk/…),
  `tsc` + `eslint` sạch ở builder-components/shared/portal, `pnpm --filter @vsite/web build` và `@vsite/portal build` OK.

### F4 — `packages/builder-core`: Operations Engine + store undo/redo
- Package mới `@vsite/builder-core` (chỉ portal dùng): `immer` + `zundo` + `zustand` (vanilla) + `zod`; thuần TS, test ở Node.
  `CLAUDE.md` của package ghi ranh giới, invariant và checklist 6 chỗ khi thêm op.
- **5 op** (Zod `operationSchema`): `add {parentId,index,node}` · `remove {nodeId}` · `move {nodeId,toParentId,toIndex}` ·
  `update {nodeId,path,value}` · `changeTheme {tokens}`. `validateOperation` theo `05` §11: [1] PageKind (System: add/remove/move
  → `SYSTEM_PAGE_STRUCTURE_CHANGED`, update chỉ `editableInSystemPageProps` → `SYSTEM_PAGE_PROP_NOT_EDITABLE`), [2]
  `allowedInPageKinds`, [3] đích tồn tại, [4] props (khoá lạ, Zod, link), [5] binding source (cấm `Review`), + `maxPerPage`,
  `acceptsChildren`, `allowedChildTypes`, id trùng, i18n, độ sâu/số node. Mã lỗi dùng ĐÚNG tên mã BE.
- `applyOperation` (Immer, structural sharing; op không đổi gì trả chính cây cũ) và `executeOperation` (parse → validate → apply,
  không ném lỗi). Store: `loadDraft` (xoá lịch sử), `loadTheme`, `dispatch`, `select`, `undo/redo`, `canUndo/canRedo`; zundo chỉ
  chụp `tree` (giới hạn 100 bước), so tham chiếu cây để không ghi bước thừa.
- `tree/`: `findNode`, đo cây, `pointerOf`/`resolvePointer` (JSON Pointer ↔ node + prop path, cho F8 tô đỏ lỗi 422), `limits`
  (1 MB byte UTF-8 / sâu 20 / 2000 node / theme 64 KB — khớp `PageTreeLimits` của BE), `generateNodeId` (`c_` + 8 ký tự mật mã).
- `builder-components`: export thêm `treeRules` (bản đồ prop + variants) và `getPropsSchema(type)` (Zod props theo type, tra theo tên).
- ESLint: `no-restricted-imports` chặn `builder-renderer`, `builder-components`, `apps/web` import `@vsite/builder-core`
  (đã thử: file mồi import bị báo lỗi).
- Test: **130** ở `builder-core` — luật theo bảng `05` §11, apply (đóng băng sâu + structural sharing), `setIn`, store, và
  **hợp đồng với BE bằng fixture chung** (`tests/fixtures/tree/*.json`, nạp trực tiếp): mọi cây Composable hợp lệ dựng lại được
  bằng chuỗi op `add`; mọi cây không hợp lệ thuộc phạm vi Engine bị từ chối đúng mã; cặp System stored/incoming → `update`
  tương đương được/bị từ chối đúng; danh sách fixture bỏ qua có chủ đích được chốt trong test. Kiểm đột biến 4 luật quan
  trọng (bỏ chặn System, bỏ kiểm link, bỏ kiểm binding source, history ghi cả select/theme) — mỗi đột biến làm 4–10 ca fail.
  `builder-components` 283 (+4), toàn workspace `test`/`typecheck`/`lint` xanh, `gen:registry` không đổi file, web + portal build OK.

### F5 — Theme qua CSS variables
- `theme-engine/src/css-vars.ts` (thuần, isomorphic): `tokensToCssVars(tokens)` → `--vs-color-<key>` (camelCase → kebab:
  `textMuted` → `--vs-color-text-muted`), `--vs-font-heading|body`, `--vs-font-scale`, `--vs-radius`, `--vs-shadow`,
  `--vs-section-gap`; `defaultThemeVars`; `colorVar(key)`; hằng `radiusVar`/`shadowVar`/`sectionGapVar`/`headingFontVar`/
  `bodyFontVar` (mỗi cái có giá trị mặc định làm dự phòng trong `var()`); `themeStyle(tokens)` = biến + nền/chữ/font/cỡ chữ
  của khung chứa trang (F6 đặt ở canvas, 8b ở `apps/web`); `themeColorKeys`.
- **Chỉ whitelist**: mọi giá trị đi vào CSS phải thuộc tập của `config/theme-tokens.json` (màu qua `colorPattern`); đầu vào
  sai/thiếu không ném lỗi mà rơi về mặc định từng phần; chuỗi lạ không bao giờ xuất hiện trong kết quả (không chèn CSS được).
- 8 variant của 5 component đọc `var(--vs-*)` cho màu/bo góc/bóng/font tiêu đề/khoảng cách; không còn class màu của chrome
  tĩnh (`bg-primary`, `text-muted-foreground`, `bg-muted`, `border-border`, `bg-white`, `text-black`). Section: `background` là
  KEY màu của theme (key lạ/thiếu/key cũ như `muted`, `foreground` → `background`); khoảng cách trên/dưới theo
  `--vs-section-gap` (`sm` = 40% của `lg`). Inspector `color-token` liệt kê `colorKeys` của config thay cho key của theme tĩnh.
- `theme-engine` có hạ tầng test (vitest, môi trường node) và bỏ `composite` trong tsconfig (để import JSON cấu hình).
- Test: `theme-engine` 49 (đầy đủ tập giá trị của config phải có ánh xạ; đầu vào hỏng; chống chèn CSS; `colorVar`);
  `builder-components` +27 (`theme-vars.node.test.tsx` — 8 variant render ở Node, không còn class chrome, có `var(--vs-…)`;
  Inspector liệt kê key màu mới); `builder-renderer` +3 (SSR `renderToString` ở Node: khung chứa mang biến của theme, component
  đọc đúng biến; không đặt theme thì vẫn ra màu mặc định, không rơi về chrome). Kiểm đột biến 3 chỗ (bỏ kiểm pattern màu,
  `colorVar` nhận key bất kỳ, font không qua whitelist) — mỗi đột biến làm 1–2 ca fail. `pnpm -r test/typecheck/lint` xanh,
  `gen:registry` không đổi, web + portal build OK.

### F6 — Khung editor: nạp draft, canvas, chọn node, Inspector, undo/redo
- `builder-renderer`: `RenderTree` nhận gốc `Page` (render con, không render gốc) + `decorateNode?(node, rendered)`; truyền node
  thường / không truyền `decorateNode` thì output y như cũ. Export `PageRoot`, `DecorateNode`.
- `builder-components` `Inspector`: prop `isFieldDisabled?` (bọc từng field trong `<fieldset disabled>`) — trang System khoá prop
  ngoài `editableInSystemPageProps`. Chỉ là UI; Engine vẫn từ chối op.
- Route `/shops/$shopId/website/pages/$pageId` (`OwnerOnlyGate` ngoài cùng → non-Owner không có request draft). `EditorShell`:
  `getPageDraft` (`staleTime: Infinity`, không refetch khi focus) → `treeEnvelopeSchema.safeParse` (hỏng → "Dữ liệu trang không đọc
  được", không cho sửa) → `EditorSession` tạo store và `loadDraft` **một lần** (#20). Lỗi/404 map qua `error-messages`.
- `EditorCanvas`: `builder-renderer` thật; mỗi node bọc `div[data-node-id]` (click/Enter chọn, chặn điều hướng link, viền chọn);
  theme đang sửa áp bằng `themeStyle(themeDraft)`; cây rỗng → "Trang trống — thêm khối từ bảng bên trái".
- `useMediaMap`: `mediaMap` từ `getAssetsByIds` cho mọi `imageId` trong cây + điền thẳng ảnh vừa chọn. `collect-image-ids.ts`.
- `InspectorPanel` → op `update {nodeId, path, value}`; op bị Engine từ chối hiện `message` tại chỗ. Toolbar Hoàn tác/Làm lại
  (theo `temporal` của zundo) + Ctrl/Cmd+Z, Ctrl/Cmd+Shift+Z, Ctrl+Y (bỏ qua khi focus ô nhập).
- `PageList` có prop `renderOpenEditor`; `WebsiteOverview` truyền link "Soạn nội dung" (prop để `PageList` test không cần router).
- `ImagePickerControl` (+ test) chuyển từ `routes/dev-registry.tsx` sang `components/media/`; **xoá** `/dev-registry`, link DEV ở
  `_authenticated.tsx` và link ở trang chủ; `routeTree.gen.ts` sinh lại. `apps/portal` thêm dependency `@vsite/builder-core`.
- Test: `-editor.test.tsx` (16: loading/404/Zod hỏng/rỗng/non-Owner không request, chọn node + Inspector, sửa title → canvas đổi,
  undo/redo bằng nút + phím + bỏ qua khi gõ, System chỉ bật `content`), `renderTree.test.tsx` +4, `Inspector.test.tsx` +1.
  Portal 121/121, `pnpm -r typecheck/lint` sạch, `gen:registry` không đổi, web build OK. Đột biến (bỏ khoá field System) → 1 ca fail.

## Lệch so với thiết kế / plan
- F6: `decorateNode` được gọi **con trước, cha sau** (chữ ký `(node, rendered)` cần `rendered` của cha đã chứa con).
- F6: mỗi lần gõ một ký tự ở Inspector là một op `update` = **một bước undo**; lịch sử giới hạn 100 bước nên gõ dài sẽ đầy. Chưa gộp
  (plan không nêu) — cần gộp op liên tiếp cùng `nodeId+path` trong cửa sổ ngắn; nên làm cùng F8 khi autosave chốt nhịp lưu.
- F6: control `lexical` vẫn là textarea stub (F10); chưa có ô nhập URL cho link nên đường `LINK_URL_UNSAFE` qua UI chưa thử được
  (đã phủ ở `builder-core`).
- F6: `/dev-registry` bị xoá nên nghiệm thu tay MEDIA-001 (08 §9) qua trang này không còn — dùng picker trong editor.
- F5: **không xoá** `colorTokens` của `theme-engine` như plan dự kiến — `packages/ui/tailwind-preset.js` (màu của chrome portal/web:
  nút, viền, chữ phụ) vẫn dùng nó. Chỉ component builder thôi dùng; tên giống nhau nhưng là hai thứ khác (chrome tĩnh vs theme của shop).
- F5: màu/bo góc/font của component đi qua `style` inline với `var(--vs-*, mặc định)` thay vì class Tailwind tuỳ ý
  (`bg-[var(--…)]`) — không phụ thuộc Tailwind quét `@source`, và chuỗi `style` kiểm được nguyên văn ở test SSR. Layout vẫn dùng Tailwind.
- F5: biến `--vs-font-scale` (0.9375 / 1 / 1.0625) và font body chỉ có tác dụng khi khung chứa dùng `themeStyle()`; component không tự
  đặt cỡ chữ gốc. Màu chữ trên nền `primary` của nút CTA lấy `--vs-color-background` (chưa tính độ tương phản tự động).
- F5: `Hero01` vẫn giữ lớp phủ `bg-black` + chữ `text-white` trên ảnh (không phải màu theme).
- F5: `builder-renderer` thêm devDependency `@vsite/theme-engine` (chỉ để test SSR), `builder-components`/`builder-renderer` thêm
  `@types/react-dom` (test dùng `react-dom/server`).
- F4: `update` mang MỘT đường dẫn mỗi op (`{nodeId, path, value}`, `value` undefined = xoá prop / gỡ phần tử mảng) thay vì "props
  patch" — khớp `onChange(path, value)` của Inspector và `update_component_props` của AI. Sửa nhiều prop = nhiều op.
- F4: node `add` **không** mang `children` (thêm nhánh = nhiều op, cha trước con) và **không** điền props mặc định từ manifest như
  plan ghi — component tự có mặc định khi thiếu prop, cây không phình và không lệch khi manifest đổi (#43). `requiresProps` vẫn
  chưa enforce (bắt ở publish, 8b). `add` thiếu `variant` bị từ chối (`NODE_VARIANT_UNKNOWN`): renderer tra `Type/Variant`.
- F4: Engine chặn `add`/`move` vượt độ sâu 20 / 2000 node (`TREE_TOO_DEEP`/`TREE_TOO_MANY_NODES`, mã của BE); 1 MB chỉ **đo**
  (`checkTreeLimits`) — F8 chặn gửi. `toIndex` của `move` tính sau khi gỡ node khỏi chỗ cũ.
- F4: Engine **không** kiểm `richText` nguy hiểm — bộ phân loại (`classifyDangerous`) ghi rõ "chỉ dùng trong test, không phải
  hiện thực của BE", nên không nâng lên production. BE từ chối (`RICHTEXT_UNSAFE`), FE làm sạch ở control Lexical (F10). Với
  textarea hiện tại, nội dung độc chỉ bị chặn khi BE trả 422.
- F4: mã chỉ-FE (không gửi/nhận BE): `INVALID_OPERATION`, `TARGET_NOT_FOUND`, `INDEX_OUT_OF_RANGE`, `MOVE_INTO_DESCENDANT`, `NO_DRAFT`.
  Trang System: add/remove/move trả `SYSTEM_PAGE_STRUCTURE_CHANGED` trước cả luật node (chặt hơn BE, cùng kết luận).
- F4: không có `invert()` từng op (workflow §11 viết trước #42) — undo là zundo chụp trạng thái (G2). `changeTheme` đi qua
  cùng cửa `dispatch` nhưng ghi `themeDraft` ngoài lịch sử cây và dùng được khi chưa nạp draft.
- F4: luật ESLint chặn import `builder-core` mở rộng sang `builder-components` và `apps/web` (plan chỉ nêu renderer): components
  bị `builder-core` import nên import ngược sẽ thành vòng; `apps/web` không có Engine.
- F4: fixture nạp bằng `import.meta.glob` (không `node:fs`) vì thêm `@types/node` riêng cho `builder-core` làm pnpm tách thêm một
  biến thể `vitest` → kiểu của `jest-dom` hỏng ở `builder-renderer`/`builder-components` (lint đỏ).
- F4: `apps/portal/src/test/setup.ts` nới `asyncUtilTimeout` lên 4000 ms — một ca F1 (`Owner thấy link Website`) từng fail một lần
  khi cache transform còn nguội sau cài lại, chạy lại thì hết.
- F3: `props-schemas.ts` (Zod props theo type) **chưa** export — plan F3 không liệt kê; F4 cần `safeParse` props
  sẽ export lúc đó (ghi lại ở F4).
- F1: `retry: false` cho `getWebsite` — 404 là trạng thái bình thường "chưa có website", không retry.
- F2: form dùng schema Zod viết tay (trường dạng chuỗi cho SEO) thay vì `createPageBody`/`updatePageBody` sinh từ
  contract — contract coi SEO là `string | null`, form cần chuỗi rỗng; kiểu payload vẫn ràng bằng
  `CreatePageRequest`/`UpdatePageRequest` của SDK (compile-time).
- F2: danh sách vẫn chưa hiện nhãn ảnh OG; ảnh chọn xong hiện ngay từ asset vừa chọn (không round-trip `assets?ids=`).

## Chưa làm xong
