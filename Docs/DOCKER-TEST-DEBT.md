# Docker test debt

> Nợ test cần Docker daemon (Testcontainers, hoặc `docker compose up` cho Postgres/Redis thật) mà
> session hiện tại KHÔNG chạy được — sandbox không có Docker. Máy có Docker đọc file này, chạy lại,
> rồi XOÁ đúng mục đã pass. Đây là nguồn sự thật DUY NHẤT cho loại nợ này — đừng ghi rải rác ở
> CLAUDE.md từng module hay báo miệng qua chat.

**Quy ước:**
- Thêm mục MỚI ở cuối danh sách "Pending" bên dưới, đừng sửa mục người/task khác đang chờ.
- Mỗi mục bắt buộc có: lệnh chạy chính xác (copy-paste được), lý do cần Docker, ngày thêm, task
  liên quan (nếu có), và tiêu chí "coi là xong".
- Chạy **thật** (không chỉ build) và **pass thật** → xoá nguyên mục khỏi file này. Lịch sử đã nằm ở
  git log của code/test liên quan, không cần giữ lại ở đây.
- Nếu FAIL → đừng xoá. Hoặc sửa bug rồi chạy lại tới khi pass, hoặc ghi rõ lỗi + để mục đó lại cho
  người tiếp theo (kèm ngày cập nhật gần nhất).
- File rỗng (chỉ còn "Pending" trống) là trạng thái mong muốn — không phải lỗi.

---

## Pending

### REFACTOR-BE-001 — toàn bộ IntegrationTests (97 test cần Testcontainers)

- **Lệnh:** `dotnet test backend/tests/IntegrationTests`
- **Vì sao cần Docker:** task đổi nơi kiểm quyền Owner (handler → `ShopMembershipEndpointFilter`),
  đổi cách dựng `Shop` trong ~15 file test và thêm audit stamping trong `AppDbContext`. Test
  endpoint (`LibraryEndpointTests`, `ShopLogoTests`, `ShopEndpointTests` — 403 non-Owner) và
  `DbConstraintTests` (CHECK `ck_shop_external_url`) chạy trên Postgres thật. Máy làm task không có
  Docker daemon: 188 test không cần Docker PASS, 97 test còn lại fail **chỉ** vì không kết nối được
  Docker (đã phân loại qua trx, không có lỗi khác). Logic kiểm Owner của filter đã có unit test
  không cần Docker (`ShopMembershipEndpointFilterTests`), phần còn thiếu là chạy end-to-end.
- **Ngày thêm:** 2026-10-08 · **Task:** `Docs/tasks/REFACTOR-BE-001/changelog.md`
- **Coi là xong khi:** lệnh trên PASS toàn bộ (Skipped chỉ còn 1 test `[Fact(Skip=…)]` có sẵn của
  SHOP-001).
