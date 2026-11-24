# JOBS-001 — FE brief (Bước 8a: nút "Job nền" ở Portal)

> Do session BE viết sau Gate 1 (2026-11-24). Việc FE theo [`plan.md`](plan.md) §10 (F1–F2). File này **trỏ** tới contract,
> chỉ ghi những gì S1 làm khác hoặc cụ thể hơn plan.

## Contract

Đã promote (Gate 1 gọn, #89). **SDK chưa chạy `pnpm gen:api`** — S1 không được đụng `packages/`. Chi tiết: [`contract-diff.md`](contract-diff.md).

- `contracts/openapi/platform.v1.json` (mới; `contracts/contract.lock`, `taskId: JOBS-001`).

| operationId | Route | Ai gọi |
|---|---|---|
| `createJobsDashboardSession` | `POST /api/platform/jobs/dashboard-session` — **không body** → `204` không body, kèm `Set-Cookie` | Portal (`PlatformAdmin`) |

**Bước đầu tiên của S2, trước F1:** thêm hai entry `platform` + `platformZod` vào `packages/api-sdk/orval.config.ts` (mirror `shop`/`shopZod`:
input `../../contracts/openapi/platform.v1.json`, `tags-split` → `src/generated/platform/platform.ts` + `model`, `client: 'react-query'`, `mock: { type: 'msw' }`,
cùng mutator `customInstance`; bản zod `mode: 'single'`, `src/generated/platform/platform.zod.ts`), rồi `pnpm gen:api`. Kết quả mong đợi: hook
`useCreateJobsDashboardSession` + MSW handler, không type trùng `…Dto2`. Nhớ export từ index của api-sdk nếu các module khác được export ở đó.

## Mã lỗi

| Status | `error_code` | Xử lý ở UI |
|---|---|---|
| 401 | `UNAUTHORIZED` | **Không tự xử lý** — mutator đã tự refresh token rồi gọi lại như mọi request; chỉ khi refresh hỏng thì phiên hết hạn (đăng xuất) |
| 403 | `PLATFORM_ADMIN_REQUIRED` | Hiện thông báo "không có quyền" **và ẩn nút** (token không có role / bị gỡ role) |
| khác (mạng, 5xx) | — | Thông báo lỗi, nút bấm lại được |

Token `shop:*` gửi tới host `admin.*` sẽ nhận `403 TOKEN_SHOP_MISMATCH`, nhưng Portal không bao giờ giữ token shop — coi mọi 403 ở endpoint này là "không có quyền".

## Claim `role` trong JWT — chỉ để vẽ UI

- Tên claim `role`, giá trị chuỗi `PlatformAdmin` (JWT có thể mang `role` là chuỗi hoặc mảng — xử lý cả hai). **Chỉ có trong token `vsite-portal`**
  (không bao giờ ở `vsite-main`/`shop:*`), **tính lại từ DB mỗi lần refresh** — gỡ role thì token mới hết claim (trễ ≤ 15 phút, #114 mục 4).
- FE đọc claim của access token **hiện tại** (decode base64url của payload, không verify; hỏng ⇒ coi như không có role) và phải phản ứng khi token đổi sau refresh.
- **Không dùng claim để quyết gì khác ngoài việc hiện/ẩn nút** — quyền thật do BE kiểm (403).

## Cookie dashboard — host-only, đi cùng host

Response `204` mang `Set-Cookie: vsite_jobs_dashboard` (`HttpOnly`, `SameSite=Strict`, `Path=/hangfire`, `Max-Age` 900 = 15 phút, `Secure` ngoài Development,
**không `Domain`** — host-only). FE không đọc được cookie này và không cần.

⚠️ Vì host-only, **POST phải tới đúng host `admin.*` đang phục vụ `/hangfire`** — Portal phải gọi API **cùng origin qua proxy Vite** (đường dẫn tương đối `/api/...`,
không gọi thẳng `localhost:5270`), nếu không cookie rơi vào host khác và `/hangfire` trả 401. Sau `204` mở `/hangfire` bằng `window.open('/hangfire', '_blank')`
(tab mới, cùng origin). Cookie hết hạn sau 15 phút ⇒ vào lại bằng nút.

## Proxy dev

`apps/portal/vite.config.ts` hiện có `'/api'` và `'/media'`, đều `{ target: 'http://localhost:5270', changeOrigin: false }` (giữ Host `admin.vsite.local` để BE resolve audience Portal).
Thêm **cùng dạng**:

```ts
'/hangfire': { target: 'http://localhost:5270', changeOrigin: false },
```

`changeOrigin: true` sẽ ghi đè Host thành `localhost:5270` ⇒ audience sai ⇒ dashboard 401. Dashboard còn tải tài nguyên tĩnh dưới `/hangfire/...` nên một mục proxy là đủ.

## Việc FE cần làm

Làm đúng F1 → F2 của plan §10. Điểm cụ thể hơn:
- **F1:** hàm đọc claim ở `@vsite/api-sdk` (cạnh mutator, nơi token đang được giữ) + cách để UI re-render khi token đổi; `jobs-dashboard-button.tsx` ở header
  `apps/portal/src/routes/_authenticated.tsx` (ẩn khi không có role); proxy `/hangfire`. Test trước (MSW): có/không role, token hỏng, token đổi sau refresh, `204` mở tab, `403`, lỗi mạng.
- **F2:** nghiệm thu tay với BE thật, không MSW.

## Nghiệm thu F2 (BE thật, trình duyệt thật)

Cấp role bằng SQL tay (chưa có UI cấp quyền). `app_user.role_id` tham chiếu bảng `role`; GUID PlatformAdmin cố định (`WellKnownRoles.PlatformAdminId`):

```sql
UPDATE app_user
   SET role_id = '00000000-0000-0000-0000-000000000002'   -- PlatformAdmin
 WHERE email_normalized = '<EMAIL VIẾT HOA>';
-- hoàn lại: role_id = '00000000-0000-0000-0000-000000000001' (PlatformUser)
```

Sau khi sửa phải **đăng nhập lại** (hoặc đợi refresh) để token mới có claim.

| # | Kiểm | Kỳ vọng |
|---|---|---|
| 1 | User đã cấp role đăng nhập Portal | Thấy nút "Job nền" → bấm → tab mới `/hangfire` vào được dashboard |
| 2 | User chủ shop thường | Không thấy nút; gọi `POST /api/platform/jobs/dashboard-session` bằng devtools ⇒ `403 PLATFORM_ADMIN_REQUIRED` |
| 3 | Mở `/hangfire` khi chưa bấm nút (không cookie) | Bị chặn (401); mở từ host shop (`{slug}.vsite.local`) cũng bị chặn dù chép cookie |
| 4 | "Trigger now" 3 recurring job (`identity.pending-registration-cleanup`, `identity.expired-token-cleanup`, `identity.zalo-integrity`) | Chạy thành công, log BE ghi đúng số dòng đã xoá (0 nếu không có dòng hết hạn); cần `Jobs:ServerEnabled=true` (mặc định) và dev server BE đã khởi động lại bản mới |
| 5 | Gỡ role bằng SQL | Sau ≤ 15 phút + một lần refresh, nút biến mất; cookie đã cấp sống tối đa 15 phút |

Lưu ý khi chạy dev: BE dev server phải dùng bản code mới (Vsite.Api đang chạy từ trước sẽ không có `platform.v1`); lần khởi động đầu Hangfire tự tạo schema `hangfire` trong DB dev — bình thường, đừng dọn bằng migration.
Nếu Postgres chưa chạy khi boot, BE chờ ~14 giây rồi mới lên (xem changelog, nợ "Trước ra mắt").

## Sau S2

`change-reviewer` toàn nhánh (model mạnh nhất — chạm bảo mật/tenant) → `review.md` kèm "Ma sát quy trình" → Gate 2. Nhắc người sửa `DesignIdeal/step.md` (8a xong, 8d dùng khung #116).
