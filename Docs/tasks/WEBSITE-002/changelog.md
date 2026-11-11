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

### F7 — Kéo-thả: thêm / di chuyển / xoá khối
- `builder-components`: export `componentManifests` (`src/manifests.ts`, chỉ đọc `registry/*.manifest.ts`) làm nguồn nhãn tiếng Việt cho
  palette; `tests/manifests.test.ts` buộc danh sách phủ đúng mọi type/variant của `tree-rules.json` (thêm component mà quên là fail).
- **Palette** (`ComponentPalette`): lọc theo `allowedInPageKinds` của loại trang; mỗi kiểu hiển thị một nút "Thêm …". Nút hỏi cùng
  `validateOperation` mà `dispatch` dùng nên tắt kèm đúng lý do Engine sẽ nêu (Hero thứ hai → "tối đa một lần"). Trang System: ẩn hẳn,
  chỉ còn một dòng giải thích. Vị trí thêm do `resolveAddTarget`: chọn Section → cuối Section; chọn khối khác → ngay sau nó; chưa chọn → cuối trang.
- **Lên / Xuống / Xoá khối** (`NodeActions`, nhóm "Thao tác với khối", ẩn ở System) + phím **Delete** (bỏ qua khi đang gõ ở ô nhập, và ở System):
  cùng op `move`/`remove` với kéo-thả. `siblingMove` đổi chỗ với anh em liền kề, **cùng cha**. Xoá node đang chọn → bỏ chọn (store).
- **Kéo-thả thật** (`@dnd-kit/core`, `EditorDnd`): kéo nút palette → `add`; kéo node trên canvas → `move` (cùng cha / vào Section / ra ngoài).
  Vùng thả là các "slot" giữa các khối + cuối mỗi khối chứa + cuối trang (chỉ hiện lúc đang kéo; không chiếm chỗ lúc thường). Rê vào slot:
  xanh nếu thả được, đỏ kèm lý do nếu Engine sẽ từ chối (`evaluateDrop`, gọi trước khi thả); thả vào chỗ sai → alert "Không thả được: …".
  `evaluateDrop` thuần: `toIndex` của `move` trừ 1 khi cùng cha và thả phía sau; thả ngay trước/sau chính nó = no-op (không thêm bước undo).
  Chỉ PointerSensor (kéo ≥ 8px nên bấm để chọn vẫn bình thường) + `pointerWithin` (không tự đoán "gần nhất").
- `ImagePickerControl`/test không đổi. `editor-helpers.ts` (test): helper dùng chung, `-editor.test.tsx` chuyển sang dùng.
- Test: `drop-operation.test.ts` (15), `add-target` (5), `sibling-move` (4), `editor-dnd.test.tsx` (12, gọi thẳng `onDragEnd` của DndContext:
  add/move/no-op/chặn/System/xoá lỗi cũ), `-editor-structure.test.tsx` (18: palette, vị trí thêm, Hero thứ hai bị chặn, Lên/Xuống, Xoá, Delete,
  undo từng thao tác, System), `manifests.test.ts` (2). Portal 176/176, `pnpm -r test/typecheck/lint` sạch, `gen:registry` không đổi,
  web + portal build OK. Kiểm đột biến 6 chỗ (vị trí vào Section, nút tắt khi bị chặn, guard "đang gõ", `toIndex` của Xuống, chọn node mới sau thả,
  hai bài trước) — mỗi đột biến làm 1–6 ca fail.
- **Chưa kiểm được:** thao tác kéo bằng chuột thật. jsdom không có layout nên không mô phỏng được dnd-kit; phần test chỉ phủ logic thả và nối dây.
  Cần người bấm tay trên trình duyệt (xem mục "Chưa làm xong").

