# IDENTITY-001 — FE brief

## Contract
contracts/openapi/identity.v1.json   (sha256: 3e66f8282686a6e1c292e4efaea01cd301a4a2efaf2c3de232252a8c40c2ef3f)

Module hoàn toàn mới — 6 endpoint, MỘT bộ route duy nhất dùng chung cho mọi context (vsite.vn /
website shop / portal). **Không có `{shopId}` trong bất kỳ route nào**:

```
POST /auth/register        → { email, password, fullName? }
GET  /auth/verify-email    → ?token=… — xác minh email, tạo User (và UserShop nếu context là shop)
POST /auth/login           → { email, password } → AuthTokenResult
POST /auth/refresh-token   → { refreshToken } → AuthTokenResult mới (rotation)
POST /auth/forgot-password → { email } → 204 luôn (không lộ email có tồn tại hay không)
POST /auth/reset-password  → { token, newPassword } → 204

AuthTokenResult = { accessToken, accessTokenExpiresAt, refreshToken, refreshTokenExpiresAt }
```

## ⚠️ Quan trọng nhất — FE KHÔNG cần biết `shopId`

Backend tự resolve context (vsite.vn / shop nào / portal) từ **Host header** của chính request —
`Api.Tenancy.TenantResolutionMiddleware` chạy trước mọi endpoint, đọc nhãn đầu tiên của hostname
(vd. `spa-abc` trong `spa-abc.vsite.vn`), tra `Shop.Slug`, set context. **FE chỉ cần gọi đúng path
tương đối `/auth/login` từ domain đang phục vụ trang hiện tại** — không truyền `shopId` ở đâu cả
(không route, không query, không body).

**Hệ quả kiến trúc bắt buộc:** mọi call tới các endpoint này PHẢI same-origin với trang đang hiển
thị (relative URL, vd. `fetch('/auth/login')`), KHÔNG được gọi cross-origin tới một host `api.*`
cố định — vì Host header chỉ đúng khi request thực sự đi tới domain của chính shop đó. Production:
Caddy reverse-proxy mọi domain shop về cùng backend, giữ nguyên Host (Quyết định #9). Dev: chưa có
Caddy — xem mục "Test ở dev" bên dưới.

**Ngoại lệ:** link trong email (verify-email, reset-password) LUÔN trỏ về host cố định
`api.vsite.vn` (03 §5) — đây là hai endpoint DUY NHẤT không phụ thuộc Host header (context được mã
hoá trong chính token, không phải domain gọi tới).

## Test ở dev (chưa có Caddy)

Thêm entry vào hosts file Windows (`C:\Windows\System32\drivers\etc\hosts`, cần quyền Admin):
```
127.0.0.1  spa-abc.vsite.local
127.0.0.1  admin.vsite.local
```
Gọi thẳng `http://spa-abc.vsite.local:5270/auth/login` (Kestrel không quan tâm hostname, chỉ quan
tâm port) — Host header đúng thật, test được middleware mà không cần domain thật/DNS. `admin` là
nhãn đã reserved (Quyết định #25) → luôn resolve audience `vsite-portal`. Nhãn không khớp Shop nào
và không phải "admin" → audience `vsite-main` (áp dụng cho `localhost` khi gọi trực tiếp).

## Việc FE cần làm

- `apps/web`: form đăng ký + đăng nhập cho khách (`vsite.vn`), và cho website từng shop — CÙNG MỘT
  code path gọi `/auth/*` (relative), khác biệt chỉ ở domain đang phục vụ trang, FE không cần biết.
  Trang xác minh email: đọc `token` từ query string, gọi `/auth/verify-email`.
- `apps/portal`: form đăng nhập, cũng gọi `/auth/login` relative — Host `admin.vsite.vn` tự cho
  audience `vsite-portal`.
- Axios interceptor refresh access token khi 401 (gọi `/auth/refresh-token`, phát lại request cũ).
- Trang "Quên mật khẩu" + "Đặt lại mật khẩu".

## Ràng buộc

- Chạy `pnpm gen:api` trước. KHÔNG sửa tay `packages/api-sdk`.
- Code với MSW mock trước; chỉ bật API thật ở bước integration.
- **`accessToken` giữ trong memory, KHÔNG localStorage** (Quyết định #3).
- **`refreshToken`** — contract trả thẳng trong JSON body (không phải httpOnly cookie). ⚠️ Điểm
  CHƯA CHỐT kỹ về bảo mật lâu dài — tạm giữ cùng nguyên tắc với accessToken (memory) cho task này.
  Đổi sang cookie httpOnly là thay đổi CONTRACT, phải qua lại Gate 1.
- 401 từ `/auth/refresh-token` = phiên hết hạn thật — clear state, về login, không retry.
- `error_code`: `EMAIL_ALREADY_REGISTERED` / `EMAIL_ALREADY_REGISTERED_AT_SHOP` (409),
  `VERIFY_TOKEN_INVALID` / `RESET_TOKEN_INVALID` (400), `UNAUTHORIZED` (401), `VALIDATION_ERROR`
  (422, có `errors` theo field).
- `/auth/forgot-password` LUÔN trả 204 dù email tồn tại hay không — hiện đúng MỘT thông báo chung.

## Không thuộc phạm vi

- KHÔNG đụng `backend/`.
- KHÔNG sửa `contracts/` hay `config/`.
- Contract sai hoặc thiếu → DỪNG, báo cáo. Không tự sửa, không tự làm tạm.
- Social login (Google/Facebook/Zalo) — chưa có endpoint nào.
- Custom domain thật (`spa-abc.com` tuỳ ý) chưa resolve được — chỉ Path/Subdomain qua `Shop.Slug`
  hoạt động ở Bước 3 (xem CLAUDE.md module Identity). Nếu test với custom domain thật, báo lại chứ
  đừng tự thêm cơ chế resolve khác ở FE.
- Không có endpoint nào yêu cầu JWT hợp lệ ở Bước 3 (Phase 3 — auth policy — chưa làm).

## Acceptance

- [ ] Loading / error / empty state đủ cả ba cho mọi form
- [ ] Validation dùng Zod schema sinh từ contract
- [ ] Test Vitest + MSW pass cho luồng đăng ký + đăng nhập + refresh
- [ ] `error_code` map đúng sang thông báo tiếng Việt cho người dùng cuối
- [ ] `/auth/forgot-password` hiện đúng MỘT thông báo chung, không phân nhánh theo tồn tại email
- [ ] Xác nhận mọi call auth là relative URL (same-origin), không hardcode absolute API host
