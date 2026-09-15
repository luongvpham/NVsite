# SHOP-001 — Module `Shop`: tạo/sửa shop + quản lý shop của mình (BE + FE)

> **Lane C** (module mới, chạm nhiều module, có FE) → Gate 1 + Gate 2, hai session tuần tự theo
> `DesignIdeal/ai-agent-development-workflow.md` §5. **Không làm FE song song với BE.**
>
> Trạng thái: **Gate 1 đã duyệt (2026-09-14), contract đã promote.** Xem `contract-diff.md` +
> `changelog.md` + `brief.md`. Session FE (§5) CHƯA bắt đầu — mở ở session riêng, theo đúng "Không
> làm FE song song với BE".

---

## 1. Vì sao task này, vì sao bây giờ

`step.md` bước 4 là `MediaAsset`, nhưng `MediaAsset.dependsOn = ["Shop"]` và mọi asset cần một
`ShopId` thật. Hiện **không có cách nào tạo `Shop`**: bảng `Shop` mới là bản trích tối thiểu
(`03` §3.4), không endpoint nào tạo nó, và nhánh `UserShopSource.ShopCreator → role Owner` — nhánh
**duy nhất** sinh ra `Owner` (`03` §3.3) — chưa hiện thực. Bằng chứng đang nằm trong
`Docs/DOCKER-TEST-DEBT.md` mục 2: *"chưa có endpoint tạo Shop nên phải insert tay"*.

Task này đóng cùng lúc 4 thứ:

| # | Đóng được gì |
|---|---|
| 1 | Nhánh `ShopCreator → Owner` — role shop duy nhất thực dùng ở Phase 1 (`03` "Cố tình để mở") |
| 2 | Nợ bàn giao bảng `Shop` từ `Identity` sang module `Shop` (`backend/docs/modules/identity.md`) |
| 3 | Bước "insert Shop bằng tay" trong `DOCKER-TEST-DEBT.md` mục 2 |
| 4 | **Cơ chế resolve `ShopId` cho Portal** — xem §3, đây là phần khó nhất và chặn mọi màn hình Portal về sau |

**KHÔNG làm ở task này:** API quản lý user/role. `03` chốt permission matrix là **Phase 4**; Phase 1
mỗi shop chỉ dùng `Owner` + `Customer`, và `Role` là seed cố định 7 giá trị nên không có CRUD.

---

## 2. Phạm vi (theo yêu cầu: "register → tạo/sửa shop → quản lý shop của mình")

**Luồng phải chạy được end-to-end sau khi xong:**

```
đăng ký tại admin.vsite.local  →  verify email  →  đăng nhập (audience vsite-portal)
   →  tạo shop (tự động thành Owner)  →  thấy shop trong danh sách của mình
   →  mở shop  →  sửa tên/slug/kind  →  thấy thay đổi có hiệu lực
```

Trong phạm vi: entity `Shop` đầy đủ (`04` §2.1), `UserShop` với `Source = ShopCreator`, 4 endpoint,
cơ chế tenant scope cho Portal, màn hình Portal tương ứng.

**Ngoài phạm vi (task riêng):**

