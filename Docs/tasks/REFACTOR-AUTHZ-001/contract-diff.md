# REFACTOR-AUTHZ-001 — contract diff

## ⚠️ BREAKING / REMOVED
(không có)

## NEW_ENDPOINT
(không có)

## ADDITIVE
(không có)

## UNCHANGED
22 operation không đổi.

## Giả định tôi đã tự đặt (không hỏi)
- Shape API không đổi; hành vi đổi theo Quyết định #90 (người duyệt chốt trước khi code) — mọi status
  code mới phát sinh (401 login/refresh, 400 `RESET_TOKEN_INVALID`, 409 đăng ký lại) đều đã khai sẵn
  trong contract.
- `User.Status != Active` cũng chặn login/refresh/forgot-password — cùng loại lỗi, phát hiện khi làm
  task (xem changelog mục 4).

## Câu hỏi cần anh quyết
(không có — 4 câu đã hỏi và được chốt trước khi code, ghi ở Quyết định #90)
