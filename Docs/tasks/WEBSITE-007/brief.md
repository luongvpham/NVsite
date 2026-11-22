# WEBSITE-007 — FE brief (Bước 7: menu website shop)

> Do session BE viết sau Gate 1 (2026-11-21). Việc FE theo [`plan.md`](plan.md) §10 (F1–F6). File này
> **trỏ** tới contract, chỉ ghi những gì S1 làm khác hoặc cụ thể hơn plan.

## Contract

Đã promote (Gate 1 gọn, #89). SDK đã chạy `pnpm gen:api`. Chi tiết thay đổi: [`contract-diff.md`](contract-diff.md).

- `contracts/openapi/website.v1.json` — sha256 `67c95b310d76727314da78ac0243b860a3632a85fbe26356ed08afc1a4d62142`
  (`contracts/contract.lock`, `taskId: WEBSITE-007`).

| operationId | Route | Ai gọi |
|---|---|---|
| `getNavigation` | `GET /api/shops/{shopId}/website/navigation` → `200 NavigationDto` | Portal (Owner) |
| `saveNavigation` | `PUT /api/shops/{shopId}/website/navigation` body `SaveNavigationRequest` → `200 SaveNavigationResultDto` | Portal (Owner) |

Kiểu Orval: `NavigationDto`, `SaveNavigationRequest`, `SaveNavigationResultDto`, `NavigationItemDto` (cấp 1) →
`NavigationChildItemDto` (cấp 2) → `NavigationGrandchildItemDto` (cấp 3, **không** có `children`).

Snapshot: `snapshot.navigation.items` không còn là `unknown[]`. Kiểu `SiteNavigationItem` và `siteSnapshotSchema`
được export từ `@vsite/builder-components`; nguồn là `packages/builder-components/meta/envelope-schema.ts`.

## Việc FE cần làm

Làm đúng F1 → F6 của plan §10. Những điểm plan chưa nói, hoặc S1 đã chốt khác:

**Màn Menu (F4)**
- **Gửi đủ field ở mọi mục:** `key`, `label`, `url`, `hidden`, `children` (cấp 3 thì không có `children`).
  Server **bắt buộc** `key` và `hidden`; thiếu một trong hai sẽ nhận `400`. Gửi `label: null` để dùng tên trang.
  Mục `page:` gửi `url: null`; nếu gửi `url`, server cũng bỏ đi.
- **`version`:** lần lưu đầu gửi `null` (lấy từ GET), các lần sau gửi `version` trong kết quả PUT gần nhất.
- **Thứ tự = vị trí trong mảng.** Không có field `order`.
- **Khoá `page:{guid}` luôn được trả về dạng chữ thường**, kể cả khi gửi chữ hoa. So khớp với `pages[].id`
  thì so chữ thường.
- **Giới hạn** (chặn ngay ở UI để không phải đợi 422): ≤ 12 mục cấp 1; ≤ 10 mục mỗi cha ở cấp 2 và cấp 3;
  nhãn ≤ 60 ký tự; URL ≤ 2048 ký tự; tối đa 3 cấp; một trang chỉ xuất hiện một lần trong cả menu.
- **Mã lỗi:**

  | Status | `error_code` | Xử lý |
  |---|---|---|
  | 422 | `NAVIGATION_INVALID` | `params.errors[] { path, code, message }` (≤ 100 mục, `params.truncated`). `path` là JSON Pointer vào body, vd. `/items/2/children/0/url`, dùng để tô đúng dòng. `code` ∈ `NAV_PAGE_NOT_FOUND`, `NAV_PAGE_DUPLICATE`, `NAV_PAGE_NOT_ALLOWED` (trang `NotFound`), `NAV_URL_UNSAFE`, `NAV_LABEL_REQUIRED`, `NAV_LABEL_TOO_LONG`, `NAV_TOO_MANY_ITEMS` (`path` trỏ tới mảng bị tràn) |
  | 409 | `NAVIGATION_CONFLICT` | `params { currentVersion, updatedAt }`, **cả hai có thể null** (khi chưa có dòng menu). Báo "menu vừa được sửa ở nơi khác", rồi tải lại |
  | 400 | `NAVIGATION_BODY_INVALID` | Body sai cấu trúc: JSON hỏng, thiếu `key`/`hidden`, field lạ ở cấp 3 (tức là gửi cấp 4). Đây là lỗi lập trình FE, không phải lỗi người dùng |
  | 413 | `NAVIGATION_BODY_TOO_LARGE` | Body > 256 KB. Không gặp nếu UI chặn đúng giới hạn |
  | 404 | `WEBSITE_NOT_FOUND` | Shop chưa có website |
  | 403 | `WEBSITE_OWNER_REQUIRED` | Route chỉ cho Owner; màn bọc `OwnerOnlyGate` |

  `NAV_TOO_DEEP` **không bao giờ** trả về qua HTTP; cấp 4 bị chặn bằng `400`.

**Publish / rollback (F5)**
- ⚠️ **Publish khi menu chưa từng lưu sẽ tạo dòng menu.** Tab nào đang mở màn Menu với `version: null` mà
  bấm Lưu sau đó sẽ nhận `409`. Sau publish **và** rollback, phải invalidate query `getNavigation`.
  _(Bổ sung sau review toàn nhánh, W4 ở [`review.md`](review.md):)_ không chỉ ca "chưa từng lưu" — publish khi menu
  đang dirty (`MarkPublished()`) và rollback (`MarkDirty()`) đều ghi dòng menu nên `version` (`xmin`) đổi. Tab nào đang
  mở màn Menu từ trước đó sẽ nhận `409` khi Lưu, dù `Items` không đổi.
- `isDirty` trong `NavigationDto`: `true` khi chưa từng lưu, sau khi lưu, hoặc sau rollback; `false` sau publish.
  Đây là nguồn cho dấu "Menu có thay đổi chưa publish".

**Render (F1–F3)**
- Phần tử `snapshot.navigation.items`: `{ key, label: string | null, url?, children? }`. `url` chỉ có ở
  `custom`; `children` chỉ có ở cấp 1–2 và chỉ khi khác rỗng; **không có** `hidden` (mục ẩn đã bị BE bỏ).
- BE đã bỏ khỏi snapshot các mục trỏ tới trang không có trong snapshot (trang đang tắt), kèm cả nhóm con của
  chúng. `resolveNavigation` vẫn phải tự lọc lại (cho preview, và cho snapshot cũ hay hỏng).
- Snapshot từ trước Bước 7 có `items: []` và vẫn hợp lệ (R6). `schemaVersion` vẫn là `1`.
- **Preview Portal** dựng từ `items` của GET (có `hidden`): phải bỏ mục `hidden` **và** mục trỏ trang đang tắt,
  để khớp với những gì site sẽ hiện.

## Ràng buộc

- #103: URL chỉ dựng qua `createResolveUrl`. Không tự ghép chuỗi URL trang.
- #23: `builder-renderer` isomorphic. Dropdown và menu mobile chỉ dùng CSS + `<details>`, không đụng `window`/`document`.
- #19: `key` là chuỗi template, không dùng `enum` của TypeScript.
- Link `custom`: render lại qua `isSafeLinkUrl`. Link http(s) mở tab mới với `rel="noopener noreferrer"`.
  Nhãn render dạng text (R4).
- `apps/web` không đọc draft và không gọi `getNavigation`; chỉ đọc snapshot.
- Không sửa `packages/api-sdk/src/generated/**`. Thiếu field hoặc endpoint ⇒ vòng `WEBSITE-007-D{n}` (workflow §6.1),
  không làm tạm.
- Style chỉ dùng `var(--vs-*)`.

## Không thuộc phạm vi

- **Mọi thứ trong `backend/` và `contracts/`.** BE đã xong và đã khoá. Thấy BE "sai" thì dừng và báo, đừng sửa.
- Component Registry (menu là chrome, không có manifest) và `config/reserved-routes.json`.
- Nguồn mục khác `page:` / `custom` (`serviceGroup` để Bước 9; `productCategory`/`sysProductListing` để 10c).
- Nhãn đa ngôn ngữ `labels` (#98, bước L). Job dọn mục mồ côi (8d).
- Dấu "chưa publish" cấp website (gồm theme). F5 chỉ nhắc về menu.
- Sửa lỗi chung "bind body có kiểu trả 500/400 rỗng" (`00-INDEX` §4) — task riêng.

## Acceptance

- Checkbox F1–F6 của plan §10 tick đủ, có test viết trước (MSW cho Portal).
- Nghiệm thu tay F6 với BE thật: đạt 4/4 và ghi vào `changelog.md` (thêm phần FE).
- `pnpm build && pnpm test` xanh; `pnpm check:docs` xanh.
- Plan §11: integration tắt MSW, chạy `change-reviewer`, viết `review.md` (có mục "Ma sát quy trình"), cập nhật dòng Bước 7 ở `00-INDEX` §3.
