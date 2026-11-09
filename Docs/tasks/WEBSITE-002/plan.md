# WEBSITE-002 — Operations Engine + editor Portal (Bước 5b, frontend)

> **Đã chốt 2026-10-21 — chưa code**
>
> **Lane C.** Nửa FE của WEBSITE-001 (BE + Gate 1 đã xong, contract `website.v1`/`media.v1` đã promote).
> Tạo package mới `packages/builder-core`, 4 màn mới ở Portal. **Không chạm contract** → không có Gate 1
> mới; thiếu field giữa chừng → vòng `WEBSITE-001-D{n}` (brief "Ràng buộc"). Cổng: Gate 2.
>
> **Người thực thi dùng skill:** `superpowers:subagent-driven-development` hoặc
> `superpowers:executing-plans`. Mỗi bước có checkbox `- [ ]`.

**Mục tiêu:** Chủ shop (Owner) tạo website, quản lý danh sách trang, sửa thuộc tính trang, dựng nội dung
trang bằng kéo-thả + Inspector (autosave, undo/redo trong phiên), chỉnh theme — reload vẫn còn nguyên.
**Bàn giao BE:** [`brief.md`](brief.md) · **Spec:** `05` §2–§6, §11 · `step.md` 5b
· **Quyết định liên quan:** #14, #20, #23, #33, #42, #43, #94, #97, #100, #101

