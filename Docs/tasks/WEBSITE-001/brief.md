# WEBSITE-001 — FE brief (Bước 5b: editor)

## Contract
- `contracts/openapi/website.v1.json` (sha256 `f1a10a97820618ece7b46345d567b3816b06bda5edd8a15878342edd42a5e557`) — module mới, 10 endpoint.
- `contracts/openapi/media.v1.json` (sha256 `4c1c8b552628b064009917e8544fb51662f469355a2598a36ad3746d6027d3f8`) — đổi: `MediaReferenceKind` thêm
  `PageDraft`; `MediaReferenceDto` thêm `pageId?`/`pageTitle?`; **mọi route chỉ Owner** (403 `MEDIA_OWNER_REQUIRED`).
- Duyệt: `luongpham` (Gate 1, 6 câu ở `contract-diff.md`). Lệch so thiết kế + mã lỗi: `changelog.md`. Vận hành BE: `backend/docs/modules/website.md`.

Tất cả dưới `/api/shops/{shopId}/website`. **Mọi** endpoint (đọc lẫn ghi) chỉ `Owner`; lỗi chung 401/403 (ProblemDetails, `error_code`).

| operationId | Method + path | Request | Response | Lỗi |
|---|---|---|---|---|
| `createWebsite` | `POST /website` | — | 201 `WebsiteDto` (có `homePageId`) | 404 `SHOP_NOT_FOUND`, 409 `WEBSITE_ALREADY_EXISTS`, 422 `WEBSITE_REQUIRES_HOSTED_SHOP` |
| `getWebsite` | `GET /website` | — | 200 `WebsiteDto` | 404 `WEBSITE_NOT_FOUND` |
| `listPages` | `GET /pages` | — | 200 `PageDto[]` (không phân trang) | — (chưa có website → `200 []`) |
| `createPage` | `POST /pages` | `CreatePageRequest` | 201 `PageDto` + `Location` | 404 `WEBSITE_NOT_FOUND`, 409 `PAGE_SLUG_TAKEN`, 422 `PAGE_SLUG_RESERVED`/`PAGE_SLUG_INVALID`/`PAGE_NOINDEX_REQUIRED`/`MEDIA_INVALID_IMAGE_REFERENCE` |
| `updatePage` | `PUT /pages/{pageId}` (**thay toàn bộ**) | `UpdatePageRequest` (`slug = null` = không đổi) | 200 `PageDto` | 404 `PAGE_NOT_FOUND`, 409 `PAGE_SLUG_TAKEN`/`CONCURRENCY_CONFLICT`, 422 `PAGE_SYSTEM_SLUG_FIXED`/`PAGE_HOME_SLUG_FIXED`/slug như trên |
| `deletePage` | `DELETE /pages/{pageId}` | — | 204 | 404 `PAGE_NOT_FOUND`, 409 `PAGE_NOT_DELETABLE` (hoặc dùng cờ `PageDto.deletable` để ẩn nút) |
| `getPageDraft` | `GET /pages/{pageId}/draft` | — | 200 `PageDraftDto` (`tree`, `version`, `isDirty`, `updatedAt`, `page{id,kind,systemType}`) | 404 `PAGE_NOT_FOUND` |
| `savePageDraft` | `PUT /pages/{pageId}/draft` | `{tree, version}` (cả hai `required`) | 200 `{version, isDirty, sanitized}` — **không** trả tree | 404, 409 `DRAFT_CONFLICT`, 413 `TREE_BODY_TOO_LARGE`, 422 `TREE_INVALID`/`RICHTEXT_UNSAFE`/`LINK_URL_UNSAFE`/`MEDIA_INVALID_IMAGE_REFERENCE`/`SCHEMA_VERSION_UNSUPPORTED` |
| `getTheme` | `GET /theme` | — | 200 `{presetKey?, tokens, updatedAt}` | 404 `WEBSITE_NOT_FOUND` |
| `updateTheme` | `PUT /theme` | `{presetKey?, tokens}` (`tokens` `required`) | 200 `ThemeDto` | 413 `THEME_BODY_TOO_LARGE`, 422 `THEME_INVALID` |

Mã `404`/`409`/`422` ở trên là đường BE trả thật; OpenAPI khai theo nhóm status, không theo `error_code` — dựa vào `error_code`, không dựa vào status một mình.

## Quy ước FE phải biết

**Quyền.** Chỉ Owner: 403 `WEBSITE_OWNER_REQUIRED` / `MEDIA_OWNER_REQUIRED`. FE **ẩn** UI Website + Media Library cho role khác
(Manager/Staff/Accountant/Customer), không chờ 403.

