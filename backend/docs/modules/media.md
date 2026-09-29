# Media — vsite

## Trạng thái (MEDIA-001, Bước 4)

`MediaAsset` + pipeline ảnh (upload → resize/crop → strip EXIF → encode `webp`) + Media Library +
9 preset + `/media/*` file serving + shop logo đã xong (T1–T9). `MEDIA-001` ở Gate 1
(`Docs/tasks/MEDIA-001/contract-diff.md`), chưa có `brief.md`.

**10 endpoint hiện có** (`Vsite.Api/Media/MediaEndpoints.cs`, tất cả `RequireGlobalScope` +
`.RequireShopMembership()`):

| Method | Path | Policy | Trả về |
|---|---|---|---|
| POST | `/shops/{shopId}/media/slot-uploads` | member | `SlotUploadResultDto { asset, libraryAsset? }` |
| POST | `/shops/{shopId}/media/library` | member | `MediaAssetDto` |
| GET | `/shops/{shopId}/media/library?page&pageSize` | member | `PagedResult<MediaAssetDto>` |
| POST | `/shops/{shopId}/media/library/{assetId}/clones` | member | `MediaAssetDto` |
| GET | `/shops/{shopId}/media/library/{assetId}/references` | member | `MediaReferencesDto` |
| GET | `/shops/{shopId}/media/library/{assetId}/derivatives?preset` | member | `MediaAssetDto[]` |
| DELETE | `/shops/{shopId}/media/library/{assetId}` | Owner | 204 |
| GET | `/shops/{shopId}/media/assets?ids=…` | member | `MediaAssetDto[]` |
| GET | `/shops/{shopId}/media/usage` | member | `MediaUsageDto { usedBytes }` |
| PUT | `/shops/{shopId}/logo` | Owner | `ShopLogoDto` |

