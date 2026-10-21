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
- **Smoke app thật (workflow §8): CHƯA chạy** — 2026-10-22 BE `:5270` trả 500 cho mọi request vì Postgres
  (`localhost:5433`, Docker) không chạy (Docker Desktop tắt). Chạy lại khi có DB.

## Lệch so với thiết kế / plan
- F1: `retry: false` cho `getWebsite` — 404 là trạng thái bình thường "chưa có website", không retry.

## Chưa làm xong
