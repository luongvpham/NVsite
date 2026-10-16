# WEBSITE-001 (Bước 5a) — nhu cầu dữ liệu

> Task **backend-only** (5b editor là task sau), nhưng contract sinh ra ở đây là thứ 5b dựng lên, nên
> file này viết từ góc nhìn **màn hình của 5b** để 5b không phải mở vòng `-D{n}` (workflow §6.1).
> Không viết contract ở đây — chỉ nói màn cần gì. Tên endpoint là đề xuất, chốt ở Gate 1.

## Màn: Tạo website (Portal, sau khi tạo shop `Hosted`)
Vào: `shopId` (route). Ra: `WebsiteDto` — `id`, `status`, `defaultLocale`, `locales[]`, `defaultSeoTitle`,
`defaultSeoDescription`, `defaultOgImageId?`, `faviconAssetId?`, `trackingSnippets`, và **id trang chủ**
để Portal mở thẳng editor.
Lỗi cần phân biệt: shop không phải `Hosted` · shop đã có website.
`GET website` khi shop **chưa có** website → `404 WEBSITE_NOT_FOUND` (Portal dựa vào đó hiện nút "Tạo website").
**Mọi màn ở file này chỉ Owner dùng được** (#100) — role khác, kể cả `Customer`, nhận 403.

## Màn: Danh sách trang (Portal)
Mỗi item: `id`, `kind` (`Composable`/`System`), `systemType?`, `slug`, `title`, `isEnabled`,
`noIndex`, `sortOrder`, `isHomePage` (**suy ra** từ `slug == ""`, không phải cột — `05` §4),
`isDirty` (từ draft), `updatedAt`.
Toàn trang: không phân trang (3–7 trang/website). Có cờ `deletable` để UI khỏi tự suy luật
"trang chủ + System page không xoá" (một nguồn luật = BE).

## Màn: Thuộc tính trang (Portal)
`slug`, `title`, `seoTitle`, `seoDescription`, `ogImageId?` (bản **Library**, #73/#76), `noIndex`,
`isEnabled`. Ghi: tạo / sửa / xoá trang `Composable`. System page: sửa `title`/SEO/`isEnabled`/`noIndex`,
**không** sửa `slug`/`kind`/`systemType`, không xoá.

## Màn: Editor — mở trang (Portal, 5b)
`GET draft`: `tree` (cả cây), `version` (token concurrency, **opaque với FE** — gửi lại nguyên văn),
`isDirty`, `updatedAt`, và `page.kind` (để Operations Engine FE biết luật System).
FE cần thêm để dựng canvas mà không gọi lần hai: `mediaMap` (imageId → storageKey) — **đã có** qua
`GET …/media/assets?ids=` (MEDIA-001), không đưa vào draft.

## Màn: Editor — autosave (Portal, 5b)
Vào: `tree` + `version`. Ra thành công: `version` mới + `isDirty` + `sanitized` (`true` khi BE đã làm sạch
richText nên bản lưu khác bản gửi — FE tải lại draft nếu cần) (**không** trả lại cả tree — tránh
vài trăm KB mỗi 2 giây).
Ra thất bại, FE cần phân biệt được bằng `error_code`:
- `409 DRAFT_CONFLICT` — tab khác đã lưu. FE cần **gì để hoà giải?** → trả kèm `currentVersion` (xem Câu hỏi 4).
- `422 TREE_INVALID` — `errors[]` mỗi phần tử `{ path, code, message }`, `path` là JSON Pointer tới node/prop
  (vd. `/children/2/props/imageId`) để Inspector tô đỏ đúng ô.
- `422 MEDIA_INVALID_IMAGE_REFERENCE` (đã có) · `422 SCHEMA_VERSION_UNSUPPORTED`.
- richText chứa thứ nguy hiểm → mục `code = RICHTEXT_UNSAFE` trong `errors[]` của `TREE_INVALID` (câu 5).

## Màn: Theme panel (Portal, 5b — op `changeTheme`)
`GET theme` → `{ presetKey?, tokens, updatedAt }`. `PUT theme` → tokens (cả tài liệu, `schemaVersion` ở gốc).
Tập giá trị cho phép (màu/font/scale…) **FE đọc từ `config/theme-tokens.json`**, không từ API.

## Màn: Xoá ảnh khỏi Media Library (đã có, MEDIA-001)
`GET …/library/{assetId}/references` phải báo thêm **"đang dùng trong trang X"** (`kind = PageDraft`,
kèm `pageId` + `pageTitle` để cảnh báo đọc được).

---

## Câu hỏi mở — ĐÃ CHỐT (người duyệt, 2026-10-13)

| # | Câu hỏi | Chốt |
|---|---|---|
| 1 | Nguồn `ShopId` cho handler: route hay `ITenantContext` | **`ITenantContext` là nguồn duy nhất.** Command chỉ mang id con (`PageId`…), **không** mang `ShopId`; handler đọc `tenant.ShopId`, null → fail-closed. Ngoại lệ duy nhất: lệnh tạo website nhận `shopId` tường minh (aggregate mới, chưa có tenant). Handler Media cũ giữ nguyên, ghi nợ dọn riêng |
| 2 | Tên file + field cho #97 | `config/locales.json` = mảng phẳng `["vi","en"]` (`05` §26.5). `config/theme-tokens.json` = `{ colorKeys[7], colorPattern, fonts[], scale[], radius[], shadow[], sectionGap[] }` — giá trị cụ thể ở `plan.md` §4. Shape vỏ viết **Zod** trong `packages/builder-components` rồi sinh JSON Schema cho BE (thêm session S0) |
| 3 | System page tạo cùng website trắng | **`Contact` + `NotFound`**, mỗi trang tree = `Page › RichText` seed sẵn (vì System page khoá cấu trúc, tree rỗng thì shop không sửa được gì). Trang chủ `Slug = ""` tree rỗng |

## Câu hỏi đợt 2 — ĐÃ CHỐT (người duyệt, 2026-10-15)

| # | Câu hỏi | Chốt |
|---|---|---|
| 4 | `409 DRAFT_CONFLICT` trả gì kèm để FE hoà giải? | `currentVersion` + `updatedAt` + `updatedByUserId`; **không** trả tree (FE gọi `GET draft` nếu muốn tải lại) |
| 5 | richText bẩn: từ chối hay làm sạch? | **Chỉ từ chối (422 `RICHTEXT_UNSAFE`) khi phần bị cắt là thứ nguy hiểm:** phần tử `script` (và `iframe`/`object`/`embed`/`style`), thuộc tính `on*`, URL `javascript:`/`data:`/`vbscript:`. **Mọi thứ khác** (`class`, `style`, `span`, thẻ ngoài whitelist, `rel` thiếu…) → **làm sạch rồi lưu bản sạch**, không báo lỗi. Lý do: Lexical xuất `class`/`style`/`span`; từ chối mọi khác biệt làm autosave 422 liên tục nếu FE quên sanitize |
| 6 | Ai bấm "tạo website"? | Endpoint tường minh `POST /shops/{shopId}/website` (Owner, shop `Hosted`), **không** tự tạo trong `CreateShopHandler` |
| 7 | `PUT draft` có bắt `requiresProps` của variant không? | **Không** ở 5a (autosave lưu bản dở dang). Bắt ở publish (8b) |
| 8 | Có API sửa `Website.Locales`/`DefaultLocale`/SEO mặc định không? | **Không** ở 5a — chỉ `GET`. Mặc định `{vi}`/`vi` |
| 9 | Role nào được dùng API Website? | **Chỉ `Owner`** — mọi endpoint Website, đọc lẫn ghi (#100). **Vá luôn Media**: mọi endpoint `/api/shops/{shopId}/media/**` cũng chỉ Owner — hiện chỉ `RequireShopMembership` nên khách (`Customer`) có token global lọt qua |