`GET .../derivatives` (MEDIA-001 D3, #73): tra phái sinh (`IsInLibrary=false`, `SourceAssetId = assetId`)
của một bản Library, tuỳ chọn lọc `preset` chính xác (chứa dấu phẩy, vd `320x96,inside` — client phải
URL-encode; không lọc thì trả tất cả, sắp `Preset` rồi `CreatedAt`). `IgnoreQueryFilters()` + `ShopId`
route + `!IsDeleted` viết tay trên CHÍNH dòng phái sinh, không đọc dòng nguồn: bản Library đã soft
delete (kể cả là `Shop.LogoId`, A11) vẫn resolve được. Id lạ / của shop khác / không có phái sinh →
`200 []`, không bao giờ 404. `preset` > 40 ký tự → 422; tên preset lạ không bị từ chối (trả `[]`).

Ngoài ra `GET /media/{relativePath}` (mọi host, không qua OpenAPI — xem §4) phục vụ file tĩnh.

## 1. Ba loại record `MediaAsset` — suy ra từ cột, không có enum

Neo vào `Shop`, **không** neo `Website`. **Không** dùng cho ảnh `Listing`/`Product` (đường dẫn nằm
trong `ImageUrls` của module `Marketplace`/`Catalog`, `08` §8).

| Loại | `IsInLibrary` | `Preset` | `SourceAssetId` | Tính quota |
|---|---|---|---|---|
| Bản Library | `true` | `NULL` | `NULL` | ✅ |
| Ảnh upload thẳng vào slot | `false` | preset của slot | `NULL` | ✅ |
| Clone / phái sinh | `false` | preset | id bản Library | ❌ |

`StorageKey` là đường dẫn **tương đối**, **bất biến** (#75 — `IObjectStorage.PutAsync` không ghi
đè, key trùng thì ném lỗi). `MimeType` sau xử lý luôn `image/webp`, quality 82. Bản Library và file
full cạnh dài ≤ 1600px, **không upscale** — ảnh gốc nhỏ hơn preset thì `Width`/`Height` ghi kích
thước thật, `Preset` vẫn ghi đúng slot (đúng tỉ lệ, chỉ mờ hơn khi phóng to ở FE).

Quota = tổng `SizeBytes` của record `SourceAssetId IS NULL AND DeletedAt IS NULL`.

## 2. Vì sao không có image proxy runtime (`08` §1, Quyết định #53)

Pipeline sinh file đúng kích thước **lúc đặt vào slot hoặc lúc upload**, không sinh lúc render. Một
proxy sinh phái sinh lúc render kéo theo bốn thứ MVP không cần trả giá: một service xử lý ảnh sống
chịu tải public, whitelist preset thành biên giới bảo mật, race condition cache-miss, và lifecycle
policy dọn phái sinh không ai xem. Sinh lúc đặt vào slot thì cả bốn đều biến mất — resize xảy ra
trong request đã có auth + rate-limit sẵn, không URL công khai nào nhận tham số kích thước, mỗi file
sinh đúng một lần, mỗi file gắn với đúng một record DB để dọn bằng job quét tham chiếu (Bước 8).

Cái giá đã chấp nhận: ảnh upload thẳng (không tick "lưu vào thư viện") không đổi được focal point
sau này (phần bị cắt đã mất — inspector ẩn điều khiển focal point khi `SourceAssetId IS NULL AND
IsInLibrary = false`) và không sinh lại được khi preset của slot đổi (preset drift, #74). Ảnh từ
Library thì đổi được (clone lại) và sinh lại được.

## 3. `/media/*` — phục vụ file tĩnh, mọi host (T9, #53, #81)

`Vsite.Api.Media.MediaFileMiddleware` chạy **trước** routing thường, khớp path `/media/{*rest}` trên
**bất kỳ Host nào** (`vsite.vn`, `{slug}.vsite.vn`, custom domain) — đọc ảnh **không kiểm tenant**
(#81), vì URL đã tự chứa `shops/{shopId}/...` và ảnh là public theo thiết kế. Vì vậy `/media/*`
**không xuất hiện trong OpenAPI** (`AddOpenApi("media")` chỉ khai các endpoint JSON ở trên) — nó
không phải REST resource có request/response DTO, chỉ stream byte theo `IObjectStorage.OpenReadAsync`.

Thứ tự xử lý mỗi request (R1, R2 test T9):
1. Path phải bắt đầu đúng `/media/`, còn lại là `relativePath`.
2. Từ chối path chứa `..`, `\`, hoặc là đường dẫn tuyệt đối — không bao giờ đọc ra ngoài root storage.
3. `IObjectStorage.OpenReadAsync` — null (không tồn tại) → 404.
4. Có → stream với `Content-Type` theo dữ liệu đã lưu, `Cache-Control: public, max-age=3600`
   (`immutable` hoãn tới Bước 8 khi file thật sự bất biến end-to-end — xem A8, `Docs/tasks/MEDIA-001/plan.md` §10),
   `X-Content-Type-Options: nosniff`.

DB lưu **đường dẫn tương đối**, không lưu URL. Ảnh trong nội dung builder vẫn đi qua `resolveImage()` (FE,
`builder-renderer`), component không bao giờ tự nối chuỗi URL. Field DTO tên `*Url` dành cho Portal (vd. `logoUrl`,
#88) do BE trả sẵn có tiền tố `/media/` qua `ImagePaths.MediaUrl` — FE dùng nguyên trạng, KHÔNG bọc thêm; ảnh
thư viện ngoài tree dùng helper FE `mediaUrl(storageKey)`.

## 4. Logo shop — `Media → IShopLogoWriter` (T7, #73, #76, #82)

`PUT /shops/{shopId}/logo` (Owner) đi qua `UploadShopLogoHandler` (module `Media`), ghi **một
`SaveChangesAsync` duy nhất** cho cả:
- Insert `MediaAsset` bản Library + derivative logo (`surfaces.Shop`: `320x96,inside` cho
  Header/Shop Profile, `96x96,cover` cho avatar).
- Update `Shop.LogoId` — qua `Vsite.Application.Shop.Interfaces.IShopLogoWriter` (Public Contract
  sống ở `Application/Shop`, implement ở `Vsite.Infrastructure.Shop.ShopLogoWriter`), **không**
  reference `Vsite.Domain.Shop.Entities.Shop` trực tiếp trong Media — đúng ranh giới module #1
  (`Media.dependsOn = ["Shop"]`).

FK ghép `Shop(LogoId, Id) → MediaAsset(Id, ShopId)` (Quyết định #76) khai từ **phía Media**
(`ShopLogoConfiguration`), Postgres `MATCH SIMPLE`: `LogoId IS NULL` thì FK không áp dụng. EF Core
tự order INSERT `MediaAsset` trước UPDATE `Shop.LogoId` nhờ composite FK — không cần tự sắp thứ tự
lệnh (chứng minh bằng `ShopLogoTests`, xem Docker debt bên dưới).

**Đọc logo cho `ShopDto` (D4, #88) — `Shop → IShopLogoReader ← Media`.** `ShopDto.logoUrl` và
`ShopSummaryDto.logoUrl` là `/media/{storageKey}` (dựng bởi `ImagePaths.MediaUrl` — cùng hằng `MediaPathPrefix`
với route mount `/media` ở `Program.cs`) của phái sinh `320x96,inside` (hằng `LogoPresets.Header`, ở
`Vsite.Application/Media/LogoPresets.cs`; test khẳng định `IDerivativePresetCatalog.For("Shop")` chứa nó).
Port `IShopLogoReader` do `Shop` khai báo (`Application/Shop/Interfaces`; `GetLogoUrlAsync` cho một shop,
`GetLogoUrlsAsync` tra theo lô cho `GET /shops`), adapter
`Vsite.Infrastructure.Media.ShopLogoReader` (đăng ký trong `AddMediaModule`) — chiều ngược với
`IShopLogoWriter`. Query dùng `IgnoreQueryFilters()` + `ShopId == shopId` + `!IsDeleted` +
`SourceAssetId == logoId` + `Preset` trong CÙNG câu (giống `GetDerivativesHandler`), lấy dòng đầu theo
`CreatedAt, Id`; bản Library đã soft delete vẫn resolve (A11). `LogoId` null hoặc không thấy phái sinh →
`null`, không ném. Bản theo lô là ĐÚNG MỘT câu SQL: join `Shop (Id, LogoId)` với `MediaAsset`
(`IgnoreQueryFilters`, `Preset`, `!IsDeleted`) nên cặp (ShopId, SourceAssetId) được ép trong query; shop không có logo
vắng mặt trong kết quả (`ShopLogoBatchSqlCountTests`, Docker).

Đọc ngược chiều (Media cần biết ai là owner của shop để authorize `DELETE`/`PUT logo`) đi qua
`Vsite.Application.Shop.Interfaces.IShopOwnershipService` (Public Contract khác, cùng mẫu
`IShopLogoWriter`) — không lấy role từ claim token (#21.5).

Xoá bản Library đang là `Shop.LogoId` **được phép** (A11, `Docs/tasks/MEDIA-001/plan.md` §10) — FK
trỏ vào row soft-delete vẫn hợp lệ (`ON DELETE` không cascade xoá vật lý ngay), logo vẫn hiện; chỉ
cảnh báo qua `GET .../references`, không chặn.

## 5. ⚠️ Cảnh báo — xoá node không xoá ảnh (`08` §4)

Bước 4 (module này) **không xoá file nào**, ngoại trừ thao tác shop chủ động xoá khỏi Library (và đó
cũng chỉ là soft delete). Cụ thể:

| Hành động | Record | File trên storage |
|---|---|---|
| Xoá component khỏi draft | Giữ nguyên | Giữ nguyên |
| Đổi ảnh khác cho slot | Record cũ giữ, tạo record mới | Cả hai cùng tồn tại |
| Xoá ảnh khỏi Library | Soft delete, ẩn khỏi picker. Clone và phái sinh **không bị ảnh hưởng**, vẫn render | Giữ tới khi hết tham chiếu |

> **Cám dỗ tự nhiên** khi code Operations Engine (Website Bước 5) là "xoá node thì xoá luôn ảnh cho
> sạch". Làm thế là vỡ #41. Node delete **chỉ** đụng tree, không bao giờ gọi xoá `MediaAsset`.

Ai thật sự dọn file: Hangfire job định kỳ quét record không còn được tham chiếu (tree, snapshot,
entity nghiệp vụ qua Public Contract) → soft delete → sau grace period mới xoá file thật + record.
Đó là việc của Bước 8, không phải module này.

## 6. Storage — cấu hình (`Storage` section, `Vsite.Application.Common.Imaging.StorageOptions`)

Đúng MỘT provider theo `Storage:Provider`, mặc định `LocalDisk` (Quyết định #83):

```json
"Storage": {
  "Provider": "LocalDisk",
  "LocalDiskRoot": "../../../.media"
}
```

`LocalDiskRoot` tương đối với `ContentRootPath` (`backend/src/Vsite.Api`) → mặc định
`<repo>/.media` (đã gitignore, A10 `Docs/tasks/MEDIA-001/plan.md` §10).

Chuyển sang S3/MinIO — đổi `Provider` thành `"S3"` và điền `S3`, **không commit secret thật** vào
`appsettings.*.json`. Ví dụ (chỉ để tham khảo, đặt secret qua biến môi trường hoặc user-secrets khi
dùng thật):

```jsonc
// KHÔNG commit bản có secret thật — ví dụ tham khảo cho appsettings.Development.local.json
// hoặc biến môi trường (Storage__S3__AccessKey, Storage__S3__SecretKey)
"Storage": {
  "Provider": "S3",
  "S3": {
    "BucketName": "vsite-media-dev",
    "ServiceUrl": "http://localhost:9000",   // MinIO local — null nếu dùng AWS S3 thật
    "Region": "ap-southeast-1",
    "ForcePathStyle": true,                  // bắt buộc true cho MinIO
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin"
  }
}
```

⚠️ Bất kể server S3-compatible nào: **phải hỗ trợ conditional write** (`If-None-Match`) — đây là cơ
chế no-overwrite (#75), `S3ObjectStorage.PutAsync` dựa vào 412 `PreconditionFailed` để ném
`ObjectAlreadyExistsException`. MinIO hỗ trợ từ ngay sau `RELEASE.2023-01-31` (minio/minio#16551);
bản `RELEASE.2023-01-31` (default cũ của gói Testcontainers.Minio) im lặng bỏ qua `IfNoneMatch`.

**Đổi provider test (2026-09-27):** `minio/minio` không còn publish image public — cả Docker Hub lẫn
quay.io đều từ chối pull (xác nhận thủ công khi chạy full-suite Docker lần đầu, `Docs/DOCKER-TEST-DEBT.md`
lịch sử). `S3ObjectStorageTests` (`backend/tests/IntegrationTests/Imaging/`) đổi sang
**`localstack/localstack:4.0.3`** (gói `Testcontainers.LocalStack`, cùng bản `3.10.0` với
`Testcontainers.PostgreSql`/`Redis`) — đã pull-verify tồn tại thật trên Docker Hub và verify LocalStack
hỗ trợ conditional write đúng qua `Put_twice_same_key_throws_and_keeps_original_content`. Không cần
cấu hình flexible-checksum riêng (`RequestChecksumCalculation`/`ResponseChecksumValidation`) cho
LocalStack ở bản này — thử không set cũng pass, nên **không** thêm vào `S3ObjectStorage.cs` (production
code không đổi). Nếu provider khác từ chối checksum mặc định của AWSSDK.S3, cấu hình hai property đó ở
`AmazonS3Config` khi tạo client.

⚠️ Thứ tự set property trên `AmazonS3Config` khi vừa dùng `ServiceURL` vừa dùng `RegionEndpoint` quan
trọng: hai property này loại trừ nhau ở AWSSDK.S3, set property SAU sẽ âm thầm xoá property set
TRƯỚC. Set `RegionEndpoint` trước, `ServiceURL` sau (đúng thứ tự `S3ObjectStorage` constructor) — đảo
ngược thứ tự khiến SDK rơi về endpoint AWS thật (`https://s3.amazonaws.com`) mà không báo lỗi rõ ràng
nào ngoài "AWS Access Key Id ... does not exist" (vì credential test không tồn tại ở AWS thật) — bẫy
đã gặp khi viết lại `S3ObjectStorageTests` cho LocalStack.

## 7. Upload — giới hạn (`Imaging:Upload` section, `ImageUploadOptions`, Quyết định #85)

```json
"Imaging": {
  "Upload": {
    "MaxBytes": 10485760,
    "MaxPixels": 25000000,
    "MaxLongEdge": 1600,
    "WebpQuality": 82
  }
}
```

`MaxBytes` (10 MB) giới hạn dung lượng file thô trước khi decode. `MaxPixels` (25 triệu) giới hạn
tổng số pixel, kiểm tra từ header **trước khi decode** — chặn ảnh "bomb" (kích thước nhỏ trên đĩa,
giải nén ra khổng lồ). Endpoint set thêm `IHttpMaxRequestBodySizeFeature.MaxRequestBodySize` = 11 MB
(dư 1 MB cho phần multipart boundary khác `MaxBytes` của riêng file ảnh) **trước khi** đọc
`ReadFormAsync` lần đầu — vượt ngưỡng này Kestrel tự ném 413 (`MEDIA_FILE_TOO_LARGE`), tách biệt với
422 `MEDIA_FILE_TOO_LARGE`-logic (nếu có) ở tầng validate sau decode.

## 8. Error code đã thêm (module `Media`)

`422` (validate/xử lý ảnh thất bại) trừ khi ghi chú khác:

| `error_code` | Khi nào |
|---|---|
| `MEDIA_UNKNOWN_PRESET` | Preset trong request không có trong `image-presets.json` |
| `MEDIA_FILE_TOO_LARGE` | **413** — vượt `IHttpMaxRequestBodySizeFeature.MaxRequestBodySize` (Kestrel) |
| `MEDIA_MULTIPART_REQUIRED` | **415** — request không phải `multipart/form-data` |
| `MEDIA_FILE_MISSING` | Multipart hợp lệ nhưng thiếu field `file` hoặc file rỗng |
| `MEDIA_OWNER_REQUIRED` | **403** — thao tác chỉ Owner (`DELETE library`, `PUT logo`) mà caller không phải Owner |
| `MEDIA_HEIC_UNSUPPORTED` | File HEIC/HEIF |
| `MEDIA_UNSUPPORTED_FORMAT` | Định dạng khác không hỗ trợ (vd. GIF) |
| `MEDIA_TOO_MANY_PIXELS` | Vượt `MaxPixels` |
| `MEDIA_CORRUPT_IMAGE` | Không decode được / magic bytes không khớp |
| `MEDIA_INVALID_IMAGE_REFERENCE` | Định nghĩa ở `IMediaReferenceValidator` (T8) — **chưa có endpoint gọi tới**, dự phòng cho module `Website` dùng khi validate tree |

Focal point ngoài `[0, 1]` **không** có mã riêng — đi qua `ValidationBehavior` chung, `error_code:
VALIDATION_ERROR` (cùng tiền lệ `UpdateShopValidator` reserved-slug). Binding-level 400 (guid/page
sai định dạng) cũng không có `error_code` — đây là lỗi binding ASP.NET Core mặc định, không đi qua
`AppExceptionHandler`.

⚠️ **`MEDIA_MULTIPART_REQUIRED` (415) không đi qua `AppExceptionHandler`** dù `MediaEndpoints.RequireMultipart`
ném `UnsupportedMediaTypeException` — 3 endpoint multipart (`slot-uploads`, `library` upload, `PUT
.../logo`) khai `.Accepts<T>("multipart/form-data")` cho OpenAPI, và metadata đó (`IAcceptsMetadata`)
cũng bị routing (`ConsumesMatcherPolicy`) dùng để loại endpoint khỏi candidate set khi Content-Type
không khớp — routing tự trả 415 **trước khi endpoint chạy**, `RequireMultipart` không kịp ném gì cả,
mặc định response rỗng body (phát hiện lúc chạy full-suite Docker lần đầu, xem
`Docs/DOCKER-TEST-DEBT.md` lịch sử).

⚠️ **415 rỗng body KHÔNG chỉ xảy ra cho endpoint multipart** — Minimal API tự gắn `IAcceptsMetadata`
cho MỌI endpoint có tham số body suy luận (kể cả JSON, vd. `POST /auth/login` với `LoginRequest`,
`POST .../media/library/{assetId}/clones` với `CloneRequest`, `POST /shops`, `PATCH /shops/{shopId}`)
— gửi sai Content-Type cho các route này cũng bị routing loại và trả 415 rỗng body theo đúng cơ chế
trên, dù đó KHÔNG phải "thiếu multipart". Review Gate 2 (D1) bắt lỗi: bản xử lý đầu tiên gắn cứng
`MEDIA_MULTIPART_REQUIRED` cho MỌI 415 rỗng body — phát biểu sai sự thật (#19), đổi hành vi của module
khác (Identity, Shop) và cả endpoint JSON của chính Media (`clones`). Sửa bằng
`Vsite.Api.ExceptionHandling.MultipartRouteMatcher`: quét một lần (lazy, từ `EndpointDataSource`) toàn
bộ `RouteEndpoint` có `IAcceptsMetadata` chứa đúng `multipart/form-data`, rồi so khớp request hiện tại
theo HTTP method + `RoutePattern` (`TemplateMatcher`) — **không** dùng `HttpContext.GetEndpoint()` sau
415 (đó là endpoint tổng hợp của routing, không mang metadata thật) và **không** dùng path prefix (
`/shops/{shopId}/media/library/{assetId}/clones` share tiền tố với route multipart thật nhưng bản
thân là JSON).

Xử lý bằng `Vsite.Api.ExceptionHandling.UnsupportedMediaTypeStatusCodeHandler` qua
`app.UseStatusCodePages(...)` (Program.cs, ngay sau `UseExceptionHandler`) — middleware này CHỈ can
thiệp khi `Response.StatusCode == 415` **và** `MultipartRouteMatcher` xác nhận request khớp đúng một
route multipart thật; mọi 415 khác (route JSON sai Content-Type, module khác, …) đi qua nguyên vẹn,
rỗng body như hành vi mặc định của framework. Không sửa `.Accepts<T>()`/không đổi OpenAPI output (đã
verify hash `contracts/openapi/.staging/media.v1.json` không đổi trước/sau).

## 9. Base class + tổ chức thư mục

- `MediaAsset` → `ShopAuditableEntity` (tenant-scoped, có soft delete qua `IsDeleted` — thay
  `DeletedAt` của `08`, xem `Docs/tasks/MEDIA-001/changelog.md` A3).
- File của module: `Vsite.Domain/Media/{Entities,Enums}/` ·
  `Vsite.Application/Media/{Interfaces,Dtos,Commands,Queries}/` ·
  `Vsite.Application/Common/Imaging/` (pipeline dùng chung, không thuộc riêng module nào) ·
  `Vsite.Infrastructure/Media/` + `Vsite.Infrastructure/Imaging/` (LocalDisk/S3 `IObjectStorage`,
  `ImageSharpImageProcessor`, preset catalog) + `Vsite.Infrastructure/Persistence/Configurations/Media/` ·
  `Vsite.Api/Media/MediaEndpoints.cs` + `Vsite.Api/Media/MediaFileMiddleware.cs`.
- `Media.dependsOn = ["Shop"]` (`Docs/architecture/dependency-map.json`) — Media dùng
  `IShopLogoWriter`/`IShopOwnershipService` (Public Contract phía Shop) và cài adapter cho `IShopLogoReader` (port của Shop, D4), Shop **không** reference
  Media ngược lại.

## 10. Lệch có chủ đích / chưa làm xong so với `DesignIdeal/08-media-asset-design.md`

> 📌 Danh sách đầy đủ, theo từng task T1–T9: `Docs/tasks/MEDIA-001/changelog.md`.
> 📌 Nợ test cần Docker (không chạy được trong sandbox không có Docker daemon):
> `Docs/DOCKER-TEST-DEBT.md`, mục `MEDIA-001`.

- `IsDeleted` (kế thừa `ShopAuditableEntity`) thay `DeletedAt` riêng — quy ước entity chung của
  codebase, không phải lệch nghiệp vụ (A3).
- Picker MVP hiện thẳng file bản Library (≤ 1600px, lazy load), chưa sinh thumbnail riêng cho picker
  (A7 — tối ưu hoãn tới khi có nhu cầu thật).
- `Cache-Control: public, max-age=3600` (không phải `immutable`) cho `/media/*` tới Bước 8, khi cache
  publish được chốt cùng lúc (A8).
