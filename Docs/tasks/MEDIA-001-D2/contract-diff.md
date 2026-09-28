# MEDIA-001-D2 — contract diff

## ⚠️ BREAKING / REMOVED
- **BREAKING** `POST /shops/{shopId}/media/slot-uploads` (media.v1.json) — chưa deploy production thì cứ sửa BE + regen FE, KHÔNG tạo v2:
  - ~ field 'libraryAsset' đổi shape

## NEW_ENDPOINT
(không có)

## ADDITIVE
(không có)

## UNCHANGED
20 operation không đổi.

### Thay đổi thật sự là gì

Schema trùng `MediaAssetDto2` bị bỏ. `SlotUploadResultDto.libraryAsset` đổi:

```
trước:  "libraryAsset": { "$ref": "#/components/schemas/MediaAssetDto2" }     ← MediaAssetDto2 = bản sao, nullable
sau:    "libraryAsset": { "allOf": [ { "$ref": "#/components/schemas/MediaAssetDto" } ], "nullable": true }
```

Orval sinh `libraryAsset?: MediaAssetDto | null` (một type duy nhất). JSON runtime **không đổi**: vẫn là
object `MediaAssetDto` hoặc `null`. Chưa FE nào dùng contract cũ, vì F1 đã dừng ở bước kiểm này.
Contract `shop` và `identity` không đổi.

Test: IntegrationTests 221 pass / 1 skip (Docker thật), ArchitectureTests 8/8. Có test dựng pipeline
OpenAPI thật (TestServer) và test chặn cặp `X`/`X2` trùng trong contract staging.

## Giả định tôi đã tự đặt (không hỏi)

- Sửa bằng transformer chung trong `backend/src/Vsite.Api/OpenApi/`, đăng ký cho **mọi** document
  module (identity, shop, media), không phải một bản vá riêng cho `media`.
- Transformer áp cho **mọi** property object nullable, không chỉ chỗ đang bị trùng. Framework không cho
  biết lúc chạy schema transformer rằng có "bản non-null" cùng type hay không.
- Nếu một id schema bị hai class khác nhau cùng chiếm, transformer **throw lúc generate** thay vì
  âm thầm trỏ nhầm.
- Gắn với .NET 9 / OpenAPI.NET v1. Lên .NET 10 phải xem lại (có TODO trong code).

## Câu hỏi cần anh quyết

1. Promote lại `media.v1.json` với thay đổi trên?
2. **Quy ước toàn dự án:** mọi property object nullable về sau đều xuất ra dạng
   `{ allOf: [$ref], nullable: true }`. Chấp nhận thì cấp **#87** ở `DECISIONS.md`.

## Người duyệt đã quyết

| Câu hỏi | Quyết định | Số hiệu |
|---|---|---|
| 1 | ok | |
| 2 | ok | |
