# SAMPLE-002 — contract diff

## ⚠️ BREAKING / REMOVED
(không có)

## NEW_ENDPOINT
(không có)

## ADDITIVE
- `POST /samples`: + field 'error_code' (optional)
- `GET /samples/{id}`: + field 'error_code' (optional)
- `GET /shops/{shopId}/samples/{id}`: + field 'error_code' (optional)

## UNCHANGED
1 operation không đổi.

## Giả định tôi đã tự đặt (không hỏi)
- `error_code` khai báo `nullable: true`, không đưa vào `required` — vì fallback exception handler mặc định của ASP.NET Core (lỗi không đi qua `ApiError`/`ValidationExceptionHandler`) vẫn có thể trả ProblemDetails không có field này.
- Sửa luôn lỗ hổng trong `tools/contract-sync/lib/diff.mjs`: `extractSchemaRefs` trước đây chỉ đọc content-type `application/json`, bỏ sót `application/problem+json` (nơi ProblemDetails/error_code sống) — nghĩa là mọi thay đổi ở error schema trước đây sẽ luôn bị báo UNCHANGED sai. Đã tổng quát hoá để đọc mọi content-type.

## Câu hỏi cần anh quyết
(không có)

## Bối cảnh
Phát hiện bởi `change-reviewer` ở Gate 2 (BOOTSTRAP-001): `error_code` được gắn vào ProblemDetails qua
`Extensions` lúc runtime nên `Microsoft.AspNetCore.OpenApi` (dựa trên reflection) không tự xuất field này
vào schema — contract cũ (`contracts/openapi/sample.v1.json` đã duyệt) thiếu `error_code` dù response thật
luôn có. Fix: `backend/src/Api/OpenApi/ProblemDetailsSchemaTransformer.cs` (`IOpenApiSchemaTransformer`,
đăng ký cho mọi document OpenAPI, không riêng `sample`) — mọi module thật sau này tự động được field này,
không phải tự thêm lại.
