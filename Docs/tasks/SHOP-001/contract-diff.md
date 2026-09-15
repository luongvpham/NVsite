# SHOP-001 — contract diff

## ⚠️ BREAKING / REMOVED
- **REMOVED** `GET /auth/me/shops` (identity.v1.json) — **có chủ đích, đã chốt trước khi thực thi**
  (`plan.md` §7 Quyết định 1, xác nhận rõ ràng — không phải bug implementation, quy tắc contract #3).
  Lý do: endpoint này thêm ở Phase 3 (IDENTITY-001) chỉ để có mục tiêu test `RequireGlobalScope`,
  **chưa FE nào tiêu thụ**, và sau khi `Shop` tách module thì `Identity` không còn trả được
  `Name`/`Slug` (`Identity.dependsOn = []`, thêm `Shop` vào đó tạo vòng lặp với
  `Shop.dependsOn = ["Identity"]`). `GET /shops` thay thế hoàn toàn — vừa giữ vai trò test
  `RequireGlobalScope`, vừa đủ dữ liệu cho shop switcher. Đã xoá `ListMyShopsQuery`/`Handler` +
  2 test tương ứng trong `TokenScopeTests.cs`.

## NEW_ENDPOINT
- GET /shops
- POST /shops
- GET /shops/{shopId}
- PATCH /shops/{shopId}

## ADDITIVE
(không có)

## UNCHANGED
8 operation không đổi.

## Giả định tôi đã tự đặt (không hỏi)

1. **`ShopKind`/`ShopStatus` chuyển từ `Vsite.Domain.Identity.Enums` sang `Vsite.Domain.Shop.Enums`**
   — bảng chuyển giao ở `plan.md` §4.1 chỉ liệt kê Entity/Interface/ServiceImpl/Configuration,
   không nhắc hai enum này, nhưng để `Shop` entity không còn phụ thuộc ngược vào namespace Identity
   cho chính field của nó thì phải chuyển theo. Đã cập nhật mọi chỗ dùng (kể cả test cũ ở
   `IntegrationTests/Identity/*`).
2. **`ITenantContext.ShopId` nới rộng hợp đồng** — trước đây "chỉ có giá trị khi `AudienceKind` =
   Shop", giờ cũng có giá trị khi Portal gọi endpoint có `{shopId}` trong route (set bởi
   `ShopMembershipEndpointFilter` sau khi xác nhận membership), `AudienceKind` vẫn giữ `Portal`.
   Đây chính là quyết định kiến trúc mà `plan.md` §3 ghi "cần chốt khi thực thi" — đã cập nhật XML
   doc của interface, không có cách nào khác để Global Query Filter hoạt động đúng cho Portal.
3. **Ràng buộc "chỉ Owner được PATCH" cài ở tầng handler** (`UpdateShopHandler` tự query
   `UserShop.RoleId`, throw `ForbiddenAccessException("SHOP_OWNER_REQUIRED", ...)`), không phải một
   ASP.NET `IAuthorizationHandler`/policy riêng — `ShopMembershipEndpointFilter` dùng chung (§3) chỉ
   xác nhận membership + set `TenantContext.ShopId`, không biết business rule "chỉ Owner" riêng của
   từng endpoint tương lai.
4. **`PATCH /shops/{shopId}` thay toàn bộ field editable** (Name/Slug/Kind/ExternalUrl/Status),
   không phải partial patch từng field — `plan.md` §4.4 chỉ liệt kê field được sửa, không nói rõ
   ngữ nghĩa partial/full. Chọn full-replace vì đơn giản hơn cho v1, ít field.
5. **`ShopSummaryDto` (`GET /shops`) có thêm `RoleCode`** ngoài `Name`/`Slug`/`Kind`/`Status` mà
   `plan.md` §4.4 yêu cầu — giữ song song với `MyShopDto` cũ (`ShopId`, `RoleCode`) để shop switcher
   hiển thị được vai trò, không mất thông tin khi thay thế endpoint.
6. **Regenerate một migration `InitialSchema` duy nhất** (xoá + tạo lại), không thêm migration
   incremental cho việc chuyển `Shop` sang module riêng — theo đúng tinh thần "chưa deploy
   production thì cứ sửa" (chưa migration nào chạy trên môi trường thật), tránh migration phát sinh
   DropTable/CreateTable "Shop" giả do đổi namespace CLR.
7. **`ShopMembershipEndpointFilter` tái dùng `IUserShopMembershipService.IsActiveMemberAsync`** —
   cùng service `ShopMembershipValidationMiddleware` (Identity Phase 3) đã dùng, không viết lại query
   membership lần thứ hai.
8. Slug: `MinimumLength(3)`/`MaximumLength(63)` — `plan.md` §4.3 chỉ nói "độ dài hợp lý", chọn theo
   giới hạn subdomain DNS thông thường (63 ký tự/label).

## Câu hỏi cần anh quyết
(không có — các điểm mở duy nhất đã hỏi trực tiếp trong session, ghi ở trên dưới dạng giả định đã
chốt)
