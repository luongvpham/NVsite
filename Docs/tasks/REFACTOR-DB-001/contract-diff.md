# REFACTOR-DB-001 — contract diff

## ⚠️ BREAKING / REMOVED
(không có)

## NEW_ENDPOINT
(không có)

## ADDITIVE
(không có)

## UNCHANGED
22 operation không đổi.

## Giả định tôi đã tự đặt (không hỏi)
- `MediaAsset.Kind` **không** đưa ra `MediaAssetDto` — chỉ dùng nội bộ BE, giữ contract không đổi.
  Cần thì thêm sau (additive).
- Hành vi API đổi nhưng shape không đổi: `GET .../derivatives` không còn trả clone (đúng định nghĩa
  #73); `POST`/`PATCH /shops` với slug của shop đã xoá mềm trả `409 SHOP_SLUG_ALREADY_TAKEN` (409 đã
  khai sẵn) thay vì 500. `GET .../usage` không còn tính clone đã mất nguồn (chỉ xảy ra khi bản
  Library bị xoá cứng — chưa có luồng nào xoá cứng).

## Câu hỏi cần anh quyết
(không có — Gate 1 gọn #89: không thêm, không bỏ → không cần promote)
