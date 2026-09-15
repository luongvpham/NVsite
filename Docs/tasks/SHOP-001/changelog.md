# SHOP-001 — changelog: lệch so với thiết kế

Ghi lại mọi chỗ thực thi khác với `DesignIdeal/04-listing-and-review-design.md` §2.1/§2.2 (và với
`plan.md`) cùng nguyên nhân — chia rõ **lệch có chủ đích** (giữ nguyên) và **việc chưa làm xong**
(theo dõi ở task sau, chủ yếu module `Marketplace`).

**Đọc kèm:**
- `backend/docs/modules/shop.md` — quy ước vận hành module (invariant, resolve `ShopId` cho Portal).
- `backend/docs/modules/identity.md` mục "Bảng Shop đã chuyển giao" — phía Identity của cùng việc
  chuyển giao.
- `Docs/tasks/SHOP-001/contract-diff.md` — Gate 1, khác biệt tầng API contract + đầy đủ giả định đã
  tự đặt (không lặp lại ở đây).

---

## Lệch có chủ đích (giữ nguyên, không phải bug)

### 1. `GET /auth/me/shops` bị xoá, thay bằng `GET /shops`

- **Thiết kế/hiện trạng trước SHOP-001:** `IDENTITY-001` thêm `GET /auth/me/shops` ở Phase 3 chỉ để
  có mục tiêu test `RequireGlobalScope` (Quyết định #32).
- **Thực thi:** xoá hẳn endpoint + `ListMyShopsQuery`/`Handler` + 2 test tương ứng trong
  `TokenScopeTests.cs`. `GET /shops` (module Shop) thay thế hoàn toàn, vừa giữ vai trò test
  `RequireGlobalScope`, vừa trả đủ `Name`/`Slug`/`Kind`/`Status`/`RoleCode` cho shop switcher.
- **Nguyên nhân:** sau khi `Shop` tách module, `Identity` (`dependsOn = []`) không còn cách nào trả
  `Name`/`Slug` của shop mà không tạo phụ thuộc ngược vào `Shop`. **Đã hỏi và được xác nhận rõ ràng**
  trước khi thực thi (`plan.md` §7 Quyết định 1) — không phải quyết định tự ý giữa chừng.
- **BREAKING có chủ đích** (quy tắc contract #3, chưa deploy production) — xem `contract-diff.md`.

### 2. `ShopKind`/`ShopStatus` chuyển namespace sang `Vsite.Domain.Shop.Enums`

- **`plan.md` §4.1 nói gì:** bảng chuyển giao chỉ liệt kê Entity `Shop.cs`, `IShopLookupService`,
  `ShopLookupService`, `ShopConfiguration` — không nhắc hai enum.
- **Thực thi:** chuyển cả `ShopKind`/`ShopStatus` từ `Vsite.Domain.Identity.Enums` sang
  `Vsite.Domain.Shop.Enums`.
- **Nguyên nhân:** để lại hai enum này ở `Identity` nghĩa là entity `Shop` (module Shop) phải
  reference ngược `Vsite.Domain.Identity.Enums` cho chính field của nó — hợp lệ về mặt
  `ModuleBoundaryTests` (`Shop.dependsOn = ["Identity"]`) nhưng sai chỗ về mặt domain: `ShopKind`/
  `ShopStatus` là khái niệm của `Shop`, không phải của `Identity`.

### 3. `ITenantContext.ShopId` có giá trị cả khi `AudienceKind` vẫn là `Portal`

- **Thiết kế trước SHOP-001:** XML doc cũ ghi "`ShopId` chỉ có giá trị khi `AudienceKind` = Shop".
- **Thực thi:** `ShopMembershipEndpointFilter` set `TenantContext.ShopId` cho mọi request Portal gọi
  endpoint có `{shopId}` trong route, sau khi xác nhận membership — `AudienceKind` KHÔNG đổi thành
  `Shop`, vẫn giữ `Portal`.
- **Nguyên nhân:** đây chính là câu hỏi kiến trúc `plan.md` §3 nêu ra ("cần chốt khi thực thi") —
  không có cách nào khác để Global Query Filter (fail-closed theo `ShopId`) hoạt động đúng khi Portal
  đọc dữ liệu shop-scoped qua route. Đã cập nhật lại XML doc của `ITenantContext.ShopId` cho khớp.

### 4. Quyền "chỉ Owner" cho `PATCH /shops/{shopId}` kiểm ở handler, không phải policy riêng

- **Thực thi:** `UpdateShopHandler` tự query `UserShop.RoleId` (đã có `ShopId` đúng nhờ mục 3) và
  throw `ForbiddenAccessException("SHOP_OWNER_REQUIRED", ...)` nếu không phải Owner.
  `ShopMembershipEndpointFilter` (dùng chung mọi module Portal) chỉ xác nhận membership generic +
  set `TenantContext.ShopId`, không biết business rule "chỉ Owner" của riêng endpoint này.
- **Nguyên nhân:** giữ filter dùng chung thuần generic (đúng tinh thần "mọi module Portal sau này
  dùng lại, không ai phải viết lại check membership") — business rule đặc thù của từng endpoint (vd.
  role nào được sửa) thuộc về handler của chính module đó.

### 5. `PATCH /shops/{shopId}` thay toàn bộ field editable, không phải partial patch

- **`plan.md` §4.4 nói gì:** chỉ liệt kê field được sửa (`Name`/`Slug`/`Kind`/`ExternalUrl`/
  `Status`), không nói rõ ngữ nghĩa partial/full.
- **Thực thi:** `UpdateShopCommand` nhận đủ 5 field, client phải gửi toàn bộ giá trị mong muốn (kể
  cả field không đổi) — không hỗ trợ JSON Merge Patch/JSON Patch từng field riêng lẻ.
- **Nguyên nhân:** đơn giản hơn cho v1 (chỉ 5 field), tránh độ phức tạp của partial-patch semantics
  khi chưa có yêu cầu nghiệp vụ cụ thể cần nó. **Điều kiện đảo lại:** nếu FE cần partial update (vd.
  chỉ đổi `Status` mà không gửi lại `Name`/`Slug`), đổi `UpdateShopCommand` sang field nullable +
  chỉ ghi đè field có giá trị.

### 6. `ShopSummaryDto` (`GET /shops`) có thêm `RoleCode`

- **`plan.md` §4.4 nói gì:** "kèm `Name`/`Slug`/`Kind`/`Status` — đủ cho shop switcher".
- **Thực thi:** thêm `RoleCode` (vai trò của user tại shop đó).
- **Nguyên nhân:** giữ song song với `MyShopDto` cũ (`ShopId`, `RoleCode`) mà endpoint này thay thế
  — không muốn shop switcher mất khả năng hiển thị vai trò (vd. badge "Owner") mà endpoint cũ từng
  cho được.

### 7. Regenerate migration `InitialSchema` duy nhất (xoá + tạo lại), không thêm migration mới

- **Thực thi:** `dotnet ef migrations remove` rồi `dotnet ef migrations add InitialSchema` lại từ
  đầu, thay vì thêm một migration incremental riêng cho việc chuyển `Shop` sang module khác.
- **Nguyên nhân:** namespace CLR của entity `Shop` đổi (`Vsite.Domain.Identity.Entities.Shop` →
  `Vsite.Domain.Shop.Entities.Shop`) khiến EF Core coi đây là "xoá entity cũ, thêm entity mới" nếu
  tạo migration incremental — sinh ra `DropTable`/`CreateTable "Shop"` giả dù bảng vật lý không đổi
  cấu trúc gì ngoài CHECK constraint mới. Vì **chưa có migration nào chạy trên môi trường thật**
  (đúng tinh thần "chưa deploy production thì cứ sửa", quy tắc contract #3 áp dụng tương tự cho
  schema), regenerate một migration sạch rẻ hơn và đúng hơn là để lại một migration ghi nhận thao
  tác drop/create giả.

### 8. Thêm `ShopMembershipRequiredMarker` + `RequireShopMembership()` — enforce bằng test, không hy vọng nhớ

- **Bối cảnh:** sau Gate 1, thảo luận với người duyệt về việc có nên bỏ `ShopMembershipEndpointFilter`
  (đổi hướng để chủ shop luôn thao tác qua domain riêng của shop). Đã đối chiếu lại Quyết định #31
  (`Portal là platform domain phục vụ NHIỀU shop qua shop switcher`) và invariant 9 của #31 (`admin
  xem dữ liệu shop → endpoint riêng + audit log riêng, KHÔNG tắt filter có điều kiện trong endpoint
  dùng chung`) — kết luận giữ nguyên kiến trúc §3, nhưng lỗ hổng thật sự đáng lo là **"ai đó thêm
  endpoint `{shopId}` mới mà quên gắn filter"**, không phải bản thân cơ chế route-based `ShopId`.
- **Thực thi:** `.AddEndpointFilter<ShopMembershipEndpointFilter>()` không còn gọi trực tiếp — thay
  bằng `ShopScopedEndpointExtensions.RequireShopMembership()` (gắn CẢ filter LẪN một metadata marker
  rỗng `ShopMembershipRequiredMarker`). Test mới `ShopScopedRouteFilterTests` (IntegrationTests, viết
  ở `Tenancy/`) duyệt `EndpointDataSource` **thật** sau khi host build (không phải regex đọc source),
  tìm mọi route có tham số `shopId`, khẳng định marker có mặt.
- **Nguyên nhân:** đúng nguyên tắc nền #17 của dự án ("codegen > skill > CLAUDE.md > hy vọng agent
  nhớ") — quên gọi đúng method giờ là RED ngay lúc build/test, không cần review bằng mắt hay ghi chú
  tài liệu. Đã verify test thật sự bắt được lỗi (tạm xoá `.RequireShopMembership()` ở 2 endpoint →
  test FAIL đúng cả 2 route vi phạm, sau đó khôi phục lại → PASS).
- **Không cần Docker:** test dùng `WebApplicationFactory<Program>` trần (connection string giả),
  không phải `ShopApiFactory`/Testcontainers — chỉ đọc `EndpointDataSource` sau khi host build xong,
  không gửi request nào chạm DB/Redis. Chạy được ở mọi máy, kể cả không có Docker.

---

## Việc chưa làm xong so với đặc tả (nợ kỹ thuật, không phải lệch có chủ đích)

### A. 04 §2.2 — ràng buộc đổi `Hosted → ExternalOnly` phải chuyển/gỡ `Listing`

`04` §2.2 yêu cầu: đổi `Kind` từ `Hosted` sang `ExternalOnly` phải chuyển mọi `Listing` đang trỏ
`ShopHome`/`ShopPage` sang `ExternalUrl` hoặc `Unpublished` — nếu không, khách từ vsite gặp 404.
`Listing` thuộc module `Marketplace`, **chưa tồn tại** ở SHOP-001 nên ràng buộc này để **RỖNG có chủ
đích** trong `UpdateShopHandler`. Đánh dấu bằng
`[Fact(Skip = "Listing chưa tồn tại — mở lại ở module Marketplace")]` ở
`ShopEndpointTests.UpdateShop_Hosted_to_ExternalOnly_must_reassign_or_unpublish_listings_targeting_website`.
**Khi làm module `Marketplace`: mở lại test này trước, đừng quên** — đây đúng loại ràng buộc dễ bị
lãng quên vì compile/test vẫn xanh khi thiếu nó.

### B. Danh mục ngoài phạm vi §2 của `plan.md` (đã liệt kê từ lúc lập plan, chưa đổi)

- Mời nhân viên / phân quyền Manager/Staff/Accountant — Phase 4 (Quyết định #39.1).
- Xoá shop cứng (hard delete) — `Status = Closed` đủ cho nhu cầu hiện tại.
- Hồ sơ shop công khai `vsite.vn/shop/{slug}` (`04` §5) — thuộc `apps/web` + module `Marketplace`.
- `ShopDomain` / custom domain / Caddy On-Demand TLS (Quyết định #7, #9) — gắn với module `Website`.

---

## Đúng đặc tả, đã xác nhận bằng code (ghi lại để không phải kiểm lại)

- **Entity `Shop` đúng 5 field nghiệp vụ của `04` §2.1** (`Id`/`Name`/`Slug`/`Kind`/`ExternalUrl`/
  `Status` + audit trail từ `BaseAuditableEntity`) — không thêm field hồ sơ công khai.
- **CHECK constraint `Kind = ExternalOnly ⇒ ExternalUrl NOT NULL`** tồn tại thật ở migration
  (`ck_shop_external_url`) — test `DbConstraintTests.Check_constraint_rejects_ExternalOnly_shop_without_ExternalUrl`
  xác nhận bằng Docker thật.
- **`UserShop(RoleId=Owner, Source=ShopCreator)` là nhánh DUY NHẤT tạo trong `CreateShopHandler`** —
  đúng 03 §3.3, đóng nợ kỹ thuật ghi ở `IDENTITY-001/changelog.md` mục A ("`ShopCreator` chưa bao
  giờ được ghi").
- **`RequireGlobalScope` áp cho cả 4 endpoint** (Quyết định #32) — test `shop:*` bị từ chối
  (`INSUFFICIENT_SCOPE`) ở cả `POST`/`GET`/`GET {id}`/`PATCH`, chạy Docker thật, pass.
- **Tenant isolation ở `GET /shops/{shopId}`** — user chỉ là Owner của shop A gọi `/shops/{B}` nhận
  403 `SHOP_ACCESS_DENIED` (không phải 404 rỗng do Global Query Filter) — test chạy Docker thật, pass.

---

## Nợ Docker

Không có — toàn bộ test integration của task này (`ShopEndpointTests`, test mới trong
`DbConstraintTests`) đã chạy pass thật với Docker trong chính session này (28 passed, 1 skip có chủ
đích, 0 fail). `Docs/DOCKER-TEST-DEBT.md` vẫn rỗng, không có mục nào cần thêm.

---

## FE — giả định tôi đã tự đặt (session FE, sau Gate 1)

### 1. IDENTITY-001 FE (đăng ký/xác minh/đăng nhập) build ở đây, không phải task riêng

Phát hiện lúc bắt đầu: `IDENTITY-001` chỉ có phần BE được merge — `packages/api-sdk/orval.config.ts`
rỗng, không có code auth nào ở `apps/web` hay `apps/portal`. `brief.md` của SHOP-001 (viết sau khi
biết điều này) đã tự liệt kê đăng ký/xác minh/đăng nhập/route-guard vào mục "Việc FE cần làm" —
không phải quyết định tự ý giữa chừng, chỉ ghi lại để task sau không thắc mắc vì sao SHOP-001 FE lại
có code Identity.

### 2. KHÔNG dựng trang FE xử lý token verify-email

`brief.md` mục "Không thuộc phạm vi" nói rõ: "KHÔNG tự chế trang FE xử lý token verify-email cho
task này" — link verify-email vẫn trỏ thẳng BE. `apps/portal` **không có** route `/verify-email`;
màn đăng ký chỉ hiện thông báo chung, dev tự lấy token từ console log của `LoggingEmailSender` và
gọi thẳng BE (không qua Portal).

### 3. `packages/api-sdk/orval.config.ts` — thêm module `identity`/`shop` theo đúng template cũ

File chỉ có `defineConfig({})` rỗng (comment trỏ về git history của `Sample` đã xoá,
`Docs/tasks/CLEANUP-SAMPLE.md`). Thêm 4 entry (`identity`, `identityZod`, `shop`, `shopZod`) đúng
y hệt shape/override của `Sample` cũ (mode `tags-split` cho react-query, `single` cho zod, mutator
`customInstance`, mock MSW). Đây là lần đầu module thật được wire vào — không phải "sửa contract",
chỉ là hoàn thiện cấu hình codegen đã có sẵn khung.

### 4. `packages/api-sdk/src/index.ts` — không `export *` blanket cho `shop/model`

`ProblemDetails`/`HttpValidationProblemDetails`/`HttpValidationProblemDetailsErrors` được Orval sinh
**giống hệt nhau** ở cả `identity/model` và `shop/model` (cùng tham chiếu shared schema của BE).
`export *` từ cả hai barrel cùng lúc gây `TS2308` (ambiguous re-export). Xử lý: export barrel đầy đủ
từ `identity/model`, còn `shop/model` chỉ export named từng file model riêng (`shopDto`, `shopKind`,
`shopStatus`, `shopSummaryDto`, `createShopRequest`, `updateShopRequest`), bỏ qua 3 file trùng.

### 5. `packages/api-sdk/src/mutator/axios-instance.ts` — hiện thực refresh-token-on-401

File gốc chỉ có `setAccessToken()` + comment "Chỗ cắm cho refresh-token rotation... Interceptor
response sẽ thêm vào đây khi module Identity xong" — đúng điểm cắm đã được đánh dấu sẵn, không phải
sửa ngoài kế hoạch. Thêm `setRefreshToken()`, `setOnSessionExpired()` (callback để app tự clear
session khi refresh thất bại thật — Quyết định #3: "401 từ /auth/refresh-token = phiên hết hạn
thật, không retry"), và response interceptor tự gọi `POST /auth/refresh-token`, phát lại request cũ
đúng một lần (`_retriedAfterRefresh` flag chặn vòng lặp vô hạn), dùng chung một `refreshPromise` để
nhiều request 401 cùng lúc chỉ refresh một lần.

### 6. Zustand session store chỉ giữ `isAuthenticated`, KHÔNG giữ token thật

`apps/portal/src/stores/session-store.ts` chỉ có state `isAuthenticated: boolean`. Giá trị token
thật (access/refresh) nằm DUY NHẤT trong memory của `axios-instance.ts` (set qua
`setAccessToken()`/`setRefreshToken()`) — Zustand chỉ là cờ UI phản ứng (route guard, layout), không
copy dữ liệu server/session state sang state khác (đúng tinh thần Quyết định #20, dù #20 nói về
Query vs Zustand chứ không nói riêng về token — áp dụng cùng nguyên tắc "không giữ hai bản sự thật").

### 7. `VITE_API_BASE_URL=/` khai qua `.env.development`/`.env.test`/`.env.production`, không phải `.env`

Root `.gitignore` chặn đúng tên file `.env` (không chặn biến thể theo mode) — dùng `.env` thì giá trị
này không bao giờ vào git, một dev khác clone về sẽ thấy Portal gọi thẳng
`http://localhost:5270` (default cứng trong `axios-instance.ts`) thay vì qua Vite proxy, hỏng cơ chế
Host header. Khai riêng theo 3 mode (Vitest chạy mode `test`, không đọc `.env.development`) — nội
dung giống hệt nhau ở cả ba vì same-origin là yêu cầu kiến trúc chung cho mọi môi trường (Caddy giữ
nguyên Host ở production — Quyết định #9), không phải giá trị riêng cho dev.

### 8. Trang Quên/Đặt lại mật khẩu — KHÔNG build

`IDENTITY-001/brief.md` liệt kê 2 trang này, nhưng `SHOP-001/brief.md` mục "Việc FE cần làm" (viết
sau, biết rõ hiện trạng) **không** nhắc tới — chỉ liệt kê đăng ký/xác minh/đăng nhập/route-guard/3
màn shop. Không build để tránh tự mở rộng phạm vi ngoài brief; API `/auth/forgot-password` +
`/auth/reset-password` đã có sẵn trong `identity.v1.json` và SDK đã generate, task sau chỉ cần thêm
2 route.

## Cần cập nhật ở tài liệu gốc

`DesignIdeal/04-listing-and-review-design.md` — banner `STATUS` đổi từ `SPEC` sang `IMPLEMENTED`,
trỏ về file này. **Chỉ §2.1/§2.2 (Shop) đã code** — §3 trở đi (`ServiceCategory`/`Listing`/`Review`/
`Lead`) vẫn là spec, chưa có dòng code nào cho phần đó. Ghi rõ ở `Stale` của banner để agent sau
không tưởng cả file đã implemented.
