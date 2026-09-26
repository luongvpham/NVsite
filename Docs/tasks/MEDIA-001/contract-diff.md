# MEDIA-001 — contract diff

Diff runtime `contracts/openapi/.staging/{media,shop}.v1.json` vs committed
`contracts/openapi/shop.v1.json` (media chưa từng promote → toàn bộ là NEW_ENDPOINT). Sinh bằng
`pnpm contract:export media shop && pnpm contract:diff MEDIA-001 media shop` (`tools/contract-sync`),
2026-09-27, sau khi T1–T9 đã merge (`dotnet build backend/vsite.sln`: 0 warning/0 error).

## ⚠️ BREAKING / REMOVED

- **BREAKING** `POST /shops` (shop.v1.json) — thêm field **required** `logoId`
- **BREAKING** `GET /shops/{shopId}` (shop.v1.json) — thêm field **required** `logoId`
- **BREAKING** `PATCH /shops/{shopId}` (shop.v1.json) — thêm field **required** `logoId`

Cả ba đều CÙNG một thay đổi (`ShopDto` thêm `LogoId`, T7). Diff tool phân loại "thêm field required
vào response object" là `BREAKING` theo quy tắc mặc định — bản chất field này **nullable**
(`"logoId": { "type": "string", "format": "uuid", "nullable": true }`, `Guid? LogoId` ở C#), chỉ
"required" theo nghĩa OpenAPI là **luôn có mặt trong JSON** (server luôn serialize field này, giá
trị có thể `null`), không phải "luôn có giá trị". Đúng tinh thần additive mà brief T10 mô tả — client
cũ bỏ qua field lạ vẫn chạy bình thường, không có field nào bị xoá hay đổi kiểu.

**Theo luật giai đoạn hiện tại** (chưa deploy production, `DesignIdeal/ai-agent-development-workflow.md`
§6): BREAKING là bình thường, không tạo `v2`. Không cần hành động gì thêm ở BE; FE chạy lại
`pnpm gen:api` sau khi contract này được promote để có field `logoId` trong type sinh ra.

## NEW_ENDPOINT

Module `media` (toàn bộ 9 endpoint — khớp đúng bảng đã chốt ở Gate 1 của brief §"Endpoint đề xuất",
đã đối chiếu lại với route thật trong `MediaEndpoints.cs`, không thừa/thiếu endpoint nào):

- `POST /shops/{shopId}/media/slot-uploads` — member — `SlotUploadResultDto { asset, libraryAsset? }`
- `POST /shops/{shopId}/media/library` — member — `MediaAssetDto`
- `GET /shops/{shopId}/media/library` (`?page&pageSize`) — member — `PagedResult<MediaAssetDto>`
- `POST /shops/{shopId}/media/library/{assetId}/clones` — member — `MediaAssetDto`
- `GET /shops/{shopId}/media/library/{assetId}/references` — member — `MediaReferencesDto`
- `DELETE /shops/{shopId}/media/library/{assetId}` — Owner (kiểm ở handler, không phải policy riêng) — 204
- `GET /shops/{shopId}/media/assets` (`?ids=…`) — member — `MediaAssetDto[]`
- `GET /shops/{shopId}/media/usage` — member — `MediaUsageDto { usedBytes }`
- `PUT /shops/{shopId}/logo` — Owner (kiểm ở handler) — `ShopLogoDto`

`GET /media/{relativePath}` (file serving, mọi host, T9) **không** nằm trong danh sách này — nó
KHÔNG đăng ký qua `AddOpenApi("media")`/Minimal API document, là middleware stream byte thẳng từ
`IObjectStorage`, không có request/response DTO để diễn đạt trong OpenAPI. Xem
`backend/docs/modules/media.md` §3.

## ADDITIVE

(không có ngoài mục `logoId` đã liệt ở BREAKING — xem giải thích trên)

## UNCHANGED

1 operation không đổi: `GET /shops` (`ShopSummaryDto` — không mang `logoId`, không đổi bởi T7).

## Auth policy

Mọi 9 endpoint `media`/`logo`: `RequireAuthorization(AuthPolicies.RequireGlobalScope)` +
`.RequireShopMembership()` (audience Portal, JWT `RequireGlobalScope`, membership active tại
`{shopId}` — 403 `SHOP_ACCESS_DENIED` nếu không phải member). Riêng `DELETE .../library/{assetId}`
và `PUT .../logo`: thêm điều kiện Owner, kiểm **trong handler** (không phải ASP.NET Core policy
riêng ở tầng endpoint, cùng khuôn `UpdateShopHandler` — #21.5) → 403 `MEDIA_OWNER_REQUIRED` nếu
member nhưng không phải Owner.

## Giả định tôi đã tự đặt (không hỏi)

**Từ plan §10 (A3–A11, `Docs/tasks/MEDIA-001/plan.md`) — mọi mục đều ghi "Duyệt ở: Gate 1", xin duyệt ở đây:**

- **A3** — `IsDeleted` (kế thừa `ShopAuditableEntity`) thay cho `DeletedAt` riêng trong `08`; tên
  bảng `MediaAsset` theo quy ước code (không phải `media_assets` snake_case như draft thiết kế).
- **A4** — Mọi lỗi từ chối ảnh (format, quá pixel, corrupt, preset lạ, focal ngoài range) trả **422**
  + `error_code`; chỉ vượt `RequestSizeLimit` của Kestrel (11 MB) mới ra 413.
- **A5** — Không upscale: ảnh nhỏ hơn preset thì record có `Width`/`Height` nhỏ hơn preset thật,
  nhưng `Preset` vẫn ghi đúng preset slot (đúng tỉ lệ, chỉ mờ hơn khi FE phóng to).
- **A6** — `GetAssetsByIds` trả cả asset đã soft-delete (cùng shop), để clone của ảnh đã xoá vẫn
  render đúng (#72).
- **A7** — Picker MVP hiện thẳng file bản Library (≤ 1600px, lazy load), chưa sinh thumbnail riêng
  cho picker.
- **A8** — `Cache-Control: public, max-age=3600` (không phải `immutable`) cho `/media/*` tới Bước 8.
- **A9** — Test không cần container (pipeline, paths, LocalDisk) đặt trong `IntegrationTests/Imaging/`,
  không tạo project test thứ tư.
- **A10** — Thư mục LocalDisk mặc định `.media/` ở root repo (gitignore, tương đối với ContentRoot
  của `Vsite.Api`: `../../../.media`); prod đặt `Storage:LocalDiskRoot` hoặc chuyển `Provider: S3`.
- **A11** — Xoá bản Library đang là `Shop.LogoId` được PHÉP (chỉ cảnh báo qua `.../references`,
  không chặn). FK ghép trỏ vào row soft-delete vẫn hợp lệ, logo vẫn hiện.

**OpenAPI/codegen quirks phát hiện khi review, FE cần biết trước khi chạy Orval:**

- Schema `MediaAssetDto2` là **bản trùng** của `MediaAssetDto` trong `media.v1.json` — .NET 9
  `Microsoft.AspNetCore.OpenApi` tự tách schema khi cùng một C# type xuất hiện với nullability khác
  nhau ở hai chỗ (ở đây: `SlotUploadResultDto.libraryAsset` là `MediaAssetDto?`, còn các chỗ khác
  dùng `MediaAssetDto` non-null) — sinh ra hai schema node giống hệt field cho cùng một DTO. Chưa
  rõ Orval xử lý việc này thế nào (gộp lại hay sinh 2 type TS trùng lặp) — cần kiểm khi chạy `pnpm
  gen:api` lần đầu, có thể cần fix riêng ở tầng generate hoặc normalize thêm một bước.
- `UploadToSlotForm`/`UploadToLibraryForm`/`UploadShopLogoForm` (multipart request body) **không**
  có mảng `"required"` trong schema — kể cả field bắt buộc thật (`file`, `preset`). Nguyên nhân: hai
  class này chỉ tồn tại để khai `.Accepts<T>("multipart/form-data")` cho mục đích tài liệu OpenAPI,
  không phải type bind thật (binding thật đọc tay qua `HttpRequest.ReadFormAsync`, xem
  `backend/docs/modules/media.md` §7 và XML doc trên `MediaEndpoints`) — nên các property không có
  validation attribute nào để `Microsoft.AspNetCore.OpenApi` suy ra `required`. Hệ quả: Orval sẽ
  sinh `file`/`preset` là optional trong form TS type dù thực chất bắt buộc (thiếu → 422
  `MEDIA_FILE_MISSING` / `MEDIA_UNKNOWN_PRESET` ở runtime) — FE cần tự validate bằng Zod thủ công
  hoặc form validation riêng, không dựa vào type sinh ra.
- `GET /shops/{shopId}/media/assets?ids=…` — `ids` là mảng `Guid` bind thẳng theo tên tham số
  Minimal API (không `[FromQuery]`), style mặc định `form`/`explode=true` → gửi dạng lặp lại
  `?ids=<guid1>&ids=<guid2>`, KHÔNG phải comma-separated `?ids=a,b`.
- `error_code` không xuất hiện trong OpenAPI schema property dạng literal/enum (chỉ khai
  `"type": "string"` ở `ProblemDetails.error_code`) — danh sách mã lỗi mới của module này nằm ở
  `backend/docs/modules/media.md` §8, không đọc được từ contract JSON:
  `MEDIA_UNKNOWN_PRESET`, `MEDIA_FILE_TOO_LARGE` (413), `MEDIA_MULTIPART_REQUIRED` (415),
  `MEDIA_OWNER_REQUIRED` (403), `MEDIA_FILE_MISSING`, `MEDIA_HEIC_UNSUPPORTED`,
  `MEDIA_UNSUPPORTED_FORMAT`, `MEDIA_TOO_MANY_PIXELS`, `MEDIA_CORRUPT_IMAGE`,
  `MEDIA_INVALID_IMAGE_REFERENCE` (định nghĩa sẵn ở `IMediaReferenceValidator`, T8 — **chưa có
  endpoint nào trả mã này**, dự phòng cho module `Website` dùng khi validate tree sau này). Focal
  point ngoài `[0,1]` KHÔNG có mã riêng, đi qua `ValidationBehavior` chung → `VALIDATION_ERROR`.
- Binding-level 400 (path/query param sai định dạng, vd. `shopId` không phải GUID hợp lệ,
  `page`/`pageSize` không phải số) là lỗi binding mặc định của ASP.NET Core, KHÔNG đi qua
  `AppExceptionHandler` → ProblemDetails không có `error_code`.
- `shop.v1.json`: `ShopDto` thêm field additive-về-bản-chất `logoId` (`Guid?`, nullable) — xem mục
  BREAKING/REMOVED ở trên cho lý do vì sao diff tool xếp loại BREAKING dù hành vi runtime là
  additive.
- Hai Public Contract cross-module MỚI (không xuất hiện trong OpenAPI vì là C# interface nội bộ,
  không phải HTTP API — nêu ở đây để FE/reviewer hiểu ranh giới, không cần hành động gì): module
  `Shop` thêm `IShopOwnershipService` và `IShopLogoWriter` (`Vsite.Application.Shop.Interfaces`),
  cả hai được `Media` gọi (`Media.dependsOn = ["Shop"]`) để authorize và ghi `Shop.LogoId` mà không
  reference thẳng entity `Shop`. Xem `backend/docs/modules/media.md` §4.
- `/media/*` (file serving) **không** có trong OpenAPI theo thiết kế (`08` §1, #53 — không phải
  image proxy runtime, không có request/response DTO cần diễn đạt) — không phải thiếu sót, xem
  `backend/docs/modules/media.md` §3.

## Câu hỏi cần anh quyết

(không có câu hỏi mở — toàn bộ giả định ở trên đều đã có lý do rõ ràng để tự quyết theo đúng phạm vi
plan §10 đã định trước; điểm duy nhất cần xác nhận là BREAKING `logoId` ở trên có chấp nhận được
không, và Orval xử lý `MediaAssetDto2` có cần fix thêm ở tầng codegen hay để nguyên)

## Người duyệt đã quyết

(điền lúc Gate 1 — để trống cho người duyệt)