### F8 — Autosave
- `builder-core` store: **gộp các op `update` liên tiếp** cùng `nodeId`+`path` cách nhau < `UPDATE_COALESCE_MS` (2 s, cửa sổ trượt, bằng nhịp debounce
  autosave) thành MỘT bước undo — gõ một câu = một bước, không đầy 100 bước. Cài bằng zundo `pause()/resume()` quanh `set`; op khác, undo/redo,
  `loadDraft` cắt đợt; update không đổi gì không làm mới mốc. `createEditorStore({ now?, coalesceMs? })` để test. Invariant 4 của
  `builder-core/CLAUDE.md` thêm một câu (vẫn snapshot, không op-log — không đổi nghĩa).
- `apps/portal/src/lib/autosave/`: `AutosaveController` (thuần TS, test bằng fake timer; `start()/dispose()` chạy lại được cho StrictMode), `classifySaveError`
  (409 / 422 / 413 / tạm thời — theo `error_code` rồi `status`), `mapTreeErrors` (422 `path` → nodeId + propPath qua `resolvePointer`; không gắn được → banner).
  Hook `use-autosave.ts` nối với `savePageDraft`/`getPageDraft` (GET thẳng, **không** qua cache Query).
- Quy tắc của controller: một PUT tại một thời điểm (R1); "đã lưu" = tham chiếu cây hiện tại === tham chiếu cây của PUT thành công gần nhất; `version` chỉ
  gán nguyên văn từ response PUT/GET; response PUT không ghi đè cây local; `sanitized` → GET rồi `loadDraft` **chỉ khi** cây hiện tại vẫn đúng là cây vừa gửi
  (R2), ngược lại bỏ — lần lưu sạch kế tiếp sẽ nạp; lỗi (mạng/5xx/422/413) không tự vòng lặp: chỉ thử lại khi cây đổi tiếp hoặc bấm "Thử lại"; vượt
  1 MB / sâu 20 / 2000 node → không gửi (`blocked`), quay về bản đã lưu (undo) thì hết chặn (R6).
- 409: dừng autosave; `ConflictDialog` **bắt buộc chọn** (Escape/nền không đóng): "Tải bản mới" (GET → `loadDraft`, xoá lịch sử) hoặc "Giữ bản của tôi" →
  xác nhận ghi đè → **GET lấy `version`** → PUT cây local (R8, không dùng `params.currentVersion`). GET lỗi/bản server không đọc được → ở lại dialog, báo lỗi,
  không mất cây local.
- UI: `AutosaveStatus` ("Đã lưu / Chưa lưu / Đang lưu… / Lỗi + Thử lại / quá lớn / chưa hợp lệ / có bản mới hơn"), `ValidationBanner` (quá lớn; lỗi 422 không gắn
  node; "còn lỗi khác" khi `truncated`), canvas `data-invalid` viền đỏ, Inspector danh sách lỗi của khối + ô đỏ qua prop mới **`invalidPaths`**
  (`builder-components`; mức field — `['cta','label']` tô cả `cta`), `LeavePrompt` + `useBlocker` (điều hướng trong app) + `enableBeforeUnload` (đóng tab).
  `packages/ui` `Dialog` thêm prop `dismissible` (mặc định `true`).
- `EditorShell`: `getPageDraft` thêm **`gcTime: 0`** — cache chỉ đúng lúc mở phiên; không bỏ thì mở lại editor lấy cây + version cũ → 409 giả/mất thay đổi.
- Test: `builder-core` +6 (gộp bước), `lib/autosave` 33 (controller 23: debounce, version, R1, R2 ×4, 409 ×6, 422 ×3 dùng fixture
  `invalid-prop-wrong-type`, 413, 1 MB, 2000 node, 5xx; lib 10), `Inspector` +2, `-editor-autosave.test.tsx` 14 (MSW + fake timer: PUT/version, sanitized,
  409 ×3, 422 ×3, 413, 2000 node, 5xx, blocker ×2, mở lại → GET mới). Portal 223, builder-components 315, builder-core 136; `pnpm -r typecheck/lint` sạch,
  `gen:registry` không đổi, web + portal build OK. Đột biến 11 chỗ (bỏ gộp bước, bỏ chặn PUT song song, sanitized không kiểm cây, keepMine dùng
  currentVersion, bỏ chặn giới hạn, 409 không dừng, bỏ banner cho path mồ côi, bỏ `gcTime:0`, bỏ truyền `invalidPaths`, tắt blocker, Inspector không tô) —
  mỗi đột biến làm 1–2 ca fail, đã khôi phục.
