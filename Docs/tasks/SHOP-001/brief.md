# SHOP-001 — FE brief

## Contract

```
contracts/openapi/shop.v1.json        (sha256: dc4a539023d2eb6e918681a70e9c495ff2a2d9cf3884cdc066953e8ff29d44bc)
contracts/openapi/identity.v1.json    (sha256: 77e1e507ee9cb5674a060cfd1140099b117d6b3789f3ecdcc81e0f9d3b59c92c)
```

`identity.v1.json` đổi vì **`GET /auth/me/shops` đã bị xoá** (BREAKING có chủ đích, xem
`contract-diff.md`) — nếu FE nào từng gọi endpoint đó (chưa có, IDENTITY-001 chỉ dùng nó làm mục
tiêu test), phải đổi sang `GET /shops` (module `shop.v1.json` mới, shape khác — xem dưới).

`shop.v1.json` là module hoàn toàn mới — 4 endpoint, tất cả yêu cầu JWT hợp lệ (khác Identity Round
1, vốn có endpoint `AllowAnonymous`):

```
POST   /shops              → { name, slug, kind, externalUrl? } → ShopDto
GET    /shops              → ShopSummaryDto[]  (shop mà user hiện tại là thành viên)
GET    /shops/{shopId}     → ShopDto
PATCH  /shops/{shopId}     → { name, slug, kind, externalUrl?, status } → ShopDto  (full-replace, KHÔNG phải partial patch)

ShopDto        = { id, name, slug, kind, externalUrl, status }
ShopSummaryDto = { id, name, slug, kind, status, roleCode }
kind   = "Hosted" | "ExternalOnly"
status = "Draft" | "Active" | "Suspended" | "Closed"
```

## ⚠️ Quan trọng nhất — 4 endpoint này CHỈ gọi được từ Portal (hoặc vsite.vn), KHÔNG từ website shop

Policy `RequireGlobalScope` chặn cứng token `shop:{shopId}` (Quyết định #32 — tạo/sửa shop là thao
tác quản trị, không phải hành vi trong context một shop cụ thể). Nghĩa là:

- FE gọi 4 endpoint này **chỉ** từ `apps/portal` (host `admin.vsite.vn`), KHÔNG từ `apps/web` khi
  đang phục vụ trang của một shop cụ thể.
- `GET/PATCH /shops/{shopId}` — `shopId` LẤY TỪ ROUTE PARAM của chính request (`/shops/{shopId}`),
  không phải từ domain đang phục vụ trang (khác hẳn nguyên tắc "FE không cần biết shopId" của
  Identity — đây là trường hợp NGOẠI LỆ DUY NHẤT, đúng Quyết định #31: Portal xem/quản lý nhiều shop
  cùng lúc nên PHẢI biết GUID của shop đang thao tác, lấy được từ `GET /shops` sau khi đăng nhập).

## Bảng năng lực theo audience

| Endpoint | `vsite-main`/`vsite-portal` | `shop:{shopId}` |
|---|---|---|
| `POST /shops` | ✅ | ❌ 403 `INSUFFICIENT_SCOPE` |
| `GET /shops` | ✅ | ❌ 403 `INSUFFICIENT_SCOPE` |
| `GET /shops/{shopId}` | ✅ (nếu là thành viên `Active` của shop đó) | ❌ 403 `INSUFFICIENT_SCOPE` |
| `PATCH /shops/{shopId}` | ✅ (nếu là **Owner** của shop đó) | ❌ 403 `INSUFFICIENT_SCOPE` |

Với `GET`/`PATCH /shops/{shopId}`: gọi bằng shop khác mà user không phải thành viên → 403
`SHOP_ACCESS_DENIED` (không phải 404 — đừng code FE hiểu nhầm 403 này là "shop không tồn tại").
`PATCH` bởi thành viên không phải Owner (vd. Staff, khi API mời nhân viên ra đời sau này) → 403
`SHOP_OWNER_REQUIRED`.

## Việc FE cần làm

