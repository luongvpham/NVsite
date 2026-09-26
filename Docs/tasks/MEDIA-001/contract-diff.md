# MEDIA-001 — contract diff

## ⚠️ BREAKING / REMOVED
(không có)

## NEW_ENDPOINT
- POST /shops/{shopId}/media/library
- POST /shops/{shopId}/media/slot-uploads

## ADDITIVE
(không có)

## UNCHANGED
0 operation không đổi.

## Giả định tôi đã tự đặt (không hỏi)
- Đọc multipart form thủ công (`HttpRequest.ReadFormAsync`) thay vì `[FromForm]` complex-type
  auto-binding, để set `IHttpMaxRequestBodySizeFeature.MaxRequestBodySize` (11 MB) ĐÚNG LÚC trước
  khi body được đọc lần đầu — binding tự động của Minimal API chạy trước endpoint filter nên set
  giới hạn trong filter sẽ quá trễ. OpenAPI vẫn khai `.Accepts<UploadToSlotForm>("multipart/form-data")`
  cho mục đích tài liệu.
- "Preset lạ" → `UploadToSlotHandler` tự ném `UnprocessableException("MEDIA_UNKNOWN_PRESET", …)`
  (KHÔNG qua FluentValidation) — đúng `error_code` literal brief yêu cầu. "Focal ngoài [0,1]" VẪN qua
  `ValidationBehavior` chung → `error_code: VALIDATION_ERROR`, 422 (giống tiền lệ `UpdateShopValidator`
  reserved-slug) — brief không đòi mã riêng cho case này.
- Request không phải `multipart/form-data` → 415 `error_code: MEDIA_MULTIPART_REQUIRED`
  (`UnsupportedMediaTypeException`, mới thêm ở `Vsite.Domain.Exceptions`). Vượt giới hạn 11 MB → 413
  `error_code: MEDIA_FILE_TOO_LARGE` (`BadHttpRequestExceptionHandler`, mới thêm ở
  `Vsite.Api.ExceptionHandling`, dùng chung mọi module set `IHttpMaxRequestBodySizeFeature` sau này).
- `UploadToLibraryCommand` không có `FocalX`/`FocalY` (đúng bảng lệnh trong brief) — bản Library luôn
  ghi focal mặc định (0.5, 0.5); FE tự chỉnh focal sau, nếu cần, qua endpoint khác (ngoài phạm vi T5).
- `MediaAssetWriter`/`TimeProvider` đăng ký ở `AddInfrastructure()` (điểm wiring DUY NHẤT theo
  backend/CLAUDE.md) dù `MediaAssetWriter` là type của Application — không có clock abstraction có
  sẵn trong codebase nên dùng thẳng `TimeProvider.System`.

## Câu hỏi cần anh quyết
(không có — hai endpoint khớp đúng bảng đã chốt ở Gate 1 của brief)
