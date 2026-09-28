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

### `dotnet test backend/tests/IntegrationTests` — toàn bộ 85 test dùng Testcontainers fail: "Docker is either not running or misconfigured"

- **Lệnh:** `dotnet test backend/tests/IntegrationTests`
- **Lý do cần Docker:** `MediaApiFactory`, `S3ObjectStorageTests` và các test Postgres/Redis thật
  (`DbConstraintTests`, `TenantIsolationTests`, `ShopEndpointTests`, …) dựng container qua
  Testcontainers.PostgreSql / Testcontainers.Redis / Testcontainers.LocalStack lúc ctor.
- **Ngày thêm:** 2026-09-27, task MEDIA-001-D2 (transformer gộp schema trùng `MediaAssetDto2`).
- **Đã thử, KHÔNG fix được trong session này:** `docker info`/`docker ps`/`docker context ls` chạy
  bình thường từ shell (Docker Desktop, context `desktop-linux`, named pipe
  `npipe:////./pipe/dockerDesktopLinuxEngine` sống) — nhưng tiến trình `dotnet test` (cả từ Git Bash
  lẫn PowerShell 5.1, có và không có `DOCKER_HOST` trỏ `npipe://./pipe/docker_engine` /
  `npipe://./pipe/dockerDesktopLinuxEngine`) luôn báo `DockerEndpointAuthConfig` null — client
  `Docker.DotNet` bên trong Testcontainers .NET không detect được endpoint dù CLI `docker` detect
  được. Nghi ngờ khác biệt token/pipe ACL giữa phiên chạy `dotnet test` và phiên chạy `docker` CLI
  trong sandbox này, chưa xác định được root cause chính xác — cần người có quyền cấu hình sandbox
  hoặc chạy ngoài sandbox kiểm tra lại.
- **Bằng chứng KHÔNG phải do thay đổi lần này gây ra:** cả 85 test fail đều dừng ngay ở constructor
  (trước khi chạm logic test) với đúng một `ArgumentException` giống hệt nhau; 128 test còn lại
  (không cần Docker, gồm 5 test mới `DuplicateNullableSchemaDocumentTransformerTests`) đều pass.
  `dotnet build` toàn repo 0 warning/0 error.
- **Coi là xong khi:** `dotnet test backend/tests/IntegrationTests` chạy **thật** (không skip) và
  toàn bộ pass trên máy có Docker daemon mà `dotnet test` reach được trực tiếp.