- **Smoke BE thật** (HTTP, BE sẵn ở :5270, shop smoke): PUT liên tiếp dùng version trả về → 200; version cũ → **409** `params {currentVersion, updatedAt,
  updatedByUserId}`; prop sai kiểu và richText độc → **422** `error_code: TREE_INVALID`, `params.errors[{path, code, message}]` (mã con `PROP_INVALID`/`RICHTEXT_UNSAFE`
  ở `errors[].code`); ~1,2 MB → **413** `TREE_BODY_TOO_LARGE`; HTML Lexical thô → 200 `sanitized:true`, GET trả bản sạch cùng version. Hình dạng khớp bộ phân loại.
  Draft smoke đã trả về bản ban đầu. **Chưa smoke bằng trình duyệt** (xem "Chưa làm xong").

### F9 — Theme panel
- Tab "Giao diện" ở cột phải editor (cạnh "Thuộc tính"; có dấu • khi theme chưa lưu). `useThemeDraft` (`hooks/use-theme-draft.ts`): `getTheme` (`retry:false`, `staleTime: Infinity`, `gcTime: 0`) → `themeTokensSchema.safeParse` → `store.loadTheme` **một lần lúc mở phiên** (canvas thấy theme đã lưu ngay, không đợi mở tab); sửa qua op `changeTheme` (ngoài lịch sử undo của cây); "bản đã lưu" giữ trong hook để so (`themeEquals`, không phụ thuộc thứ tự khoá) và để "Hoàn tác".
- Control dựng từ `themeOptions` (theme-engine export thêm, đọc `config/theme-tokens.json` — không viết tay danh sách thứ hai): 7 màu (ô chọn màu + ô hex), 2 font, cỡ chữ, bo góc, đổ bóng, khoảng cách khối. Màu sai `colorPattern` bị chặn ở client (báo lỗi, không gửi op, canvas giữ màu hợp lệ gần nhất; rời ô thì trả về màu hợp lệ).
- "Lưu theme" → `updateTheme {presetKey (giữ nguyên từ GET, kể cả null), tokens}`; một PUT tại một thời điểm; thành công → bản đã lưu = bản vừa gửi (sửa tiếp trong lúc lưu vẫn tính là chưa lưu). 422 `THEME_INVALID` → `params.errors[].path` (JSON Pointer `/colors/primary`) → tô đúng ô + `message` của BE; path không gắn được vào ô nào → liệt kê ở banner; sửa tiếp thì xoá lỗi. 413 `THEME_BODY_TOO_LARGE` → báo, giữ bản đang sửa. 404 `WEBSITE_NOT_FOUND` → báo, không có "Thử lại". Tokens không qua Zod → "không đọc được", không cho sửa.
- Rời khi chưa lưu: đổi tab → `LeaveThemeTabPrompt` ("Ở lại để lưu" / "Chuyển tab"); rời trang → `useBlocker` + `beforeunload` nay tính cả `theme.dirty` (trước chỉ có autosave).
- `test/editor-helpers.ts`: `openEditor`/`openFixture` nhận thêm responder cho GET theme (mặc định trả theme của config); thêm `themeBody`, `defaultTheme`.
- Test: `lib/theme-edit.test.ts` 16, `routes/-editor-theme.test.tsx` 22 (nạp: loading/404/lỗi+thử lại/Zod hỏng/tập giá trị; sửa: hex + ô màu + select đổi biến CSS canvas, màu sai bị chặn, Hoàn tác, không là bước undo cây; lưu: body đúng + presetKey, null, chống PUT đôi, 422 tô ô + banner + xoá lỗi, 413, 422 không params; rời: nhắc đổi tab, blocker, không chặn khi sạch, non-Owner không request theme). Portal 261, builder-core 136, builder-renderer 10, theme-engine 49; `pnpm -r typecheck/lint` sạch, `gen:registry` không đổi, web + portal build OK. Đột biến 5 chỗ (bỏ chặn màu, `presetKey: null` cứng, bỏ blocker theme, Hoàn tác không nạp lại, ô hex không đồng bộ sau Hoàn tác) — mỗi đột biến làm 1–2 ca fail; đột biến cuối là lỗi thật do test bắt được và đã sửa trước commit.
- **Chưa kiểm được:** BE thật (PUT/GET theme) và trình duyệt thật — xem "Chưa làm xong".

