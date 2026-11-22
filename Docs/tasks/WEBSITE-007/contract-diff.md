# WEBSITE-007 — contract diff

> Gate 1 gọn trước production (#89): chỉ có `NEW_ENDPOINT`, không `REMOVED`/`BREAKING`, không câu hỏi
> mở ⇒ **promote ngay** (`approvedBy = gate1-pre-production-89`). Người duyệt đọc lại file này sau.
> Sinh bằng `node tools/contract-sync/diff-report.mjs WEBSITE-007`, phần dưới "UNCHANGED" điền tay.

## ⚠️ BREAKING / REMOVED
(không có)

## NEW_ENDPOINT
- GET /api/shops/{shopId}/website/navigation
- PUT /api/shops/{shopId}/website/navigation

## ADDITIVE
(không có)

## UNCHANGED
37 operation không đổi.

---

## Endpoint mới (document `website`, tag `Website`)

| operationId | Method + path | OK | Lỗi khai |
|---|---|---|---|
| `getNavigation` | `GET /api/shops/{shopId}/website/navigation` | 200 `NavigationDto` | 401 · 403 `WEBSITE_OWNER_REQUIRED` · 404 `WEBSITE_NOT_FOUND` |
| `saveNavigation` | `PUT /api/shops/{shopId}/website/navigation` | 200 `SaveNavigationResultDto` | 400 `NAVIGATION_BODY_INVALID` · 401 · 403 · 404 `WEBSITE_NOT_FOUND` · 409 `NAVIGATION_CONFLICT` · 413 `NAVIGATION_BODY_TOO_LARGE` · 422 `NAVIGATION_INVALID` |

**Schema mới:**

| Schema | Field |
|---|---|
| `NavigationDto` | `version: integer \| null` (null = chưa từng lưu, đang là menu tự sinh) · `isDirty: boolean` · `items: NavigationItemDto[]` (đã ghép, #113) |
| `SaveNavigationRequest` | `version: integer \| null` · `items: NavigationItemDto[]` |
| `SaveNavigationResultDto` | `version: integer` · `isDirty: boolean` |
| `NavigationItemDto` (cấp 1) | `key: string` (`page:{guid}` \| `custom`) · `label: string \| null` · `url: string \| null` · `hidden: boolean` · `children: NavigationChildItemDto[]` |
| `NavigationChildItemDto` (cấp 2) | như cấp 1, `children: NavigationGrandchildItemDto[]` |
| `NavigationGrandchildItemDto` (cấp 3) | `key` · `label` · `url` · `hidden` — **không** `children`, `additionalProperties: false` |

Orval chạy thử trên staging (thư mục tạm): 6 type mới, `children` có type đúng từng cấp, **không** type trùng `…Dto2`.

`snapshot.navigation.items` (trong `GET /api/site`) đổi shape nhưng `snapshot` ra OpenAPI là schema rỗng
(`JsonElement`) nên diff không thấy — shape nằm ở Zod/JSON Schema `packages/builder-components/meta/envelope-schema.ts`
(`{ key, label: string|null, url?, children? }`, tối đa 3 cấp, không `hidden`, `schemaVersion` vẫn 1 — additive, `items: []` cũ vẫn hợp lệ).

## Auth policy

Cả hai route: `RequireAuthorization(RequireGlobalScope)` + `RequireShopOwner("WEBSITE_OWNER_REQUIRED")` —
**chỉ Owner** của shop, đọc lẫn ghi (#100). `ShopId` chỉ từ route + `ITenantContext` (#99), không có trong body.
Trang trong menu tra theo website của tenant **trong câu query** — `page:{id}` của shop khác ⇒ `422 NAV_PAGE_NOT_FOUND`, không lộ tồn tại (R1).

## Giả định tôi đã tự đặt (không hỏi)

| # | Giả định | Nếu sai thì |
|---|---|---|
| A1 | Mã lỗi cấp trên: `422 NAVIGATION_INVALID` (cùng vỏ `THEME_INVALID`: `params.errors[] {path, code, message}` ≤ 100 + `params.truncated`), mã con đúng như `data-needs.md` (`NAV_*`) | Đổi tên hằng, FE đổi theo |
| A2 | `400 NAVIGATION_BODY_INVALID`: JSON hỏng, thiếu `key`/`hidden`, field lạ ở cấp 3 (tức gửi cấp 4). `detail` chỉ mang đường dẫn cấu trúc (chỉ số mảng + tên field đã biết, ≤ 200 ký tự), không lặp lại input | — |
| A3 | `413 NAVIGATION_BODY_TOO_LARGE` khi body > **256 KB** (menu thật vài KB; trần lý thuyết nếu mọi URL dài 2048 là ~2,8 MB) | Tăng hằng `MaxNavigationBodyBytes` |
| A4 | `NAV_TOO_DEEP` không bao giờ ra qua HTTP — contract chặn cấp 4 bằng cấu trúc (400). Mã vẫn còn trong `Validate` để phòng dữ liệu lưu hỏng | — |
| A5 | `409 NAVIGATION_CONFLICT` `params { currentVersion: long?, updatedAt? }` — **cả hai null** khi chưa có dòng menu (khác `DRAFT_CONFLICT`, luôn có giá trị) | FE đọc null-safe |
| A6 | `params` của 422/409 **không** có type trong OpenAPI (khai `ProducesValidationProblem`/`ProducesProblem`) — giống `savePageDraft`/`updateTheme` hiện có; FE đọc `params` như các route đó | Nếu muốn type: việc chung cho mọi route website, ngoài task |
| A7 | OpenAPI đánh `required` cả `label`, `url`, `children` (DTO dùng chung đọc/ghi); server chỉ **bắt buộc** `key` + `hidden`. FE gửi đủ field theo type Orval — đừng dựa vào sự dễ dãi | — |
| A8 | Khoá `page:{guid}` được chuẩn hoá về **GUID chữ thường** khi lưu và khi vào snapshot (FE so với `pages[].id` chữ thường) — GET/PUT trả khoá chữ thường dù gửi chữ hoa | — |
| A9 | Website chưa có ⇒ `404 WEBSITE_NOT_FOUND` cả GET lẫn PUT (như `getTheme`) | — |
| A10 | Publish khi menu chưa từng lưu sẽ **tạo dòng** từ menu tự sinh (plan Flow 4) ⇒ tab đang mở màn Menu với `version: null` bấm Lưu sau đó nhận `409`. FE phải invalidate query menu sau publish/rollback (đã có trong F5) | — |

## Câu hỏi cần anh quyết
(không có — các điểm review nêu ở T3 đã được quyết hoặc sửa: độ sâu ⇒ người chốt 3 cấp + ba kiểu DTO; thiếu `hidden` ⇒ bắt buộc (400); body không trần ⇒ 256 KB)

## Người duyệt đã quyết

| Quyết định | Ở đâu |
|---|---|
| Menu lồng **tối đa 3 cấp**, contract diễn đạt bằng **ba kiểu DTO tường minh** (OpenAPI .NET 9 sinh `items: {}` cho DTO đệ quy); ≤ 10 mục mỗi cha ở cấp 2 và 3 (người chốt 2026-11-21) | #113 (viết đè "một cấp") · plan §4 dòng 1 · §12 G4 |
| Snapshot lưu khoá, FE ghép nhãn/URL; menu ghép lúc đọc; `IsDirty` riêng (người chốt 2026-11-20) | #113 |
