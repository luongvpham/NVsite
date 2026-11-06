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

## Lệch so với thiết kế / plan
- F1: `retry: false` cho `getWebsite` — 404 là trạng thái bình thường "chưa có website", không retry.
- F2: form dùng schema Zod viết tay (trường dạng chuỗi cho SEO) thay vì `createPageBody`/`updatePageBody` sinh từ
  contract — contract coi SEO là `string | null`, form cần chuỗi rỗng; kiểu payload vẫn ràng bằng
  `CreatePageRequest`/`UpdatePageRequest` của SDK (compile-time).
- F2: danh sách vẫn chưa hiện nhãn ảnh OG; ảnh chọn xong hiện ngay từ asset vừa chọn (không round-trip `assets?ids=`).

## Chưa làm xong