### F10 — Lexical cho RichText
- `builder-components/src/rich-text-editor/`: `RichTextEditor` (control của field `lexical`), `profile.ts` (thuần: `toolbarCapabilities`, `normalizeEditorHtml`), `lexical-html.ts` (`$importHtml`, `exportHtml`, `nodesForProfile`). `sanitize-html.ts` thêm `getProfileTags(profile)`. Inspector tải editor bằng `React.lazy(() => import(...))`; `profile` lấy từ `field.profile` (manifest).
- **Toolbar suy ra từ `config/sanitize-profiles.json`**, không viết tay: `inline` chỉ có B/I/U/S + Liên kết, **không** có chọn khối; `basic` thêm Đoạn/H2–H4/Trích dẫn/Danh sách chấm/số. Nút ngoài profile thì không tồn tại. Chỉ đăng ký loại node mà profile có thẻ (`inline` = chỉ `LinkNode`) nên HTML dán vào không tạo được khối ngoài profile.
- **Luồng**: HTML đã lưu → `sanitizeRichText` **trước khi** vào Lexical (nội dung cũ/độc không vào được editor; mọi thẻ profile cho phép còn nguyên) → sửa → `$generateHtmlFromNodes` → `normalizeEditorHtml` (sanitize theo profile, bỏ `<br>` giữ chỗ cuối nội dung của Lexical, không còn chữ → `''`) → `onChange` → op `update` ở portal. Không phát `onChange` lúc mở; giá trị đổi từ ngoài (undo của cây khi đang chọn khối) → nạp lại editor bằng tag `history-merge`, không phát lại; giá trị trùng cái vừa phát thì không nạp lại (giữ con trỏ).
- `inline` không có `<p>`: Enter = xuống dòng (`<br>`), plugin riêng; nhiều đoạn (dán) nối bằng `<br>`.
- Liên kết: ô URL kiểm bằng `isSafeLinkUrl` (cùng hàm Engine/R7) — `javascript:`/`data:`/`vbscript:`/`//host`/`ftp:` → báo lỗi, không áp dụng; `LinkPlugin` kiểm lần nữa. Lexical tự thêm `rel="noreferrer"` vào `<a>`, sanitizer cho qua (`rel` nằm trong attrs của profile).
- Bộ đếm `n/maxLength`, đỏ khi vượt (op `update` vượt `maxLength` bị Engine từ chối như trước).
- **Lỗ hổng phát hiện khi chạy test Inspector cũ**: `<fieldset disabled>` KHÔNG khoá `contenteditable` → trang System sẽ sửa được RichText bị cấm (Engine vẫn từ chối op, nhưng UI sai). Thêm prop `disabled` xuyên `Inspector → RichTextEditor` (`editor.setEditable`, `aria-disabled`).
- Test: `profile.test.ts` 8, `lexical-html.test.ts` 132 (**mọi vector `xss-richtext.json` × 2 profile × 2 đường vào** — nạp, và dán thô bỏ qua sanitize-trước — ra HTML không chứa `mustNotContain`/thẻ/scheme nguy hiểm, idempotent; round-trip 11 mẫu hợp lệ + inline; `inline` không ra h/ul/p), `RichTextEditor.test.tsx` 21 (toolbar theo profile, nạp không phát, In đậm/H2/danh sách, xoá hết → `''`, chữ chứa `<script>` thành `&lt;script&gt;`, Enter inline, link hợp lệ/6 URL xấu/gỡ link, đồng bộ ngoài, bộ đếm), `Inspector.test.tsx` +2 và 2 ca sửa, `tests/lexical-isolation.test.ts` 2 (không file ngoài `rich-text-editor/` import Lexical; chỉ tham chiếu bằng `import()`), portal `-editor.test.tsx` 2 ca System chuyển sang editor thật. builder-components 480, portal 261, `pnpm -r typecheck/lint` sạch, `gen:registry` không đổi.
- **Bundle**: `pnpm --filter @vsite/web build` rồi tìm "lexical" trong `apps/web/.output` → **0 file**. Portal: Lexical là chunk riêng `RichTextEditor-*.js` (~293 KB, tải khi chọn khối có field `lexical`), bundle chính không chứa.
- Kiểm đột biến 7 chỗ (nạp không sanitize, xuất không sanitize, không tắt editable khi disabled, sync không so `lastEmitted`, bỏ plugin Enter, toolbar bật mọi khối, import tĩnh Lexical) — mỗi đột biến làm ≥1 ca fail. Đột biến "bỏ plugin Enter" ban đầu **không** bị bắt (HTML xuất giống nhau nhờ normalize) → thêm kiểm DOM một đoạn.
- **Chưa kiểm được:** gõ/chọn bằng chuột-bàn phím thật, dán từ Word/Google Docs, IME tiếng Việt (Telex/VNI) trên trình duyệt thật — jsdom chỉ phủ logic và nối dây.