- `apps/portal` (CSR, `admin.vsite.vn`) — **hiện mới có skeleton, chưa có auth, chưa có layout, chưa
  có màn hình nào.** Cần dựng từ đầu:
  1. Đăng ký (`POST /auth/register` — dùng lại contract Identity, gọi trên host `admin.vsite.local`
     sẽ tự cho audience `vsite-portal`).
  2. Xác minh email — lấy token từ **console log** của `LoggingEmailSender` để test thủ công (link
     verify-email vẫn trỏ endpoint BE, xem "Không thuộc phạm vi").
  3. Đăng nhập (`POST /auth/login`), lưu access/refresh token cùng nguyên tắc Identity brief (access
     token in-memory, KHÔNG localStorage — Quyết định #3).
  4. Route guard cho toàn bộ layout Portal (chưa đăng nhập → redirect login).
  5. Màn "Danh sách shop của tôi" — `GET /shops`, hiển thị `name`/`slug`/`kind`/`status`/`roleCode`.
     Đây cũng là chỗ shop switcher đọc dữ liệu (nếu Portal cho phép chuyển qua lại nhiều shop).
  6. Màn "Tạo shop" — form `POST /shops`, sau khi tạo thành công điều hướng sang màn shop vừa tạo.
  7. Màn "Sửa shop" (`/shops/{shopId}` theo route Portal) — `GET` để load form, `PATCH` để lưu. Gửi
     **toàn bộ 5 field** kể cả field không đổi (full-replace, không phải partial).

## ⚠️ Thiết lập dev bắt buộc — Host header phải đúng, nếu không audience sai

Portal phải phục vụ tại host có nhãn đầu là `admin`, nếu không
`Vsite.Api.Tenancy.TenantResolutionMiddleware` sẽ resolve thành `vsite-main` chứ không phải
`vsite-portal`:

1. hosts file Windows (`C:\Windows\System32\drivers\etc\hosts`, cần quyền Admin):
   `127.0.0.1  admin.vsite.local`
2. Vite dev server bind `admin.vsite.local`.
3. **Vite proxy `/auth` + `/shops` → `http://localhost:5270` với `changeOrigin: false`** để giữ
   nguyên Host header. Gọi cross-origin thẳng tới `localhost:5270` sẽ hỏng audience (Host thành
   `localhost` → resolve `vsite-main`, không phải `vsite-portal`) — xem
   `Docs/tasks/IDENTITY-001/brief.md` mục "FE KHÔNG cần biết shopId" để hiểu cơ chế Host-based
   resolve chung của toàn hệ.

## Ràng buộc

- Chạy `pnpm gen:api` trước. KHÔNG sửa tay `packages/api-sdk`.
- Dựng UI với **MSW mock trước** (bắt buộc — chống FE bám vào hành vi ngoài contract), chỉ tắt MSW
  ở bước integration cuối cùng (API thật, chạy trên máy có Docker).
- `error_code` cần map sang thông báo tiếng Việt: `SHOP_SLUG_ALREADY_TAKEN` (409),
  `INSUFFICIENT_SCOPE` (403 — không nên xảy ra nếu FE gọi đúng từ Portal, nhưng vẫn phải handle),
  `SHOP_ACCESS_DENIED` (403 — "Bạn không có quyền truy cập shop này"), `SHOP_OWNER_REQUIRED` (403 —
  "Chỉ chủ shop mới được sửa"), `VALIDATION_ERROR` (422, có `errors` theo field — slug sai format
  hoặc trùng route hệ thống dùng riêng).
- `slug`: lowercase, `[a-z0-9-]`, không bắt đầu/kết thúc bằng `-`, 3–63 ký tự — validate ở FE bằng
  Zod schema sinh từ contract trước khi gửi, đỡ round-trip 422 vô ích, nhưng LUÔN xử lý 422 từ
  server làm nguồn sự thật cuối (server còn check thêm reserved-routes mà FE không có sẵn danh sách
  — đọc qua `config/reserved-routes.json` nếu cần hiển thị gợi ý, KHÔNG hardcode danh sách thứ hai).
- `kind = "ExternalOnly"` bắt buộc `externalUrl` non-empty — validate ở form trước khi submit.

## Không thuộc phạm vi

- KHÔNG đụng `backend/`.
- KHÔNG sửa `contracts/` hay `config/`.
- Contract sai hoặc thiếu → DỪNG, báo cáo. Không tự sửa, không tự làm tạm.
- Hồ sơ shop công khai `vsite.vn/shop/{slug}` — thuộc `apps/web` + module `Marketplace`, task riêng.
- Mời nhân viên / phân quyền Manager/Staff/Accountant — chưa có API (Phase 4).
- Custom domain / `ShopDomain` — chưa có API (gắn module `Website`).
- Xoá shop — chưa có API (chỉ đổi `status = Closed` qua PATCH).
- Link verify-email vẫn trỏ thẳng endpoint BE (không phải trang FE) — lấy token qua console log lúc
  dev, KHÔNG tự chế trang FE xử lý token verify-email cho task này.

## Acceptance

- [ ] Loading / error / empty state đủ cả ba cho màn danh sách shop, tạo shop, sửa shop
- [ ] Validation dùng Zod schema sinh từ contract (slug format, `ExternalOnly ⇒ externalUrl`)
- [ ] Test Vitest + MSW pass cho luồng đăng ký → đăng nhập → tạo shop → thấy trong danh sách → sửa
- [ ] `error_code` map đúng sang thông báo tiếng Việt, đặc biệt `SHOP_ACCESS_DENIED` khác
      `SHOP_SLUG_ALREADY_TAKEN` khác `SHOP_OWNER_REQUIRED`
- [ ] Xác nhận Host header đúng `admin.vsite.local` khi dev (không vô tình gọi qua `localhost` trần)
- [ ] Integration cuối: tắt MSW, chạy luồng end-to-end thật trên máy có Docker, không cần một câu
      SQL tay nào để tạo shop (đúng mục tiêu §6 của `plan.md`)