**Tạo website.** Phải gọi `POST` tường minh (không tự tạo cùng Shop). `getWebsite` 404 `WEBSITE_NOT_FOUND` → hiện nút "Tạo website".
`listPages` chưa có website trả `200 []` (đừng dùng nó để phát hiện "chưa có website"). Tạo xong: trang chủ (`Slug = ""`) cây rỗng +
System page `Contact`, `NotFound` (mỗi cái seed `Page › RichText`).

**`version` opaque.** Số nguyên (`xmin`); gửi lại **nguyên văn** giá trị lần `GET draft`/`PUT draft` gần nhất, không so sánh/không tăng. Cây y hệt
bản đã lưu có thể giữ nguyên version.

**`409 DRAFT_CONFLICT`.** `params.currentVersion`, `params.updatedAt`, `params.updatedByUserId`; **không** có tree → muốn hoà giải thì `GET draft`.
Theme **không** có concurrency (ghi cuối thắng).

**`PUT draft` thành công.** `sanitized = true` nghĩa là BE đã làm sạch richText nên bản lưu ≠ bản gửi → **tải lại draft** (`GET`) và thay cây trong editor.
richText nguy hiểm (script/iframe/object/embed/style, `on*`, `javascript:`/`data:`/`vbscript:`) bị **từ chối** chứ không làm sạch.

**Lỗi có cấu trúc (422 `TREE_INVALID`, `THEME_INVALID`).** Nằm ở `ProblemDetails.params`, không phải `errors` cấp gốc (từ điển `{field:[msg]}` vẫn
điền theo path, `"$"` cho gốc; FE phải cast kiểu `params`):
`params.errors[] = { path, code, message }`, `params.truncated: boolean` (tối đa **50** lỗi; `truncated = true` → còn lỗi chưa thấy, sửa rồi lưu lại).
`path` là **JSON Pointer** tới node/prop (vd. `/children/2/props/imageId`) → Inspector tô đỏ đúng ô theo `path`; `path` gốc/envelope → banner chung.
Mã con `code` (nguồn tên: `packages/builder-components/tests/fixtures/tree/README.md`, FE dùng **đúng tên**):

| Nhóm | Mã |
|---|---|
| Vỏ / phiên bản | `TREE_ENVELOPE_INVALID`, `SCHEMA_VERSION_UNSUPPORTED` |
| Node | `NODE_TYPE_UNKNOWN`, `NODE_VARIANT_UNKNOWN`, `NODE_ID_DUPLICATE`, `NODE_NOT_ALLOWED_IN_PAGE_KIND`, `NODE_CHILDREN_NOT_ACCEPTED`, `NODE_CHILD_TYPE_NOT_ALLOWED`, `NODE_MAX_PER_PAGE_EXCEEDED` |
| Prop | `PROP_UNKNOWN`, `PROP_INVALID`, `I18N_NOT_ENABLED`, `BINDING_SOURCE_NOT_ALLOWED` (kể cả `Review`) |
| An toàn | `LINK_URL_UNSAFE`, `RICHTEXT_UNSAFE`, `MEDIA_INVALID_IMAGE_REFERENCE` (không lộ id có tồn tại) |
| System page | `SYSTEM_PAGE_STRUCTURE_CHANGED`, `SYSTEM_PAGE_PROP_NOT_EDITABLE` |
| Kích thước | `TREE_BODY_TOO_LARGE` (trong biên 4096 byte → 422), `TREE_TOO_DEEP`, `TREE_TOO_MANY_NODES` |
| Theme | `THEME_TOKEN_INVALID` |

**Giới hạn cây.** Body 1 MB (vượt hẳn 1 MB + 4096 byte → **413** `TREE_BODY_TOO_LARGE`, trong biên → 422 với mã con cùng tên), sâu 20, 2000 node. Theme: 64 KB (413
`THEME_BODY_TOO_LARGE`). FE nên tự chặn trước khi gửi (đừng đợi 413).

**System page** (`page.kind = System`): cấu trúc **khoá** — không thêm/xoá/đổi id/type/variant/thứ tự/cha-con; chỉ sửa prop thuộc
`editableInSystemPageProps` (`packages/builder-components/generated/tree-rules.json`). Không sửa `slug`/`kind`/`systemType`, không xoá (`deletable = false`);
sửa được `title`/SEO/`isEnabled`/`noIndex`. `NotFound`: `slug = null`, bắt buộc `noIndex` (`PAGE_NOINDEX_REQUIRED`). Trang chủ: `isHomePage` (slug rỗng), slug không đổi.
Cấu hình từng System page: `config/system-pages.json`.

