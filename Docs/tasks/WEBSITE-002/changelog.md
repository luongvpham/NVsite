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

## Lệch so với thiết kế / plan
- F1: `retry: false` cho `getWebsite` — 404 là trạng thái bình thường "chưa có website", không retry.

## Chưa làm xong
