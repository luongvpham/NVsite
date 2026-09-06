# SAMPLE-001 — contract diff

## ⚠️ BREAKING / REMOVED
(không có)

## NEW_ENDPOINT
- GET /samples
- POST /samples
- GET /samples/{id}
- GET /shops/{shopId}/samples/{id}

## ADDITIVE
(không có)

## UNCHANGED
0 operation không đổi.

## Giả định tôi đã tự đặt (không hỏi)
- `page`/`pageSize` không truyền → mặc định `page=1`, `pageSize=20`; `pageSize` chặn trần ở 100.
- `Sample` có `ShopId` (không nullable) dù throwaway, chỉ để endpoint nested `/shops/{shopId}/samples/{id}` có ý nghĩa filter thật (WHERE ShopId = ... AND Id = ...) thay vì giả vờ.
- Validation dùng FluentValidation qua MediatR pipeline behavior (`ValidationBehavior<TRequest,TResponse>`) — ném `FluentValidation.ValidationException`, `Api` host bắt bằng `IExceptionHandler` chung cho mọi module, không viết exception handler riêng cho Sample.
- Không có auth/audience trên endpoint Sample — Bước 1 chưa có Identity, đây chỉ là chỗ chứng minh pipeline.
- OpenAPI document name = tên module viết thường (`sample`), khớp `WithGroupName`; file xuất từ `Microsoft.Extensions.ApiDescription.Server` có tên `Api_{document}.json`, script `export.mjs` tự rename thành `{module}.v1.json`.

## Giới hạn kỹ thuật cần biết (không phải giả định nghiệp vụ)
- `tools/contract-sync/lib/diff.mjs` chỉ resolve `$ref` một cấp (schema gắn trực tiếp ở request/response của operation). Field lồng sâu hơn một cấp có thể không bị bắt là BREAKING/ADDITIVE — cần mở rộng khi module thật có schema lồng nhau (vd. `Shop` chứa `ShopDomain[]`).
- `SampleStatus` enum trong OpenAPI schema sinh ra thiếu `"type": "string"` tường minh (chỉ có mảng `enum` giá trị string) — hạn chế của bộ sinh OpenAPI built-in .NET 9 khi dùng `JsonStringEnumConverter`. Orval thường tự suy luận type từ giá trị enum, nhưng cần xác nhận khi Orval chạy thật ở Bước 1C.

## Câu hỏi cần anh quyết
(không có — đây là module throwaway, không có quyết định nghiệp vụ nào cần chốt)