### F11 — Khung crop theo tỉ lệ preset (MEDIA-001 #6)
- `apps/portal/src/lib/crop-frame.ts` (thuần): `presetRatio(preset)` (chỉ `{w}x{h},cover`; `inside`/lạ → `null`), `frameSize(imageAspect, ratio)` (khung lớn nhất đúng tỉ lệ preset nằm gọn trong ảnh), `clampCenter`. `components/media/crop-frame.tsx`: `CropFrame` (ảnh + khung, kéo bằng pointer hoặc bấm → **tâm khung = focal point**, kẹp để khung không tràn ảnh). Dùng ở `UploadSlotDialog` và bước xác nhận clone của `MediaLibraryPicker` (thay chấm bấm cũ).
- Preset `inside` (hoặc lạ): ảnh thuần, không khung, không gửi focal — như trước. Hợp đồng không đổi: vẫn chỉ gửi `focalX/focalY`, **không** có tham số crop → không cần vòng D.
- Khi ảnh vừa load mà focal cũ (focal của asset thư viện) nằm sát mép làm khung tràn, `CropFrame` kéo focal vào trong ngay, để giá trị gửi đi trùng khung đang hiện. Hệ quả có chủ đích: chiều mà khung phủ kín ảnh thì focal luôn 0.5 (chiều đó không ảnh hưởng crop).
- `src/test/setup.ts`: polyfill `PointerEvent` (jsdom không có; mất `clientX/Y`).
- Test: `crop-frame.test.ts` 8 (tỉ lệ, kích thước khung, kẹp), `crop-frame.test.tsx` 4 (khung đúng tỉ lệ, `inside` không khung, kéo → focal = tâm kẹp, thả rồi không kéo nữa, focal mép bị kéo vào), `upload-slot-dialog.test.tsx` +1 (multipart gửi đúng `focalX=0.35,focalY=0.5`; `inside` không có `focalX`), `media-library-picker.test.tsx` +1 (clone gửi `focalX=0.75`). portal 273 test, `tsc`/`eslint` sạch. Đột biến 3 chỗ (đảo tỉ lệ khung, bỏ kẹp, nhận cả `inside`) đều bị bắt.
- **Chưa kiểm được:** kéo bằng chuột/cảm ứng thật, kích thước khung trên ảnh thật với `max-h`/responsive (jsdom không có layout).

