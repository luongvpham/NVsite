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

### MEDIA-001 — 8 lớp test cần Postgres/Redis/MinIO thật (Testcontainers), T2–T9

Máy làm các task này không có Docker daemon. Build (`dotnet build backend/vsite.sln`) 0 lỗi/0
warning, và mọi hành vi tương đương đã verify KHÔNG cần Docker qua test EF InMemory / unit test
song song (liệt kê ở từng mục) — 123/123 test không-Docker của `IntegrationTests` + 8/8
`ArchitectureTests` pass thật trong sandbox này.

- Lệnh chạy TỪNG lớp:
  ```
  dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~S3ObjectStorageTests"
  dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~MediaDbConstraintTests"
  dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~MediaTenantIsolationTests"
  dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~UploadEndpointTests"
  dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~LibraryEndpointTests"
  dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~ShopLogoTests"
  dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~MediaReferenceValidatorSqlCountTests"
  dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~MediaFileServingTests"
  ```
- Lệnh chạy GỘP cả 8 lớp trong một lần:
  ```
  dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~S3ObjectStorageTests|FullyQualifiedName~MediaDbConstraintTests|FullyQualifiedName~MediaTenantIsolationTests|FullyQualifiedName~UploadEndpointTests|FullyQualifiedName~LibraryEndpointTests|FullyQualifiedName~ShopLogoTests|FullyQualifiedName~MediaReferenceValidatorSqlCountTests|FullyQualifiedName~MediaFileServingTests"
  ```

- **`S3ObjectStorageTests`** (T2) — `Testcontainers.Minio`, image pin
  `RELEASE.2025-04-22T22-12-26Z` (bản mới hơn default cũ của package, cần vì hỗ trợ conditional
  write `If-None-Match` — bản `RELEASE.2023-01-31` im lặng bỏ qua, làm no-overwrite #75 không hoạt
  động). Image pin **chưa được pull-verify** trên máy có Docker. Chạy lại cũng verify luôn
  `AWSSDK` flexible checksums (SDK mới mặc định gửi checksum header khác chuẩn S3 cũ) hoạt động
  đúng với MinIO ở bản pin này.
- **`MediaDbConstraintTests` + `MediaTenantIsolationTests`** (T4) — CHECK constraint
  `ck_media_library_preset` và composite FK `(LogoId, Id) → MediaAsset(Id, ShopId)` cần Postgres
  thật (SQLite/InMemory không enforce CHECK/composite FK giống Postgres). Chú ý đặc biệt: test 8 và
  11 của bộ 12 test bắt buộc ở `08` §9.
- **`UploadEndpointTests`** (T5) — 2 endpoint upload qua HTTP pipeline thật (`MediaApiFactory`,
  Testcontainers Postgres+Redis). Test 3, 4 của bộ 12 (`08` §9). Bao gồm R4 (rollback best-effort
  sau lỗi DB) qua HTTP thật, và xác nhận 413 do Kestrel enforce đúng ở ngưỡng
  `IHttpMaxRequestBodySizeFeature.MaxRequestBodySize` = 11 MB (`BadHttpRequestExceptionHandlerTests`
  không-Docker chỉ verify phần DỊCH lỗi → ProblemDetails, không verify TestServer/Kestrel có thật sự
  ném `BadHttpRequestException` ở đúng ngưỡng).
- **`LibraryEndpointTests`** (T6) — test 5, 9, 10 của bộ 12 (`08` §9), qua HTTP pipeline thật.
- **`ShopLogoTests`** (T7) — ngoài verify 2 endpoint logo qua HTTP, đây là bằng chứng DUY NHẤT rằng
  Postgres thật sự order INSERT `MediaAsset` **trước** UPDATE `Shop.LogoId` dưới composite FK ghép
  `Shop(LogoId, Id) → MediaAsset(Id, ShopId)` (EF Core suy ra thứ tự từ FK graph — hành vi này không
  verify được bằng InMemory provider, provider đó không enforce FK).
- **`MediaReferenceValidatorSqlCountTests`** (T8) — đếm số câu SQL thật sự chạy (exactly-1-SQL) cho
  `IMediaReferenceValidator` — cần `DbCommandInterceptor` bắt câu lệnh SQL thật, InMemory provider
  không sinh SQL để đếm.
- **`MediaFileServingTests`** (T9) — test 12 của bộ 12 (`08` §9): `/media/*` phục vụ đúng file qua
  HTTP thật, gồm case cross-host (đọc ảnh từ Host khác `vsite.vn`, vd. `{slug}.vsite.vn`, custom
  domain) — cần `MediaApiFactory` dựng host pipeline thật.
- **Ghi chú thêm:** eager load preset catalog (`image-presets.json`) lúc host khởi động
  (`Program.cs`, ngay dưới `builder.Build()`, cùng cách `IReservedRoutesProvider`) tới giờ **chỉ**
  được exercise bởi các test KHÔNG cần Docker (mọi `IntegrationTests` đã pass dùng chung
  `WebApplicationFactory` build 1 lần) — hành vi fail-fast khi `image-presets.json` lỗi/thiếu lúc
  host thật khởi động (không phải qua factory test) chưa có xác nhận riêng.

- Ngày thêm: 2026-09-27.
- Task liên quan: `MEDIA-001` (T2, T4, T5, T6, T7, T8, T9 — gộp lại ở T10 khi đóng session S1).
- Tiêu chí "coi là xong": chạy lệnh GỘP ở trên, toàn bộ 8 lớp pass thật (không chỉ build) → xoá
  nguyên mục này. Nếu chỉ một vài lớp pass, sửa lại danh sách để giữ đúng những lớp còn FAIL, ghi rõ
  ngày cập nhật.
