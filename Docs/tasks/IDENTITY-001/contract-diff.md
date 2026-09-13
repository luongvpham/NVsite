# IDENTITY-001 — contract diff

## ⚠️ BREAKING / REMOVED
- **REMOVED** `POST /portal/auth/login` (identity.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `POST /shops/{shopId}/auth/forgot-password` (identity.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `POST /shops/{shopId}/auth/login` (identity.v1.json) — endpoint có trong contract nhưng runtime không còn
- **REMOVED** `POST /shops/{shopId}/auth/register` (identity.v1.json) — endpoint có trong contract nhưng runtime không còn

## NEW_ENDPOINT
(không có)

## ADDITIVE
(không có)

## UNCHANGED
6 operation không đổi.

## Giả định tôi đã tự đặt (không hỏi)

**Đây là sửa lại thiết kế route của CHÍNH IDENTITY-001** (vừa Gate 1 lần đầu, chưa có FE nào code
theo) sau khi thảo luận trực tiếp — không phải một task/contract mới. 4 endpoint REMOVED ở trên
KHÔNG phải xoá tính năng — chức năng của chúng (đăng ký/đăng nhập/quên mật khẩu theo shop, theo
portal) vẫn còn nguyên, chỉ gộp về đúng 6 route ở mục UNCHANGED (`/auth/*`) thay vì tách route theo
context.

**Lý do gộp:** route cũ (`/shops/{shopId}/auth/*`) đòi hỏi FE phải tự biết `shopId` (GUID) trước
khi gọi — nhưng FE khi render website một shop chỉ biết slug/domain đang phục vụ trang
(`spa-abc.vsite.vn`, `vsite.vn/spa-abc`, `spa-abc.com`), không biết GUID nội bộ. Quyết định (thảo
luận trực tiếp, không phải tôi tự suy luận): **BE tự resolve `ShopId`/audience từ `Host` header**
qua `Api.Tenancy.TenantResolutionMiddleware` (Quyết định #7, thu hẹp — chỉ Path/Subdomain qua
`Shop.Slug`, custom domain vẫn deferred) — chạy TRƯỚC mọi handler, set `ITenantContext`. FE giờ chỉ
gọi đúng MỘT path tương đối (`/auth/login`...) từ domain đang phục vụ trang; không endpoint nào
nhận `ShopId` từ route/body nữa (đúng tinh thần Quyết định #21.4 chặt hơn so với route cũ).

**Testing local:** không có Caddy/domain thật ở dev — xác nhận dùng Windows hosts file
(`C:\Windows\System32\drivers\etc\hosts`, map `spa-abc.vsite.local` → `127.0.0.1`) để Host header
đúng thật khi gọi thẳng `http://spa-abc.vsite.local:5270/auth/login` (Kestrel không quan tâm
hostname, chỉ quan tâm port).

## Câu hỏi cần anh quyết
(không có — đã thảo luận và chốt hướng đi trước khi sửa contract này)