## Lệch so với thiết kế / plan
- F10: cài thêm `@lexical/rich-text`, `@lexical/list`, `@lexical/link`, `@lexical/selection`, `@lexical/utils` (plan chỉ nêu `lexical` + `@lexical/react` + `@lexical/html`) — `HeadingNode/QuoteNode/ListNode/LinkNode`, `$setBlocksType`, `$getNearestNodeOfType` nằm ở các gói này và pnpm không cho import gói chỉ có transitive. Tất cả pin `0.53.0`. Portal có thêm `lexical` làm **devDependency** (chỉ test portal gõ chữ vào editor).
- F10 **nợ**: dùng `LexicalComposer` — Lexical 0.53 đánh dấu deprecated (thay bằng `LexicalExtensionComposer`, dự kiến gỡ ở major sau); lint `no-deprecated` tắt đúng một dòng có ghi lý do. Chuyển khi nâng major.
- F10: `tests/setup.ts` của builder-components nâng `asyncUtilTimeout` lên 4000 ms — lần `import()` Lexical đầu tiên trong một file test mất >1 s.
- F10: `jsdom` không gán `.style` cho phần tử MathML nên `$generateNodesFromDOM` ném TypeError ở vector `mxss-math-mglyph-style` khi **dán thô**; chỉ loại đường đó của vector đó khỏi test (đường "nạp" vẫn chạy). Trình duyệt thật có `style`.
- F10: Lexical dùng `<b><strong>`/`<i><em>` khi xuất; `b`/`i` ngoài whitelist bị sanitizer bỏ nhưng `strong`/`em` còn → không mất định dạng. Dòng trống ở **giữa** nội dung (`<p><br /></p>`) được giữ; `<br>` thừa ở **cuối** bị bỏ.
- F10: heading chỉ h2–h4 (theo profile). Dán `<h1>` → sanitizer bỏ thẻ giữ chữ (đúng fixture `h1-outside-whitelist-keeps-text`).
- F10: chưa có chọn `target=_blank` cho link; liên kết ngoài mở cùng tab. Link nội bộ `/…` và `#neo` được `isSafeLinkUrl` chấp nhận nhưng phần render phía `apps/web` xử lý theo `resolveUrl` (#53) — chưa kiểm ở F10.
- F9: lỗi 422 theme hiện nguyên văn `message` của BE (tiếng Anh kỹ thuật như F8); chưa có bảng Việt hoá mã con. Panel chỉ sửa 12 ô của config — không có UI đổi `presetKey`/chọn preset (plan: giữ nguyên).
- F9: theme là state của **website** nhưng store tạo theo **trang** (`EditorSession` remount khi đổi `pageId`) → mỗi trang GET theme mới; theme chưa lưu mất khi sang trang khác (đã có cảnh báo rời trang). Nếu cần giữ xuyên trang thì nâng store lên cấp website — chưa làm.
- F9: "rời tab nhắc" chỉ cảnh báo, bản sửa vẫn nằm ở canvas (không tự lưu/không tự bỏ); `LeaveThemeTabPrompt` chỉ có hai nút.
- F9: không chặn khi `schemaVersion` của theme khác bản FE biết — Zod (`themeTokensSchema`) từ chối thì rơi vào "không đọc được".
- F9: `themeOptions` thêm vào `theme-engine` (additive, portal được phép ghi) thay vì để portal import thẳng `config/*.json`.
- F8: `error_code` của 422 là `TREE_INVALID` kể cả lỗi richText độc (mã con ở `errors[].code`) — `RICHTEXT_UNSAFE`/`LINK_URL_UNSAFE` ở bảng `error-messages` chỉ dùng nếu BE
  đổi. Thông báo của từng lỗi con hiện nguyên văn `message` của BE (vd. `PROP_INVALID` là tiếng Anh kỹ thuật: `Value is "string" but should be "number"`) — chưa có bảng
  Việt hoá mã con; nên làm khi chốt UX lỗi (F9–F11 hoặc Bước 6).
- F8: lỗi tô đỏ ở Inspector mức **field** (không tới từng phần tử của repeater/ô trong group); danh sách lỗi của khối ghi đủ path/mã nên không mất thông tin.
- F8: lỗi 413 không kèm body (proxy chặn trước BE) phân loại thành "tạm thời" (mutator chỉ ném `response.data`, mất `status`) — BE thật trả `error_code`, nên đủ.
- F8: rời trang chỉ cảnh báo + cho "Ở lại/Rời đi"; **không** tự flush khi rời (tránh PUT mồ côi sau khi phiên đã huỷ). Đóng tab dùng `beforeunload` chuẩn của trình duyệt.
- F8: `getPageDraft` `gcTime: 0` (xem trên) — hệ quả: mỗi lần mở editor luôn GET mới, kể cả quay lại từ danh sách trang.
- F8: Dialog `dismissible` thêm vào `packages/ui` (additive) chỉ để `ConflictDialog` không bị Escape/nền đóng.
- F6 (mục "mỗi lần gõ một ký tự = một bước undo") → **đã xử lý ở F8** (gộp bước theo cửa sổ 2 s).
- F7: chỉ cài `@dnd-kit/core`, **không** cài `@dnd-kit/sortable` như plan ghi — vùng thả là slot tường minh nên `sortable` không có việc.
- F7: nút Lên/Xuống chỉ đổi chỗ trong **cùng cha** (không rời/vào Section); rời/vào Section làm bằng kéo-thả. Muốn bàn phím làm được cả hai thì
  thêm "Đưa ra ngoài/Vào khối trên" sau.
- F7: không có KeyboardSensor của dnd-kit — nó tranh Enter/Space với việc chọn khối; đường bàn phím là các nút Thêm/Lên/Xuống/Xoá.
- F7: nút palette bị tắt (Engine từ chối ở vị trí gợi ý) thì cũng không kéo được; các luật chặn hiện có (maxPerPage, loại trang) không phụ thuộc vị trí nên chưa thành vấn đề.
- F7: chưa có `touch-action: none` cho cảm ứng (editor làm cho desktop).
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
- F7: nghiệm thu tay kéo-thả trên trình duyệt thật — kéo từ palette vào trang rỗng / vào Section rỗng / giữa hai khối; kéo node đổi chỗ, vào và ra Section;
  rê vào chỗ không hợp lệ (Hero thứ hai, Section vào chính nó) thấy thanh đỏ + lý do; thả → alert; bấm để chọn vẫn chọn được; link trong khối không điều hướng.
- F8: smoke bằng trình duyệt thật (không dùng được ở session này): hai tab cùng trang → sửa ở tab A, đợi "Đã lưu", sửa ở tab B → tab B nhận 409 → dialog; thử cả "Tải bản mới"
  và "Giữ bản của tôi". Cây quá lớn (nhiều ảnh/khối) → banner, không PUT. Tắt mạng giữa chừng → "Lỗi" + Thử lại. Đóng tab khi "Chưa lưu" → trình duyệt hỏi.
- F9: smoke với BE thật + trình duyệt: đổi màu thấy canvas đổi; Lưu rồi F5 thấy giữ; gửi theme cố tình sai (sửa tay request) thấy ô đỏ; đổi tab/rời trang khi chưa lưu thì có nhắc. Chưa chạy `GET/PUT /theme` với BE thật ở session này (hình dạng `params.errors[].path` đang dựa vào `ThemeTokensValidator.cs` + fixture, chưa thấy bằng mắt).
- F10: gõ tay trên trình duyệt thật: chọn chữ → In đậm/Link/H2/danh sách; gõ tiếng Việt có dấu (Telex/VNI) không rớt/đôi chữ; dán từ Word/Google Docs thấy chỉ còn định dạng cho phép; chọn khối khác rồi quay lại thấy nội dung giữ nguyên; trang System: editor `content` sửa được.
- F11: kéo khung crop trên trình duyệt thật (chuột + cảm ứng): khung đúng tỉ lệ preset, không tràn ảnh; upload xong ảnh crop theo vùng đã chọn; preset `320x96,inside` (logo) không thấy khung.
