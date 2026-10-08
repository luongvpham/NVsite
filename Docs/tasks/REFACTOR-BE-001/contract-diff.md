# REFACTOR-BE-001 — contract diff

## ⚠️ BREAKING / REMOVED
(không có)

## NEW_ENDPOINT
(không có)

## ADDITIVE
(không có)

## UNCHANGED
22 operation không đổi.

## Giả định tôi đã tự đặt (không hỏi)
- Status code và `error_code` của 3 endpoint Owner-only giữ nguyên (`403 SHOP_OWNER_REQUIRED` /
  `MEDIA_OWNER_REQUIRED`) — chỉ đổi **nơi** kiểm (endpoint filter thay vì handler). Hệ quả hành vi
  không lộ ra contract: non-Owner gửi body sai giờ nhận 403 thay vì 422/415 (quyền được kiểm trước
  validation/parse multipart). Xem `changelog.md` mục 2.
- `ChangePassword`/`ResetPassword`/`ForgotPassword` giờ thật sự trả 422 khi input sai — 422 đã có
  sẵn trong contract (Quyết định #19), chỉ là trước đây không bao giờ xảy ra.

## Câu hỏi cần anh quyết
(không có — Gate 1 gọn #89: không thêm, không bỏ, không câu hỏi → không cần promote)
