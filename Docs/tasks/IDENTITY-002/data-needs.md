# IDENTITY-002 — Nhu cầu dữ liệu (FE → BE)

## Màn / điểm chạm

| Màn | Dữ liệu cần |
|---|---|
| `/login` ([login.tsx](../../../apps/portal/src/routes/login.tsx)) | Response login: `accessToken`, `accessTokenExpiresAt`. **Không** còn `refreshToken` — BE set cookie `vsite_rt`. |
| Route guard `_authenticated` ([_authenticated.tsx](../../../apps/portal/src/routes/_authenticated.tsx)) | Khi tải lại trang (store rỗng): gọi `refreshToken()` không body; 200 → `{ accessToken, accessTokenExpiresAt }`; 401 → về `/login`. |
| Nút "Đăng xuất" (cùng file) | `POST /auth/logout` không body → 204. FE luôn clear state kể cả khi gọi lỗi mạng. |

## Mỗi item / toàn trang

- Không có danh sách. Mọi thứ là response đơn của 3 endpoint auth.

## Câu hỏi mở

Không còn. Đã chốt với người (2026-10-18): thêm `/auth/logout`; cookie `HttpOnly; Secure; SameSite=Lax;
Path=/api/auth`, host-only; response không trả `refreshToken`. Chi tiết thực thi xem `plan.md` §4.
