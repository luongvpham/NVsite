# IDENTITY-001 — contract diff

## Round 2 (Phase 3 — auth policy + protected endpoints) — 2026-09-13

## ⚠️ BREAKING / REMOVED
(không có)

## NEW_ENDPOINT
- GET /auth/me
- POST /auth/me/change-password
- GET /auth/me/shops

## ADDITIVE
(không có)

## UNCHANGED
6 operation không đổi (6 endpoint auth flow của Round 1).

## Giả định tôi đã tự đặt (không hỏi)

Ba endpoint này được thêm **có chủ đích** để có mục tiêu thật cho test bắt buộc theo `03` §7.1
("với mỗi endpoint identity, một test khẳng định token `shop:*` bị từ chối") — trước đó cả 6
endpoint Round 1 đều `AllowAnonymous`, không có endpoint nào để test `RequireGlobalScope` cho thật.
Đã hỏi và được xác nhận rõ ràng (không phải tôi tự quyết): user chọn "Thêm cả 3 endpoint" thay vì
chỉ thêm tối thiểu 1.

- `GET /auth/me` — `RequireAuthorization()` (mọi audience). Trả `MeDto` (UserId/Email/FullName/
  AvatarUrl/Phone/Audience) đọc từ JWT claims qua `ICurrentUserContext`, không chạm DB gì thêm ngoài
  load `User` — chọn vì đây là use-case tối thiểu, không nghiệp vụ mới.
- `GET /auth/me/shops` — `RequireAuthorization(RequireGlobalScope)`. Đây chính là endpoint bị chặn
  cho token `shop:*` (Quyết định #32 — token thuộc shop không được đọc danh sách MỌI shop user sở
  hữu). Trả `ownerShopIds` (đã có trong JWT claim, dùng lại — không query lại DB).
- `POST /auth/me/change-password` — `RequireAuthorization()` (mọi audience, vì #32 cho phép "đổi
  password của chính shop đó" khi gọi bằng token `shop:{shopId}`). Chọn endpoint này (thay vì một
  endpoint đọc/ghi dữ liệu nghiệp vụ khác) vì logic "đổi đúng credential theo scope" đã tồn tại sẵn
  trong thiết kế Login/VerifyEmail (`User.PasswordHash` vs `UserShop.PasswordHash`), không cần entity
  mới.

Không endpoint mới nào query dữ liệu tenant-scoped ngoài `UserShop` của chính user đang đăng nhập
(qua `ICurrentUserContext.UserId`, không nhận input từ client) — không phát sinh invariant tenant
isolation mới.

## Câu hỏi cần anh quyết
(không có)
