# IDENTITY-002 — contract diff

> `identity.v1` · staging `contracts/openapi/.staging/identity.v1.json` · Orval dry-run trên staging: **không có type trùng `…Dto2`**.
> Trước production (#89): nhãn BREAKING của tool chỉ để tham khảo; không tạo `v2`.

## Endpoint / field mới
- **Mới** `POST /api/auth/logout` (operationId `logout`) — không body, không tham số. Response `204`. Đọc cookie `vsite_rt`, thu hồi đúng token đó, xoá cookie. **Luôn 204** (không cookie / token lạ / đã revoke đều 204).
- **Mới** schema `AccessTokenResponse { accessToken: string, accessTokenExpiresAt: date-time }` — response 200 của `login` và `refreshToken`.

## Đổi so với contract đã chốt (BREAKING theo tool — đều có chủ đích, đã chốt trong `plan.md` §4)
- `POST /api/auth/login`: response 200 đổi `AuthTokenResult` → `AccessTokenResponse` (**bỏ** `refreshToken`, `refreshTokenExpiresAt`). Header `Set-Cookie: vsite_rt=…` **không** nằm trong OpenAPI (header cookie không khai trong schema).
- `POST /api/auth/refresh-token`: **bỏ** request body (`RefreshTokenRequest`); response 200 đổi sang `AccessTokenResponse`; **bỏ** response 422 (không còn input để validate; thiếu cookie → 401).
- Schema `AuthTokenResult` và `RefreshTokenRequest` biến mất khỏi contract (còn nội bộ ở Application, không ra API).
- Không operation nào bị `REMOVED`; 30 operation còn lại `UNCHANGED`.

## Promote lần hai (chỉ mô tả)
Sau review thêm `WithDescription`/`WithSummary` mô tả cookie `vsite_rt` cho `login`, `refreshToken`, `logout`. Classifier: 9 operation
không đổi; promote lại để contract (và `check-no-drift`) khớp code. Header `Set-Cookie` vẫn không khai được trong schema.

## Auth policy
- `login`, `refreshToken`, `logout`: **AllowAnonymous** (danh tính của refresh/logout là chính cookie httpOnly). Không thêm `RequireAuthorization`.
- Cookie: `HttpOnly; Secure (trừ Development); SameSite=Lax; Path=/api/auth`, host-only, `Expires` = hạn refresh token (30 ngày). FE phải gọi `withCredentials: true`.

## Giả định tôi đã tự đặt (không hỏi)
Đã người duyệt đồng ý ở plan §12 (G1–G6). Đáng nhắc lại cho FE:
- FE không bao giờ đọc/ghi refresh token; muốn biết còn phiên không thì gọi `refreshToken()` (không body).
- Refresh 401 **không** xoá cookie phía server (G3) — cookie cũ vô hại.

## Câu hỏi cần anh quyết
Không còn câu hỏi mở về hình dạng API. Còn một việc thủ tục: contract có đổi BREAKING nên cần người xác nhận promote (`tools/contract-sync/promote.mjs`) — xem bảng dưới.

## Người duyệt đã quyết
| Mục | Quyết định | Ai / khi nào |
|---|---|---|
| Thêm `POST /auth/logout`, 204 luôn | Đồng ý | luongpham · 2026-10-18 (chat lập plan) |
| Response không trả `refreshToken` trong body | Đồng ý | luongpham · 2026-10-18 |
| Cookie `HttpOnly; Secure; SameSite=Lax; Path=/api/auth`, host-only | Đồng ý | luongpham · 2026-10-18 |
| Promote `identity.v1` | Đồng ý | luongpham · 2026-10-18 (Gate 1) |