## 1. Yêu cầu (đã chốt với người)
- Người dùng / vai trò: **chỉ Owner** của shop (#100). Role khác: ẩn UI Website + Media Library.
- Điểm chạm: Portal — tạo website · danh sách trang · thuộc tính trang · editor (canvas + Inspector +
  autosave) · Theme panel. API: 10 endpoint `website.v1` (brief bảng đầu).
- Xong nghĩa là (`step.md` 5b): dựng được một trang từ đầu bằng kéo-thả, reload thì còn nguyên,
  undo/redo chạy trong phiên. Acceptance của brief.
- Tiên quyết `step.md` 5b "refresh token sang cookie httpOnly": **người đã làm** (#101, IDENTITY-002) —
  không thuộc plan này.
- **Không làm trong task này:** (brief "Không thuộc phạm vi") publish/rollback, `NavigationConfig`,
  translation, sửa `Locales`/SEO mặc định của Website, enforce `requiresProps`, nới quyền Manager/Staff,
  mọi thứ trong `backend/`, `contracts/`, `config/`.

## 2. Hiện trạng (khảo sát 2026-10-18)

| Khả năng | Trạng thái | Ở đâu | Ghi chú |
|---|---|---|---|
| SDK website (10 hook + query key + Zod + MSW) | ✅ có | [website.ts](../../../packages/api-sdk/src/generated/website/website/website.ts) · [index.ts:44-59](../../../packages/api-sdk/src/index.ts#L44-L59) · [mocks.ts:6](../../../packages/api-sdk/src/mocks.ts#L6) | `tree`/`tokens` là `JsonElement {}` → phải kiểu hoá bằng Zod của builder-components |
| MSW website trong test portal | 🟡 | [msw-server.ts:6](../../../apps/portal/src/test/msw-server.ts#L6) | `getWebsiteMock` có nhưng chưa đăng ký |
| Route `/shops/$shopId/website…` | ❌ | — | `$shopId` là route lá, không có `<Outlet/>` ([_authenticated.shops.$shopId.tsx](../../../apps/portal/src/routes/_authenticated.shops.$shopId.tsx)) → phải tách `$shopId.index.tsx` hoặc thêm Outlet |
| Kiểm "là Owner" | ✅ | [shop-role.ts:14-18](../../../apps/portal/src/lib/shop-role.ts#L14-L18) | `isShopOwner(useListShops().data, shopId)`; chuỗi `'Owner'` lấy từ tài liệu, không từ enum contract |
| Ẩn Media UI cho non-Owner | 🟡 | [media-library-picker.tsx:147-152](../../../apps/portal/src/components/media/media-library-picker.tsx#L147-L152) | Nút Xoá đã ẩn; nút **Tải lên** của picker và nút Upload/Library của `ImagePickerControl` **chưa** ẩn |
| Phiên sống qua F5 | ✅ | [session-store.ts:17-49](../../../apps/portal/src/stores/session-store.ts#L17-L49) | Cookie `vsite_rt` (#101); đang sửa chưa commit lúc khảo sát |
| Inspector sinh form từ manifest | ✅ | [Inspector.tsx:38](../../../packages/builder-components/src/inspector/Inspector.tsx#L38) | Nhận `fields, values, onChange(path,value), renderMediaPicker`; control `lexical` là textarea stub (:120), `binding-builder` stub (:330); **không** validate Zod |
| Chọn ảnh từ Inspector | 🟡 | [dev-registry.tsx:122-226](../../../apps/portal/src/routes/dev-registry.tsx#L122-L226) | `ImagePickerControl` nằm trong route dev sẽ xoá ở Bước 5 → chuyển sang `components/` |
| Media Library picker + cảnh báo references | ✅ | [media-library-picker.tsx:127-135](../../../apps/portal/src/components/media/media-library-picker.tsx#L127-L135) | Chỉ hiện **số** nơi đang dùng ([:271-273](../../../apps/portal/src/components/media/media-library-picker.tsx#L271-L273)), không liệt kê `pageTitle` cho `PageDraft` |
| Khung crop theo preset (MEDIA-001 #6) | ❌ | [upload-slot-dialog.tsx:139-144](../../../apps/portal/src/components/media/upload-slot-dialog.tsx#L139-L144) | Mới có focal point |
| Zod vỏ tree + theme tokens | 🟡 | [envelope-schema.ts:169-170](../../../packages/builder-components/meta/envelope-schema.ts#L169-L170) | `treeEnvelopeSchema`, `themeTokensSchema` có nhưng **không export** từ `src/index.ts` |
| Zod props theo type | ✅ | `packages/builder-components/generated/props-schemas.ts` | Không `.strict()` → strip key lạ (không báo `PROP_UNKNOWN`); binding schema không có key `source` — chỉ dùng `safeParse` để kiểm, giữ object gốc |
| Luật cho Operations Engine | ✅ | `packages/builder-components/generated/op-rules.ts` · `generated/tree-rules.json` | `acceptsChildren/allowedChildTypes/allowedInPageKinds/maxPerPage/editableInSystemPageProps`; thiếu `requiresProps` (đúng — ngoài phạm vi) |
| `sanitizeRichText` | 🟡 | [sanitize-html.ts:20](../../../packages/builder-components/src/sanitize-html.ts#L20) | Có, chưa export |
| Kiểm link URL / validate toàn cây phía TS | 🟡 | `packages/builder-components/tests/helpers/reference-tree-validator.ts` (`isSafeLinkUrl` :64, `validateTree` :110, `compareSystemPage` :266) | Chỉ là **test helper**, dùng `ajv` (devDependency) |
| `packages/builder-core` (Operations Engine) | ❌ | — | Không có op type nào trong repo |
| Canvas: render cây + chọn node | 🟡 | [renderTree.tsx:28](../../../packages/builder-renderer/src/renderTree.tsx#L28) | `RenderTree({tree: ComponentNode})` **không render gốc `Page`**; không có hook chọn/viền node |
| Theme → CSS variables | ❌ | [tokens.ts:5-22](../../../packages/theme-engine/src/tokens.ts#L5-L22) · `builder-renderer/src/section/Section01.tsx` | `theme-engine` chỉ có `colorTokens` tĩnh, key **không khớp** `config/theme-tokens.json`; Section01 đặt `style` inline từ token tĩnh |
| Map `error_code` → câu tiếng Việt | 🟡 | [error-messages.ts:5-42](../../../apps/portal/src/lib/error-messages.ts#L5-L42) | Chưa có mã Website/tree |
| Kiểu `ProblemDetails.params` | ❌ | [problem-details.ts:5-13](../../../packages/shared/src/errors/problem-details.ts#L5-L13) | Không có `params`; `errors` là record → cần kiểu riêng cho `params.errors[]` |
| Fixture tree dùng chung | ✅ | `packages/builder-components/tests/fixtures/tree/` (44 file + README) | Mỗi fixture có `expect` + `errorCodes` |
| dnd-kit / lexical / zundo / immer | ❌ | `apps/portal/package.json` | Chưa cài (CLAUDE.md portal: chỉ cài khi task builder bắt đầu — là task này) |
| Component Loading/Error/Empty dùng chung | ❌ | — | Mỗi màn tự viết inline (vd. [_authenticated.shops.index.tsx:37-45](../../../apps/portal/src/routes/_authenticated.shops.index.tsx#L37-L45)) |

### Phát hiện khi khảo sát
- `brief.md` dòng 1 còn tiêu đề "WEBSITE-001 — FE brief" (file đã chuyển sang `WEBSITE-002/`). Vòng D
  vẫn đặt tên `WEBSITE-001-D{n}` theo brief.
- `/dev-registry` nằm **ngoài** `_authenticated` (không guard) và comment ghi "xoá ở Bước 5"
  ([dev-registry.tsx:19](../../../apps/portal/src/routes/dev-registry.tsx#L19)); nợ TOOLING-001 "chặn
  `/dev-registry` ở production".
- `vite.config.ts` portal: `host: 'admin.vsite.local'`, không có `allowedHosts`, proxy `/api` + `/media`
  → `localhost:5270`. Smoke workflow §8 nhắc host `{slug}.vsite.local` — đó là của `apps/web`, Portal
  smoke ở `admin.vsite.local`.
- `theme-engine` `colorTokens` (background, foreground, primary…) lệch key với `config/theme-tokens.json`
  (primary, secondary, background, surface, text, textMuted, border) — nợ TOOLING-001 #1.
- Không có toast; lỗi hiển thị inline `role="alert"`.
- TanStack Table chưa cài dù portal CLAUDE.md ghi là quy ước bảng dữ liệu.
- Component nằm ở `packages/builder-components/src/*` (không phải renderer). Màu nền Section là prop
  `kind: 'color'` (chuỗi tự do, `allowCustom: false`, default `'background'`) — Zod chỉ `z.string()`
  ([props-schemas.ts:31](../../../packages/builder-components/generated/props-schemas.ts#L31)); control
  `color-token` của Inspector liệt kê key `colorTokens` cũ ([Inspector.tsx:202](../../../packages/builder-components/src/inspector/Inspector.tsx#L202)).
- `config/theme-tokens.json` **không có danh mục preset** → `presetKey` chưa có nguồn giá trị.
- `PageDto` có `sortOrder` nhưng contract không có endpoint sắp xếp trang → không làm.
- `MediaLibraryPicker` luôn clone (#71); `ogImageId` của Page phải là bản **Library** (brief "Media") →
  cần chế độ chọn không clone.

## 3. Flow

### Flow 1: Vào mục Website, tạo website
```text
1. Owner mở shop → thấy link "Website"          ❌ route + link; ẩn với non-Owner (isShopOwner ✅ shop-role.ts:14)
2. GET /website                                 ✅ useGetWebsite (website.ts:144)
   404 WEBSITE_NOT_FOUND → empty "Tạo website"   ❌
   lỗi khác → error + Thử lại                    ❌
3. Bấm tạo → POST /website                      ✅ useCreateWebsite (website.ts:87)
   422 WEBSITE_REQUIRES_HOSTED_SHOP → câu giải thích, không retry   ❌
   409 WEBSITE_ALREADY_EXISTS → refetch getWebsite                  ❌
4. Thành công → invalidate getWebsite + listPages → danh sách trang  ❌
Non-Owner gõ URL thẳng → màn "Chỉ chủ shop" (không gọi API)          ❌
```

### Flow 2: Danh sách trang + tạo/xoá trang
```text
1. GET /pages (không phân trang)               ✅ useListPages (website.ts:230) — chưa có website trả []
2. Bảng: title, slug, loại (Trang chủ/System/Composable), bật/tắt, isDirty   ❌
   empty: chỉ có trang chủ + System → gợi ý "Tạo trang"                       ❌
3. Tạo trang → form (slug, title, SEO…)        ❌ → POST /pages (useCreatePage :318)
   409 PAGE_SLUG_TAKEN / 422 PAGE_SLUG_RESERVED|INVALID → lỗi tại ô slug       ❌
   FE tiền-kiểm slug với reserved-routes (portal/src/lib/reserved-routes.ts ✅) + locales
4. Xoá → chỉ hiện khi `deletable`; xác nhận → DELETE (:433); 409 PAGE_NOT_DELETABLE → báo   ❌
5. Mở editor / mở thuộc tính                   ❌
```

### Flow 3: Thuộc tính trang
```text
1. Form từ PageDto (đọc từ listPages)          ❌ PUT /pages/{id} thay toàn bộ (useUpdatePage :377)
2. Khoá theo loại: Trang chủ → slug khoá; System → slug khoá; NotFound → noIndex bật cứng   ❌
   (gửi slug = null = không đổi)
3. OG image: chọn ảnh Library (không clone)    🟡 MediaLibraryPicker cần chế độ "chọn bản Library"
4. 409 CONCURRENCY_CONFLICT → báo + tải lại; 422 slug/PAGE_*_FIXED/PAGE_NOINDEX_REQUIRED → tại ô  ❌
```

### Flow 4: Mở trang & sửa (editor)
```text
1. GET draft → Zod treeEnvelopeSchema           🟡 schema có, chưa export (envelope-schema.ts:169)
   parse hỏng → error "Dữ liệu trang không đọc được", không cho sửa                       ❌
   cây rỗng (trang chủ mới) → canvas empty "Kéo khối vào đây"                              ❌
2. Canvas render children của gốc Page, click chọn node, viền node chọn   🟡 RenderTree không render gốc, không có hook chọn
3. Kéo từ palette / kéo đổi chỗ / xoá           ❌ dnd-kit + Operations Engine (add/move/remove)
   → engine kiểm [1]–[5] 05 §11 bằng opRules; vi phạm → không áp, báo lý do
4. Inspector sửa prop → op `update`            🟡 Inspector có (Inspector.tsx:38), chưa nối op
   System page: chỉ prop trong editableInSystemPageProps sửa được, còn lại disabled; không palette/kéo/xoá
5. Undo/redo (Ctrl+Z / Ctrl+Shift+Z) trong phiên (#42, zundo)            ❌
```

### Flow 5: Autosave
```text
1. Cây đổi → debounce ~2s → tiền-kiểm 1 MB / sâu 20 / 2000 node → PUT draft {tree, version}   ❌
   vượt giới hạn → không gửi, banner "Trang quá lớn"
2. 200 → giữ version mới; sanitized=true → GET draft, thay cây (xoá lịch sử undo)              ❌
3. 409 DRAFT_CONFLICT → dừng autosave, dialog "Có bản mới hơn" (hành vi: xem §4)               ❌
4. 422 TREE_INVALID|… → params.errors[].path (JSON Pointer) → tô đỏ node/ô Inspector;
   path gốc → banner chung; truncated → "còn lỗi khác"                                          ❌
5. 413 TREE_BODY_TOO_LARGE → banner; lỗi mạng/5xx → giữ dirty, thử lại khi đổi tiếp/nút Thử lại ❌
6. Chỉ báo trạng thái: "Đã lưu / Đang lưu / Chưa lưu / Lỗi"; rời trang khi còn chưa lưu → cảnh báo ❌
```

### Flow 6: Theme
```text
1. GET /theme → Zod themeTokensSchema         ✅ useGetTheme (:642) · 🟡 schema chưa export
2. Panel: 7 màu, font heading/body, scale, radius, shadow, sectionGap (giá trị từ config/theme-tokens.json)  ❌
3. Sửa → canvas đổi ngay qua CSS variables     ❌ theme-engine tokensToCssVars + component đọc var(--vs-*)
4. Lưu → PUT /theme (last-write-wins, không version); 422 THEME_INVALID → tô đỏ theo path; 413   ❌
```

### Flow 7: Xoá ảnh đang dùng trong trang
```text
1. Picker xoá ảnh → GET references             ✅ media-library-picker.tsx:127-135
2. Liệt kê nơi dùng: ShopLogo → "Logo shop", PageDraft → "Trang {pageTitle}"   ❌ hiện chỉ có số (picker :271-273) → F2
```

## 4. Đã chốt trong session lập plan
| # | Chốt |
|---|---|
| — | Tiên quyết refresh token cookie đã xong (#101) — không thuộc plan |
| — | Lexical (control RichText) + khung crop theo preset (MEDIA-001 #6) **trong** 5b, là hai task cuối; cắt được nếu phình |
| — | Theme qua CSS variables **trong** 5b: `theme-engine` có hàm thuần `tokensToCssVars`, component đọc `var(--vs-*)` — trả nợ TOOLING-001 #1 |
| — | FE kiểm theo **từng op** ([1]–[5] `05` §11, dùng `opRules`) + chặn giới hạn kích thước trước khi gửi. **Không** chép `validateTree` đầy đủ sang runtime — BE là nguồn sự thật, 422 tô đỏ theo `path`. `isSafeLinkUrl` chuyển từ test helper sang `src` (test helper import lại) |
| — | `409 DRAFT_CONFLICT` → dừng autosave, dialog hai lựa chọn: **"Tải bản mới"** (bỏ thay đổi của tab này) hoặc **"Giữ bản của tôi"** (xác nhận → `GET draft` lấy `version` mới → `PUT` cây local). **Không** PUT bằng `params.currentVersion` — version luôn lấy nguyên văn từ `GET`/`PUT` gần nhất (brief) |
| — | Theme: preview sống trên canvas, nút **"Lưu theme"** mới `PUT`; "Hoàn tác" về bản đã lưu. Không vào lịch sử undo của tree. `presetKey` gửi lại nguyên giá trị đang có (config chưa có danh mục preset) |
| — | Xoá `/dev-registry` trong task này; `ImagePickerControl` + test chuyển sang `apps/portal/src/components/` |
| — | Màn: `/shops/$shopId/website` (tổng quan + danh sách trang), `/shops/$shopId/website/pages/$pageId` (editor, Theme panel là tab trong editor). Non-Owner: ẩn link, gõ URL thẳng → màn "Chỉ chủ shop" không gọi API |
| — | Canvas dùng `builder-renderer` thật: thêm **prop tuỳ chọn** `decorateNode` + cho `RenderTree` nhận gốc `Page` (additive; renderer vẫn không import `builder-core`, vẫn isomorphic). Bước 6 chốt API cuối |
| — | Không cấp số `#N` mới — mọi điểm trên là chi tiết thực thi trong khung #14/#20/#42 |

## 5. Trình tự session
Chỉ **S2 (FE)** — BE (S1) và Gate 1 đã xong ở WEBSITE-001. F1 → F11 tuần tự trong một nhánh
`feature/WEBSITE-002`. **Smoke app thật ngay sau F1** (workflow §8). F3–F5 là nền packages, phải xong
trước F6. Contract thiếu giữa chừng → dừng, vòng `WEBSITE-001-D{n}`.

## 6. Ràng buộc toàn cục
- **Ghi:** `apps/portal/**`, `packages/{builder-core (mới), builder-components (trừ generated/ và registry/),
  builder-renderer, theme-engine, shared, ui}`. **Cấm:** `backend/`, `contracts/`, `config/`,
  `packages/api-sdk/src/generated/**`, `packages/builder-components/generated/**`. **Không sửa manifest**
  (sửa = đổi contract Registry, cần người duyệt).
- `builder-renderer` không import `builder-core`, không `window`/`document` trong render (#23).
  `builder-core` chỉ portal dùng; `packages/` không import `apps/`.
- **State #20:** server state (website, pages, draft *gốc*, theme *đã lưu*) ở TanStack Query; cây đang
  sửa + selection + theme đang sửa ở Zustand (`builder-core`) + zundo. Không copy Query → Zustand ngoài
  **một** lần khởi tạo phiên editor từ draft (và lúc nạp lại sau `sanitized`/409) — đó là "mở bản nháp
  để sửa", không phải mirror; ghi rõ ở `builder-core/CLAUDE.md`.
- Enum: dùng `const object + union` của Orval (`PageKind`, `MediaReferenceKind`), không `enum` TS (#19).
- **Tenant:** `shopId` chỉ từ route param, không lấy từ dữ liệu khác; không bao giờ gửi `shopId` trong body.
- `version` opaque: không so sánh/tăng, gửi lại nguyên văn.
- Không bao giờ có binding `Review` trong cây (engine chặn ở [5]).
- `tree`/`tokens` luôn qua Zod của `builder-components` ở biên (response) — không tin type Orval.
- Mọi màn: loading · error · empty. Lỗi map từ `error_code` (`apps/portal/src/lib/error-messages.ts`).
- Test: Vitest + MSW, mock **dùng fixture** `packages/builder-components/tests/fixtures/tree/*.json`
  (import trực tiếp, không chép).

## 7. Review focus — lỗi dễ lọt khi spec im lặng
| # | Tình huống | Hành vi đúng | Test ở |
|---|---|---|---|
| R1 | Autosave đang bay, người tiếp tục sửa; response về sau | Không ghi đè cây local bằng response; chỉ cập nhật `version`; còn thay đổi mới → lưu tiếp với version mới. Không bao giờ hai PUT song song | F8 |
| R2 | `sanitized: true` trong khi người đã sửa tiếp sau lần gửi | Nạp lại draft chỉ khi không có thay đổi mới; có thì lưu tiếp, nạp lại ở lần sạch kế tiếp (không đè việc đang làm) | F8 |
| R3 | System page: kéo-thả, xoá, đổi variant, sửa prop không thuộc `editableInSystemPageProps` | UI không cho; engine từ chối op (kể cả gọi thẳng); chỉ `update` prop được phép | F4, F6 |
| R4 | Non-Owner (Manager/Staff) mở thẳng URL `/website`… hoặc picker Media | Không thấy link/nút; route hiện "Chỉ chủ shop", **không** phát request website | F1 |
| R5 | 422 `path` trỏ node đã bị xoá/di chuyển sau lần gửi | Bỏ qua highlight không tìm thấy, vẫn hiện banner + mã lỗi; lỗi xoá khi lưu thành công kế tiếp | F8 |
| R6 | Cây vượt 1 MB / sâu 20 / 2000 node; theme > 64 KB | Không gửi, banner rõ; undo được để quay lại | F4, F8, F9 |
| R7 | Binding `Review` hoặc link `javascript:` đi qua Inspector/AI op | Engine từ chối op `update` ([5], `isSafeLinkUrl`) | F4 |
| R8 | 409 → "Giữ bản của tôi" | Lấy `version` qua `GET draft` mới, không dùng `params.currentVersion` | F8 |

## 8. Sơ đồ file
```text
packages/builder-core/                       (mới)
  CLAUDE.md · package.json · tsconfig.json · vitest.config.ts
  src/index.ts
  src/operations/types.ts      Operation union + Zod (add/remove/move/update/changeTheme)
  src/operations/validate.ts   kiểm [1]–[5] 05 §11 bằng opRules + props-schemas + isSafeLinkUrl
  src/operations/apply.ts      apply bằng immer
  src/tree/query.ts            findNode, parentOf, path → JSON Pointer, nodeIdFromPointer
  src/tree/limits.ts           byteSize/depth/count (1 MB, 20, 2000) + theme 64 KB
  src/tree/new-node.ts         tạo node mới (id ^c_…, default props từ manifest)
  src/store/editor-store.ts    Zustand + zundo (partialize: chỉ tree)
  src/*.test.ts
packages/builder-components/src/index.ts     + export envelope-schema, sanitizeRichText, opRules, link-url
packages/builder-components/src/link-url.ts  isSafeLinkUrl (chuyển từ tests/helpers)
packages/builder-components/src/*/*.tsx      màu/radius/font qua var(--vs-*)
packages/builder-components/src/inspector/Inspector.tsx   color-token theo colorKeys; prop `invalidPaths`; lexical control (F10)
packages/builder-renderer/src/renderTree.tsx  decorateNode? + nhận gốc Page
packages/theme-engine/src/css-vars.ts         tokensToCssVars(tokens) thuần
packages/shared/src/errors/problem-details.ts + TreeValidationParams, getProblemParams, DraftConflictParams
apps/portal/src/routes/_authenticated.shops.$shopId.tsx            → tách: $shopId.index.tsx + layout Outlet
apps/portal/src/routes/_authenticated.shops.$shopId.website.index.tsx
apps/portal/src/routes/_authenticated.shops.$shopId.website.pages.$pageId.tsx
apps/portal/src/routes/dev-registry.tsx + -dev-registry.test.tsx   (xoá)
apps/portal/src/components/website/*   owner-only-gate, website-overview, page-list, page-form-dialog,
                                       editor-shell, editor-canvas, component-palette, autosave-status,
                                       conflict-dialog, validation-banner, theme-panel
apps/portal/src/components/media/image-picker-control.tsx          (chuyển từ dev-registry)
apps/portal/src/components/media/media-library-picker.tsx          + chế độ chọn Library không clone; ẩn Tải lên non-Owner
apps/portal/src/components/media/crop-frame.tsx                    (F11)
apps/portal/src/hooks/use-autosave.ts
apps/portal/src/lib/error-messages.ts                              + mã Website/tree/theme
apps/portal/src/test/msw-server.ts                                 + getWebsiteMock
apps/portal/src/test/website-handlers.ts                           handler dựng từ fixture
```

## 9. Session S1: Backend
Không có — xong ở WEBSITE-001.

## 10. Session S2: Frontend

### F1: Route Website + chặn non-Owner + tạo website + danh sách trang
Tách `$shopId` thành layout có `<Outlet/>` + `$shopId.index.tsx` (giữ nguyên màn sửa shop). Component
`OwnerOnlyGate({shopId})` dùng `isShopOwner`; non-Owner → "Chỉ chủ shop", không mount con (không gọi
API). Link "Website" ở màn shop chỉ với Owner. Ẩn nút "Tải ảnh lên thư viện" của `MediaLibraryPicker`
khi `!isOwner`. Tổng quan: `getWebsite` → 404 `WEBSITE_NOT_FOUND` = empty + nút tạo; 422
`WEBSITE_REQUIRES_HOSTED_SHOP` giải thích; 409 → refetch. Danh sách trang chỉ đọc ở F1 (bảng thường —
không cài TanStack Table cho một bảng không phân trang; ghi giả định).
- [ ] Test trước: `apps/portal/src/routes/-website.test.tsx` (tạo mới) — Owner thấy link; Manager không thấy link
  và mở URL thẳng → "Chỉ chủ shop" + **không** có request `/website` (MSW `onUnhandledRequest:'error'`
  + spy); 404 → empty + tạo → danh sách; 422 hosted; lỗi 500 → error + Thử lại; loading; danh sách chỉ
  trang chủ + System → empty gợi ý; badge loại trang/isDirty/isEnabled
- [ ] `media-library-picker.test.tsx`: non-Owner không thấy nút Tải lên
- [ ] `msw-server.ts` + `getWebsiteMock()`; `test/website-handlers.ts` (website/pages mặc định hợp lệ)
- [ ] `error-messages.ts` + mã: `WEBSITE_*`, `PAGE_*`, `DRAFT_CONFLICT`, `TREE_*`, `THEME_*`,
  `RICHTEXT_UNSAFE`, `LINK_URL_UNSAFE`, `MEDIA_INVALID_IMAGE_REFERENCE`, `SCHEMA_VERSION_UNSUPPORTED`,
  `CONCURRENCY_CONFLICT` (+ test bảng mã)
- [ ] FAIL → hiện thực → PASS (`pnpm --filter @vsite/portal test`)
- [ ] **Smoke app thật:** BE chạy (`backend/CLAUDE.md` "Lệnh hay dùng"), `pnpm --filter @vsite/portal dev`,
  `admin.vsite.local:5173`, đăng nhập Owner, F5 không mất phiên, mở `/shops/{id}/website`, tạo website,
  thấy 3 trang (Trang chủ, Contact, NotFound). Ghi kết quả vào `changelog.md`
- [ ] Commit `feat(portal): route Website, chặn non-Owner, tạo website + danh sách trang (WEBSITE-002 F1)`

### F2: Tạo / sửa / xoá trang
`PageFormDialog` (RHF + Zod `createPageBody`/`updatePageBody` từ `website.zod.ts`). Slug: tiền-kiểm
reserved-routes (`apps/portal/src/lib/reserved-routes.ts`) + mã locale (`config/locales.json`), BE vẫn là
nguồn sự thật. Khoá: trang chủ/System → slug disabled, gửi `slug: null`; NotFound → `noIndex` bật cứng.
OG image: `MediaLibraryPicker` thêm `mode: 'library'` (trả bản Library, không clone); hiển thị qua
`getAssetsByIds`. Xoá: nút chỉ khi `deletable`, dialog xác nhận.
- [ ] Test trước: `page-form-dialog.test.tsx` — tạo OK → invalidate listPages; 409 `PAGE_SLUG_TAKEN` /
  422 `PAGE_SLUG_RESERVED` hiện ở ô slug; slug reserved bị chặn client; trang chủ slug khoá + gửi null;
  NotFound noIndex khoá; 409 `CONCURRENCY_CONFLICT` → báo + tải lại; `PAGE_NOINDEX_REQUIRED`
- [ ] `media-library-picker.test.tsx`: `mode:'library'` không gọi clone, trả id Library
- [ ] Xoá: không có nút khi `deletable=false`; 409 `PAGE_NOT_DELETABLE` → báo
- [ ] Cảnh báo xoá ảnh (Flow 7): picker liệt kê từng reference — `ShopLogo` "Logo shop", `PageDraft`
  "Trang {pageTitle}" (dùng `MediaReferenceKind`, không so chuỗi tay) — test trong `media-library-picker.test.tsx`
- [ ] FAIL → PASS · Commit `feat(portal): tạo/sửa/xoá trang (WEBSITE-002 F2)`

### F3: Nền packages — export schema + kiểu lỗi có cấu trúc
- `builder-components/src/index.ts` export `treeEnvelopeSchema`, `themeTokensSchema`, `NODE_ID_PATTERN`,
  `sanitizeRichText`, `opRules`, `isSafeLinkUrl`; tạo `src/link-url.ts`, `tests/helpers/reference-tree-validator.ts`
  import lại từ đó (một nguồn). (`sanitize-html` đã là `dependencies` runtime — `package.json:18`.)
- `shared`: `TreeValidationError {path, code, message}`, `TreeValidationParams {errors, truncated}`,
  `DraftConflictParams {currentVersion, updatedAt, updatedByUserId}`, `getProblemParams<T>(err, guard)`
  (type guard, không cast mù). Thêm `params?: unknown` vào `ProblemDetails`.
- [ ] Test trước: `builder-components/tests/public-exports.test.ts` (export tồn tại; `isSafeLinkUrl` qua
  hết ca trong `fixtures/tree` nhóm link); `shared/src/errors/problem-details.test.ts` (params hợp lệ/hỏng/thiếu)
- [ ] `pnpm --filter @vsite/builder-components test` (fixture cũ vẫn xanh) · `pnpm gen:registry` không đổi `generated/`
- [ ] Commit `feat(builder-components,shared): export schema tree/theme + kiểu params lỗi (WEBSITE-002 F3)`

### F4: `packages/builder-core` — Operations Engine
Cài `immer`, `zundo`, `zustand`. 5 op (tên theo AI tool schema để AI tái dùng sau):
`add {parentId, index, node}`, `remove {nodeId}`, `move {nodeId, toParentId, toIndex}`,
`update {nodeId, props (patch theo path)}`, `changeTheme {tokens}`. `validate(op, ctx{tree, pageKind,
systemSnapshot?})` → `{ok} | {ok:false, code, message}` dùng **đúng mã con** của fixture README
(`NODE_NOT_ALLOWED_IN_PAGE_KIND`, `NODE_CHILDREN_NOT_ACCEPTED`, `NODE_CHILD_TYPE_NOT_ALLOWED`,
`NODE_MAX_PER_PAGE_EXCEEDED`, `PROP_INVALID`, `BINDING_SOURCE_NOT_ALLOWED`, `LINK_URL_UNSAFE`,
`SYSTEM_PAGE_STRUCTURE_CHANGED`, `SYSTEM_PAGE_PROP_NOT_EDITABLE`, `THEME_TOKEN_INVALID`). Thêm mã chỉ-FE
`TARGET_NOT_FOUND` (không gửi BE). Props kiểm bằng `safeParse` của `props-schemas` (giữ object gốc —
schema strip key). Store: `{tree, pageKind, selectedId, themeDraft}`; zundo `partialize` **chỉ `tree`**,
giới hạn 100 bước; `loadDraft()` xoá lịch sử. `limits.ts`: `measureTree` (byte UTF-8 của JSON, độ sâu,
số node), theme 64 KB.
- [ ] Test trước (thuần TS, chạy được ở Node): mỗi op apply + từ chối theo bảng `05` §11 (System: add/remove/move ❌,
  update chỉ prop editable); maxPerPage Hero=1; RichText vào System OK, Hero vào System ❌; Section nhận con,
  Hero không; `Review` bị chặn; link `javascript:` bị chặn; `move` vào chính con cháu của nó ❌; id mới
  khớp `NODE_ID_PATTERN`; undo/redo khôi phục đúng cây, `themeDraft` không vào lịch sử; giới hạn 2000 node/sâu 20/1 MB;
  **mọi fixture `expect:"valid"`** dựng lại được bằng chuỗi op `add` từ cây rỗng (cùng pageKind)
- [ ] `CLAUDE.md` của package: checklist 6 chỗ khi thêm op (union · Zod · apply · undo (zundo — không có
  `invert()` vì #42 chụp trạng thái) · AI tool schema · test) — ghi rõ lệch "invert" so với workflow §11 vào changelog
- [ ] ESLint `no-restricted-imports` ở `builder-renderer` chặn `@vsite/builder-core` (một phần nợ TOOLING-001)
- [ ] FAIL → PASS (`pnpm --filter @vsite/builder-core test`) · Commit `feat(builder-core): Operations Engine 5 op + store zundo (WEBSITE-002 F4)`

### F5: Theme qua CSS variables
`theme-engine/src/css-vars.ts`: `tokensToCssVars(tokens): Record<\`--vs-${string}\`, string>` (thuần,
isomorphic) — màu `--vs-color-{key}`, font `--vs-font-heading|body`, radius/shadow/sectionGap/scale →
giá trị CSS qua bảng ánh xạ cố định; `defaultThemeVars` từ `config/theme-tokens.json` `defaults`. 5
component đọc `var(--vs-…, fallback)`; Section `background` = key màu → `var(--vs-color-{key})` (key lạ →
`background`). Inspector `color-token` liệt kê `colorKeys` của config. Giữ export `colorTokens` cũ cho
tới khi không còn ai dùng rồi xoá.
- [ ] Test trước: `theme-engine/src/css-vars.test.ts` (defaults → đủ biến; mọi giá trị enum có ánh xạ);
  `builder-renderer` test `renderToString` có `var(--vs-color-…)`, không đụng `window` (SSR)
- [ ] `pnpm -r test` xanh (renderer + components snapshot cập nhật có chủ đích)
- [ ] Commit `feat(theme-engine): tokens → CSS variables, component đọc var (WEBSITE-002 F5, TOOLING-001 #1)`

### F6: Khung editor — nạp draft, canvas, chọn, Inspector, undo/redo
Route editor bọc `OwnerOnlyGate`. `getPageDraft` → `treeEnvelopeSchema.safeParse` (hỏng → error, không
cho sửa) → `store.loadDraft`. `RenderTree` thêm `decorateNode?: (node, rendered) => ReactNode` và render
`children` khi gốc `type:'Page'` (giữ hành vi cũ khi truyền node thường). Canvas bọc node bằng
`data-node-id`, click chọn, viền chọn; theme đang sửa áp qua `style={tokensToCssVars(themeDraft)}` ở
khung canvas. `mediaMap` dựng từ `getAssetsByIds` (id ảnh trong cây). Inspector của node chọn →
op `update`; prop không editable ở System → disabled. Toolbar Undo/Redo + Ctrl+Z / Ctrl+Shift+Z / Ctrl+Y
(bỏ qua khi focus trong ô nhập). Empty canvas: "Trang trống — thêm khối từ bảng bên trái". Chuyển
`ImagePickerControl` sang `components/media/`, xoá `/dev-registry` + link DEV.
- [ ] Test trước: `-editor.test.tsx` — loading/error (draft hỏng Zod, 404 `PAGE_NOT_FOUND`)/empty; chọn
  node → Inspector đúng field; sửa title → canvas đổi; undo/redo; System page (fixture system) Inspector
  chỉ bật `content`; `renderTree.test.tsx` — `decorateNode` được gọi mỗi node, gốc Page render children,
  không truyền `decorateNode` thì output y như cũ
- [ ] FAIL → PASS · Commit `feat(portal): editor — nạp draft, canvas chọn node, Inspector, undo/redo (WEBSITE-002 F6)`

### F7: Kéo-thả
Cài `@dnd-kit/core` + `@dnd-kit/sortable`. Palette từ `registryMap`/manifest (lọc theo
`allowedInPageKinds` của trang; ẩn hẳn với System). Kéo palette → `add`; kéo node → `move` (cùng cha / vào
Section / ra ngoài); nút Xoá + phím Delete → `remove`. Vùng thả không hợp lệ hiện "không thả được" (gọi
`validate` trước khi thả). Nút "Lên/Xuống" cho bàn phím (a11y) dùng cùng op `move`.
- [ ] Test trước: chuỗi op từ giao diện (dùng nút Lên/Xuống + "Thêm vào" thay cho mô phỏng drag trong
  jsdom); Hero thứ hai bị chặn kèm lý do; System không có palette; xoá node đang chọn → bỏ chọn
- [ ] FAIL → PASS · Commit `feat(portal): kéo-thả thêm/di chuyển/xoá khối (WEBSITE-002 F7)`

### F8: Autosave
`useAutosave` (portal): subscribe `tree`, debounce 2000 ms, **một** request tại một thời điểm (R1);
`measureTree` vượt → không gửi + banner (R6). 200 → cập nhật `version`, `isDirty`; `sanitized` → nạp lại
(R2). 409 `DRAFT_CONFLICT` → dừng, `ConflictDialog` hiện `params.updatedAt` (R8). 422 → `TreeValidationParams`
→ map JSON Pointer → nodeId + prop path → canvas viền đỏ + Inspector ô đỏ (prop `invalidPaths` của
Inspector); gốc → `ValidationBanner`; `truncated` → "còn lỗi khác" (R5). 413 → banner. Mạng/5xx → giữ
"Chưa lưu", nút Thử lại. Trạng thái `AutosaveStatus`. `beforeunload` + router blocker khi còn chưa lưu.
- [x] Test trước (fake timers + MSW): debounce gộp nhiều sửa thành 1 PUT; PUT gửi đúng `version` lần
  trước; sửa trong lúc PUT bay → PUT thứ hai sau, version mới; `sanitized` → GET lại; 409 → dialog, hai
  nhánh (Tải bản mới / Giữ bản của tôi → GET rồi PUT); 422 dùng **fixture invalid** (`errorCodes` → MSW trả
  `params.errors`) tô đúng node; path không tồn tại không crash; 413; vượt 2000 node không gửi
- [x] FAIL → PASS · Commit `feat(portal): autosave draft — 409/422/sanitized/413 (WEBSITE-002 F8)`

### F9: Theme panel
Tab "Giao diện" trong editor. `getTheme` → `themeTokensSchema.safeParse` → `themeDraft` (op
`changeTheme`). Control lấy tập giá trị từ `config/theme-tokens.json`. Canvas đổi ngay. "Lưu theme" →
`updateTheme {presetKey (giữ nguyên), tokens}`; "Hoàn tác" → về bản đã lưu; rời tab còn chưa lưu → nhắc.
422 `THEME_INVALID` → tô đỏ theo `params.errors[].path`; 413 `THEME_BODY_TOO_LARGE`.
- [x] Test trước: loading/error/404 `WEBSITE_NOT_FOUND`; đổi màu → biến CSS canvas đổi; lưu gửi body đúng;
  422 tô ô; màu sai pattern bị chặn client
- [x] FAIL → PASS · Commit `feat(portal): Theme panel — preview CSS vars + lưu (WEBSITE-002 F9)`

### F10: Lexical cho RichText
Cài `lexical` + `@lexical/react` + `@lexical/html` (vào `builder-components` vì Inspector ở đó — lazy load
để không vào bundle renderer của `apps/web`). Control `lexical` theo `profile` của prop
(`inline`/`basic`): toolbar chỉ bật thẻ profile cho phép; xuất HTML → `sanitizeRichText(html, profile)` →
op `update`. Kiểm nội dung trước đó: HTML cũ nạp vào Lexical không mất định dạng cho phép.
- [ ] Test trước: các vector `xss-richtext.json` dán vào → HTML ra không chứa `mustNotContain`; profile
  `inline` không có heading/list; round-trip HTML hợp lệ
- [ ] Kiểm `apps/web` build không kéo lexical (`pnpm --filter @vsite/web build` + tìm trong bundle)
- [ ] FAIL → PASS · Commit `feat(builder-components): control Lexical cho RichText (WEBSITE-002 F10)`

### F11: Khung crop theo tỉ lệ preset (MEDIA-001 #6)
Trong `UploadSlotDialog` / bước clone của picker: khung crop tỉ lệ của preset (bảng từ
`generated/derivative-presets.json`), kéo khung → tính focal point (contract upload/clone chỉ nhận focal
point — **không** có tham số crop; nếu cần crop thật → dừng, vòng D). Preset `fit: inside` → không khung.
- [ ] Test trước: tỉ lệ khung đúng preset; kéo khung → focal point gửi đi đúng tâm; `inside` không khung
- [ ] FAIL → PASS · Commit `feat(portal): khung crop theo preset ở dialog ảnh (WEBSITE-002 F11, MEDIA-001 #6)`

## 11. Đóng task (trước Gate 2)
- [ ] `pnpm build && pnpm test` toàn repo; `pnpm gen:api` (lock khớp) + `pnpm gen:registry` không diff
- [ ] Integration tắt MSW, API thật: chạy lại Flow 1–7 bằng tay (2 tab cùng trang → tab cũ 409)
- [ ] `changelog.md` (lệch thiết kế: invert → zundo, canvas decorator, mã `TARGET_NOT_FOUND` chỉ-FE…) +
  cập nhật `00-INDEX` §3 (5b), §4 (trả TOOLING-001 #1 phần CSS vars, MEDIA-001 #6), banner `05`/`step.md`
- [ ] `change-reviewer` → `review.md` (kèm "Ma sát quy trình" sau Gate 2)
- [ ] `pnpm check:docs` PASS

## 12. Giả định tôi đã tự đặt (chưa hỏi, cần duyệt)
| # | Giả định | Vì sao | Nếu sai thì |
|---|---|---|---|
| G1 | Danh sách trang dùng bảng thường, không cài TanStack Table | Không phân trang/sắp xếp/lọc, ≤ vài chục trang | Thêm ở task sau, không đổi dữ liệu |
| G2 | Undo/redo chụp trạng thái (zundo) thay cho `invert()` từng op | #42 chỉ định zundo; workflow §11 ghi `invert()` từ trước #42 | Thêm `invert()` khi cần op-log/AI giải thích |
| G3 | Mã `TARGET_NOT_FOUND` chỉ dùng trong FE (không gửi BE) | `05` §11 bước [3] không có mã con tương ứng trong README | Đổi tên nếu muốn khớp bộ mã BE |
| G4 | `isSafeLinkUrl` chuyển vào `src` của `builder-components` (không phải `builder-core`) | Inspector/link-picker cũng cần, và renderer có thể cần sau | Chuyển sang `builder-core` |
| G5 | Lexical vào `builder-components` (lazy) | Inspector nằm ở đó | Tách Inspector khỏi package model (nợ TOOLING-001) |
| G6 | Key màu cũ (`muted`, `foreground`…) trong draft dev cũ → fallback `background` | Chưa có dữ liệu thật | Thêm bảng ánh xạ key cũ → mới |
| G7 | Sau `sanitized`/409 "Tải bản mới" → xoá lịch sử undo | Cây nền đổi, undo về trạng thái cũ sẽ đè bản sạch | Giữ lịch sử, cảnh báo |
| G8 | Smoke Portal ở `admin.vsite.local:5173` (không phải `{slug}.vsite.local`, là host của `apps/web`) | `vite.config.ts` portal | Thêm `allowedHosts` nếu cần |

## 13. Verification tổng
- `pnpm build && pnpm test` xanh; `pnpm --filter @vsite/builder-core test` có ca cho mọi dòng bảng `05` §11.
- Thủ công (API thật): tạo website → thêm trang → kéo Hero + Section(RichText) → F5 còn nguyên → undo/redo
  → đổi màu primary thấy canvas đổi → lưu theme → F5 còn; 2 tab cùng trang → tab cũ 409 → dialog; Manager
  không thấy Website.
- Acceptance brief: 7/7 mục đánh dấu.