- `ShopDomain` / custom domain / Caddy On-Demand TLS (Quyết định #7, #9) — gắn với module `Website`.
- Mời nhân viên, phân quyền Manager/Staff/Accountant (Phase 4, Quyết định #39.1).
- Xoá shop (`Status = Closed` là đủ cho giờ; xoá cứng chưa có nhu cầu nghiệp vụ).
- Hồ sơ shop công khai `vsite.vn/shop/{slug}` (`04` §5) — thuộc `apps/web` + module `Marketplace`.
- Ràng buộc `Hosted → ExternalOnly` phải chuyển/gỡ `Listing` trỏ nội bộ (`04` §2.2) — `Listing`
  chưa tồn tại nên check này rỗng nghĩa. **Phải để lại một test đang `Skip` + ghi chú**, không được
  im lặng bỏ qua (đây đúng loại ràng buộc sẽ bị quên khi module `Marketplace` ra đời).

---

## 3. ⚠️ Vấn đề kiến trúc phải giải trước khi viết endpoint

**Quyết định #31 nói Portal lấy `ShopId` từ route param `/shops/{shopId}/...`** — trường hợp DUY
NHẤT `ShopId` không đến từ Host. Nhưng hiện tại:

- `TenantResolutionMiddleware` thấy host label `admin` → set `AudienceKind = Portal`, `ShopId = null`.
- Global Query Filter **fail-closed**: `ShopId == tenantContext.ShopId`, mà `ShopId` của entity là
  `Guid` non-null → `null` không khớp hàng nào → **mọi query shop-scoped trả rỗng**.

Nghĩa là với thiết kế hiện tại, Portal không đọc được dữ liệu shop nào. Cần một bước set
`TenantContext.ShopId` từ route param, **sau khi** đã xác thực membership.

**Hướng đề xuất** (cần chốt khi thực thi, xem §7 câu hỏi 3):

Một **endpoint filter** (không phải middleware — middleware chạy trước routing nên chưa có route
values) gắn vào các endpoint có `{shopId}`:

1. Đọc `shopId` từ route.
2. Query `UserShop(userId, shopId)` còn `Active` — **tại request này**, không tin claim
   `ownerShopIds` trong JWT (Quyết định #31 invariant 7).
3. Không có record → **403**, không fallback role mặc định (invariant 8).
4. Có → set `TenantContext.ShopId = shopId` để Global Query Filter hoạt động đúng.

Đặt ở `Vsite.Api/Tenancy/` (namespace dùng chung, mọi module Portal sau này dùng lại). Phải có test
khẳng định user của shop A gọi `/shops/{B}/...` bị 403 — đây là test tenant isolation quan trọng
nhất của task.

---

## 4. Session BE

### 4.1 Chuyển giao bảng `Shop` (làm trước, không lẫn với việc khác)

Theo đúng bảng đã ghi ở `backend/docs/modules/identity.md`:

| Từ | Sang |
|---|---|
| `Vsite.Domain/Identity/Entities/Shop.cs` | `Vsite.Domain/Shop/Entities/Shop.cs` + mở rộng đủ `04` §2.1 |
| `Vsite.Application/Identity/Interfaces/IShopLookupService.cs` | `Vsite.Application/Shop/Interfaces/` |
| `Vsite.Infrastructure/Identity/ShopLookupService.cs` | `Vsite.Infrastructure/Shop/` |
| `Persistence/Configurations/Identity/ShopConfiguration.cs` | `Configurations/Shop/` |

⚠️ **`UserShop.Shop` navigation phải bị XOÁ**, chỉ giữ `ShopId` (Guid). Lý do: `UserShop` thuộc
`Identity`, mà `dependency-map.json` khai `Identity.dependsOn = []` — giữ navigation nghĩa là
Identity phụ thuộc Shop, `ModuleBoundaryTests` sẽ fail (đúng). FK `UserShop.ShopId → Shop` khai lại
từ **phía Shop** (`ShopConfiguration`: `HasMany<UserShop>()...`) — `Shop.dependsOn = ["Identity"]`
nên chiều này hợp lệ.

`AppDbContext.Shops` giữ nguyên (một DbContext cho toàn hệ) — chỉ đổi namespace của entity.

### 4.2 Entity `Shop` đầy đủ (`04` §2.1)

```
Id · Name · Slug (UNIQUE) · Kind {Hosted, ExternalOnly} · ExternalUrl? · Status {Draft, Active, Suspended, Closed}
```
Kế thừa `BaseAuditableEntity` (**không** `ShopEntity` — bản thân `Shop` LÀ tenant, không thuộc về
tenant khác; ghi chú này đã có sẵn trong file entity hiện tại, giữ nguyên).

Ràng buộc DB: CHECK `Kind = 'ExternalOnly' ⇒ ExternalUrl IS NOT NULL` (`04` §2.1).

### 4.3 Validation slug (Quyết định #8 + #24)

- Không nằm trong `reservedPaths` **hoặc** `reservedSubdomains` của `config/reserved-routes.json` —
  đọc qua `IReservedRoutesProvider` đã có, **không viết danh sách thứ hai**.
- Format: lowercase, `[a-z0-9-]`, không bắt đầu/kết thúc bằng `-`, độ dài hợp lý.
- Unique (index DB + kiểm ở handler để trả `ConflictException` có `error_code` tử tế).

### 4.4 Endpoint

| Method | Path | Policy | Ghi chú |
|---|---|---|---|
| `POST` | `/shops` | `RequireGlobalScope` | Tạo shop + `UserShop(Owner, ShopCreator)` trong MỘT `SaveChangesAsync` |
| `GET` | `/shops` | `RequireGlobalScope` | Shop mà user hiện tại là thành viên (kèm `Name`/`Slug`/`Kind`/`Status` — đủ cho shop switcher) |
| `GET` | `/shops/{shopId}` | `RequireGlobalScope` + filter §3 | |
| `PATCH` | `/shops/{shopId}` | `RequireGlobalScope` + filter §3 + role `Owner` | Sửa `Name`/`Slug`/`Kind`/`ExternalUrl`/`Status` |

`RequireGlobalScope` cho cả 4: token `shop:{shopId}` **không** được tạo/sửa shop (Quyết định #32 —
đó là thao tác quản trị, thuộc Portal/Main).

`GET /shops` đồng thời **thay thế `GET /auth/me/shops`** (xoá — xem §7 quyết định 1), gồm cả vai
trò làm mục tiêu test `RequireGlobalScope` của `TokenScopeTests`.

⚠️ **`PATCH` đổi `Slug` PHẢI gọi `IShopLookupService.InvalidateAsync` cho CẢ slug cũ lẫn mới**, ngay
sau `SaveChangesAsync` thành công. Yêu cầu này đã được ghi sẵn ở `backend/docs/modules/identity.md`
khi dựng cache Redis — **task này là nơi đầu tiên nó có hiệu lực**. Quên = middleware resolve sai
tenant tới 30 phút.

### 4.5 Test bắt buộc

- Tenant isolation: user của shop A gọi `/shops/{B}` → 403 (không phải 404 rỗng do filter).
- Token scope: token `shop:*` bị từ chối ở cả 4 endpoint (`03` §7.1).
- Slug: trùng → 409; nằm trong reserved-routes → 422; sai format → 422.
- `ExternalOnly` không có `ExternalUrl` → bị chặn (cả ở validator lẫn CHECK constraint DB).
- Tạo shop → đúng 1 `UserShop` với `RoleId = Owner`, `Source = ShopCreator`.
- Đổi slug → `InvalidateAsync` được gọi cho cả hai slug (test bằng spy).
- `[Fact(Skip = "Listing chưa tồn tại — mở lại ở module Marketplace")]` cho ràng buộc `04` §2.2.

Integration test chạy Testcontainers → **ghi nợ vào `Docs/DOCKER-TEST-DEBT.md`** nếu máy thực thi
không có Docker (quy ước đã có).

### 4.6 Kết thúc session BE

`export.mjs` → `diff-report.mjs` → viết `Docs/tasks/SHOP-001/contract-diff.md` (kèm mục "Giả định
tôi đã tự đặt") → **DỪNG, chờ Gate 1**. Sau khi duyệt mới `promote.mjs` rồi viết `brief.md`.

---

## 5. Session FE (mở SAU Gate 1, không sớm hơn)

App: **`apps/portal`** (CSR, `admin.vsite.vn` — Quyết định #22/#25). Hiện mới có skeleton
(`__root`, `index`, `dev-registry`) — **chưa có auth, chưa có layout, chưa có màn hình nào**.

1. `pnpm gen:api` (Orval) từ contract đã duyệt → `packages/api-sdk`.
2. Dựng UI với **MSW mock trước** (bắt buộc theo §4 workflow — chống FE bám vào hành vi ngoài contract).
3. Màn hình: đăng ký · đăng nhập · danh sách shop của tôi · tạo shop · sửa shop.
4. Lưu/refresh token, route guard, shop switcher đọc `GET /shops`.
5. Integration: tắt MSW, chạy API thật.

### ⚠️ Thiết lập dev bắt buộc — Host header phải đúng, nếu không audience sai

Portal phải được phục vụ tại host có nhãn đầu là `admin`, nếu không
`TenantResolutionMiddleware` sẽ resolve thành `vsite-main` chứ không phải `vsite-portal`:

1. hosts file Windows: `127.0.0.1  admin.vsite.local`
2. Vite dev server bind `admin.vsite.local`
3. **Vite proxy `/auth` + `/shops` → `http://localhost:5270` với `changeOrigin: false`** để giữ
   nguyên Host header. Gọi cross-origin thẳng tới `localhost:5270` sẽ hỏng audience (Host thành
   `localhost`) — xem `Docs/tasks/IDENTITY-001/brief.md` mục "FE KHÔNG cần biết shopId".

---

## 6. Verification

- `dotnet build` 0 error 0 warning; `ArchitectureTests` xanh (**đặc biệt `ModuleBoundaryTests`** —
  nó sẽ bắt ngay nếu `UserShop.Shop` navigation còn sót).
- `check-no-drift.mjs` sạch sau khi promote.
- `pnpm test` (FE) xanh; UI chạy được **hoàn toàn bằng MSW mock** trước khi bật API thật.
- Luồng end-to-end ở §2 chạy thật một lần trên máy có Docker, không cần một câu SQL tay nào.
- Xoá mục 2 khỏi `Docs/DOCKER-TEST-DEBT.md` (bước "insert Shop bằng tay" không còn lý do tồn tại).
- `change-reviewer` trước Gate 2.

---

## 7. Quyết định đã chốt (2026-09-13, trước khi thực thi)

1. **Bỏ `GET /auth/me/shops`**, `GET /shops` thay thế hoàn toàn.
   - Đây là **BREAKING** trong `contract-diff.md` — nhưng là breaking **có chủ đích**, không phải
     bug implementation (quy tắc contract #3). Lý do: endpoint này thêm ở Phase 3 chỉ để có mục
     tiêu test `RequireGlobalScope`, **chưa FE nào tiêu thụ**, và sau khi `Shop` tách module thì nó
     không thể trả `Name`/`Slug` được nữa (`Identity.dependsOn = []`; thêm `Shop` vào đó sẽ tạo
     vòng lặp với `Shop.dependsOn = ["Identity"]`). `GET /shops` vừa thay được vai trò test
     `RequireGlobalScope`, vừa đủ dữ liệu cho shop switcher.
   - Việc cần làm: xoá `ListMyShopsQuery`/`Handler` + endpoint + phần tương ứng trong
     `TokenScopeTests`; ghi rõ lý do vào `contract-diff.md` để Gate 1 duyệt breaking này một cách
     tường minh.

2. **Giữ nguyên link verify-email trỏ vào endpoint BE.** Session FE lấy token từ console log của
   `LoggingEmailSender` để test. Đổi link sang trang FE là task riêng, làm cùng lúc chọn nhà cung
   cấp email thật — gộp vào đây sẽ phải thêm config base URL riêng cho từng loại domain
   (portal/web/shop) và làm phình phạm vi.

3. **Endpoint filter dùng chung** cho cơ chế ở §3, đặt tại `Vsite.Api/Tenancy/`. Mọi màn hình Portal
   sau này (Listing, Media, Product…) dùng lại, không ai phải viết lại check membership — và quan
   trọng hơn, không ai có thể **quên** viết nó.
