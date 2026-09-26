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
- Lỗi "preset lạ"/"focal ngoài [0,1]" đi qua `ValidationBehavior` chung (FluentValidation) → envelope
  `error_code: VALIDATION_ERROR`, 422 — giống tiền lệ `UpdateShopValidator` (reserved slug), KHÔNG
  tạo `error_code` riêng `MEDIA_UNKNOWN_PRESET` ở top-level (message field có nhắc mã này để debug).
- `UploadToLibraryCommand` không có `FocalX`/`FocalY` (đúng bảng lệnh trong brief) — bản Library luôn
  ghi focal mặc định (0.5, 0.5); FE tự chỉnh focal sau, nếu cần, qua endpoint khác (ngoài phạm vi T5).
- `MediaAssetWriter`/`TimeProvider` đăng ký ở `AddInfrastructure()` (điểm wiring DUY NHẤT theo
  backend/CLAUDE.md) dù `MediaAssetWriter` là type của Application — không có clock abstraction có
  sẵn trong codebase nên dùng thẳng `TimeProvider.System`.

## Câu hỏi cần anh quyết
(không có — hai endpoint khớp đúng bảng đã chốt ở Gate 1 của brief)