**`JsonElement`.** `tree`, `tokens`, `trackingSnippets` ra `{}` (Orval: `interface JsonElement {}`). Kiểu hoá bằng Zod/JSON Schema của `packages/builder-components`
(`tree-envelope`, `theme-tokens`); không dựa type Orval. Body `required` đã khai đủ; vẫn parse response bằng Zod ở biên.

**Config FE đọc (không qua API):** `config/locales.json`, `config/theme-tokens.json` (tập giá trị + `defaults`), `config/system-pages.json`,
`config/sanitize-profiles.json` (khoá `dangerous` **không phải profile**; `sanitizeRichText('dangerous')` throw). Link `url` chỉ nhận scheme
https/http/mailto/tel, đường dẫn nội bộ `/x` (không `//host`) hoặc `#neo` (cả `kind` khác `external` nếu có `url`).

**Media.** `GET …/library/{assetId}/references` trả thêm `kind = PageDraft` + `pageId`/`pageTitle` (một mục mỗi trang) → cảnh báo xoá ảnh đọc được.
`ogImageId` của Page/Website là bản **Library**; ảnh trong cây là bản Clone/Direct.

**Fixture dùng chung = bộ test hợp đồng:** `packages/builder-components/tests/fixtures/tree/*.json` (+ `xss-richtext.json`). Mock MSW trả đúng `errorCodes`
của fixture; không chép fixture sang chỗ khác.

## Việc FE cần làm
1. **SDK đã sinh sẵn trong nhánh WEBSITE-001:** `website` có trong `packages/api-sdk/orval.config.ts`, `pnpm gen:api` đã chạy (generated `website` mới, `media` đổi `PageDraft`); `src/index.ts`/`src/mocks.ts` đã export `website` (bỏ `ProblemDetails*` trùng). `tree`/`tokens` ra `interface JsonElement {}` — kiểu hoá bằng Zod của `builder-components`.
2. MSW trước (dựa fixture), chỉ bật API thật ở integration. Màn: tạo website, danh sách trang, thuộc tính trang, editor (mở draft + autosave), Theme panel.
3. Mỗi màn có **loading · error · empty** đủ cả ba (empty: chưa có website; website chưa có trang Composable ngoài trang chủ).
4. Autosave: xử lý `409` (hoà giải), `422` (tô đỏ theo `path`), `sanitized` (nạp lại), `413`; state theo #20; dùng API model, không mirror Domain Entity.
5. **Smoke app thật ngay sau task FE đầu tiên (F1)**, không đợi integration (workflow §8): dev server, host `{slug}.vsite.local`, proxy BE, route mới mở được khi đã đăng nhập với Owner.

## Ràng buộc
- Chỉ ghi `apps/`, `packages/` (trừ `generated/**`). Không sửa tay generated.
- `builder-renderer` không import `builder-core`; Review không bao giờ có trong tree Shop Site (binding `Review` bị BE từ chối).
- Contract thiếu field giữa chừng → dừng, vòng `WEBSITE-001-D{n}` (workflow §6.1).

## Không thuộc phạm vi
- KHÔNG đụng `backend/`, `contracts/`, `config/`. BE sai/thiếu → DỪNG, báo cáo; không tự sửa, không làm tạm.
- Publish/rollback, `NavigationConfig`, `*_translation`, `CurrentPublicationId`/`SeedTemplateId` (Bước 7, 8b, 11, #98).
- API sửa `Website.Locales`/`DefaultLocale`/SEO mặc định (chỉ `GET`; mặc định `vi`).
- `requiresProps` của variant: BE chưa enforce (bắt ở publish, 8b).
- `references` chưa báo `Page.OgImageId`/`Website.DefaultOgImageId`/`FaviconAssetId` (nợ I2, Bước 8).
- Nới quyền Manager/Staff (nghiệp vụ nhân viên, sau).

## Acceptance
- [x] `pnpm gen:api` chạy được, lock khớp; `website` có trong `orval.config.ts` (làm ở WEBSITE-001)
- [ ] Loading / error / empty đủ cả ba cho mọi màn
- [ ] Zod/JSON schema của `builder-components` dùng để kiểu hoá `tree`/`tokens`
- [ ] Test Vitest + MSW pass; mock dùng fixture tree dùng chung
- [ ] UI Website/Media ẩn với role không phải Owner
- [ ] Autosave: `409` hoà giải, `422` tô đỏ theo `params.errors[].path`, `sanitized` nạp lại, `413`
- [ ] Smoke app thật sau F1
