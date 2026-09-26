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

### T5, MEDIA-001 — `UploadEndpointTests` (`Media/UploadEndpointTests.cs`, `MediaApiFactory`)

- Lệnh chạy chính xác:
  ```
  dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~UploadEndpointTests"
  ```
- Lý do cần Docker: `MediaApiFactory` dùng `Testcontainers.PostgreSql` + `Testcontainers.Redis` (mô
  phỏng `ShopApiFactory`) để chạy hai endpoint `POST /shops/{shopId}/media/slot-uploads` và
  `POST /shops/{shopId}/media/library` qua pipeline HTTP thật (auth, tenancy, ProblemDetails).
- Đã xác nhận: build 0 lỗi/0 warning (`dotnet build backend/vsite.sln`), đọc lại bằng mắt khớp hành
  vi handler — cùng logic đã pass thật ở `UploadHandlerTests` (EF InMemory, không cần Docker, xem
  `backend/tests/IntegrationTests/Media/UploadHandlerTests.cs`, 7/7 test cases tương ứng: record
  Direct, record Library+Derived, preset lạ 422, focal ngoài [0,1] 422, R4 rollback). Test HTTP
  layer thêm: 403 SHOP_ACCESS_DENIED, field `shopId` trong multipart bị bỏ qua, và toàn bộ đi qua
  `RequireGlobalScope` + `RequireShopMembership()` thật (JWT, ProblemDetails).
- Ngày thêm: 2026-09-26.
- Tiêu chí "coi là xong": chạy lệnh trên, toàn bộ test trong `UploadEndpointTests` pass thật (không
  chỉ build) → xoá mục này.
