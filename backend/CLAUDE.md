# backend — vsite

.NET 9, Modular Monolith, Clean Architecture + DDD Lite + CQRS.

**File này là nguồn sự thật cho layout backend vsite.** `DesignIdeal/architecture-guide.md` chỉ là
tham chiếu ngoại lai (lấy từ dự án TPCS, còn nguyên placeholder `{Entity}`/`{Project}`) — dùng để
tham khảo pattern chung khi ở đây không nói tới, và **khi hai bên khác nhau thì file này thắng**.

### Tài liệu thiết kế cho phạm vi backend

Chỉ đọc khi thật sự cần nền thiết kế. Trạng thái từng file (còn tin được không) ở
`DesignIdeal/00-INDEX.md` §2 — **đọc dòng đó trước khi mở file**.

| Cần gì | Đọc |
|---|---|
| Một quyết định `#N` | `DesignIdeal/DECISIONS.md` — đừng quét `02` |
| `User` `ExternalLogin` `UserShop` `Role` `PendingRegistration` | `DesignIdeal/03-identity-entity-design.md` ⚠️ có lệch — đọc `Docs/tasks/IDENTITY-001/changelog.md` kèm |
| `Shop` (đầy đủ) `ServiceCategory` `Listing` `Review` `Lead` | `DesignIdeal/04-listing-and-review-design.md` |
| `Website` `Page` `PageDraft` `MediaAsset` `Product` | `DesignIdeal/05-website-builder-and-product-design.md` |
| `Service` `ShopServiceGroup` | `DesignIdeal/06-service-design.md` |
| Quy ước vận hành module đã code | `backend/docs/modules/{module}.md` |

## Lệnh hay dùng

```bash
# Tiền đề (fresh clone / CI): Vsite.Api.csproj link artifact gitignored derivative-presets.json —
# thiếu thì build dừng ở target EnsureDerivativePresetsArtifact. Job CI mới build Vsite.Api cũng phải có bước này.
pnpm install && pnpm --filter @vsite/builder-components run gen:registry

dotnet test backend/tests/ArchitectureTests        # không cần Docker
dotnet test backend/vsite.sln                      # IntegrationTests cần Docker (Testcontainers)
dotnet ef migrations add {Name} --project backend/src/Vsite.Infrastructure --startup-project backend/src/Vsite.Api
pnpm contract:export [module...]                   # build Api → contracts/openapi/.staging/{module}.v1.json
pnpm contract:diff                                 # → dùng qua skill contract-sync
```

## Cấu trúc thư mục — nguồn sự thật để biết tạo/tìm file ở đâu

**ĐÚNG 4 project cho toàn hệ** — `Vsite.Domain` / `Vsite.Application` / `Vsite.Infrastructure` /
`Vsite.Api`. **Module là FOLDER + NAMESPACE**, không phải project riêng — xem "Ranh giới module".

```
backend/src/
  Vsite.Domain/                   ← thuần: không EF Core, không ASP.NET Core, không Npgsql
    Common/       BaseEntity.cs BaseAuditableEntity.cs TenantEntity.cs TenantAuditableEntity.cs
                  IShopScoped.cs BaseEvent.cs
    Abstractions/ ITenantContext.cs
    Exceptions/   AppException + NotFound/Domain/Conflict/ForbiddenAccess/TooManyRequests
    Authorization/ AuthPolicies.cs AudienceHelpers.cs
    Pagination/  ReservedRoutes/
    {Module}/     Entities/{Entity}.cs  Enums/{Name}.cs  ← vd. Identity/
  Vsite.Application/              ← CQRS, chỉ phụ thuộc Domain
    Common/       Behaviors/ValidationBehavior.cs  Exceptions/  Interfaces/{IAppDbContext,ICurrentUserContext,IAuditActor}.cs
    {Module}/     Interfaces/  Options/  {Feature}/Commands/{UseCase}/  {Feature}/Queries/{UseCase}/
  Vsite.Infrastructure/
    Persistence/  AppDbContext.cs TenantQueryFilterExtensions.cs
                  Configurations/{Module}/{Entity}Configuration.cs
                  Seed/  Migrations/
    Configuration/ ReservedRoutesProvider.cs
    {Module}/     implementation của interface khai ở Application/{Module}/Interfaces/
    DependencyInjection.cs        ← AddInfrastructure() — điểm DUY NHẤT wiring DI
  Vsite.Api/                      ← host: Program.cs, middleware, OpenAPI
    Tenancy/  ExceptionHandling/  OpenApi/  Auth/
    {Module}/{Module}Endpoints.cs
backend/tests/
  ArchitectureTests/              ← LayeringTests (assembly) + ModuleBoundaryTests (namespace)
  IntegrationTests/               ← Common/ + {Module}/
  ComponentSchemaTests/
backend/docs/modules/{module}.md  ← tài liệu từng module (module trải trên 4 project nên không
                                    đặt CLAUDE.md trong một folder nào được)
```

**Deploy: chỉ MỘT thứ.** `Vsite.Api` là project `Microsoft.NET.Sdk.Web` duy nhất. Thêm module =
thêm folder, **không** thêm deployment.

**Một DbContext duy nhất** (`AppDbContext`). Module mới thêm `DbSet` vào đó + `IAppDbContext`, đặt
`IEntityTypeConfiguration` dưới `Persistence/Configurations/{Module}/` (tự động được quét). KHÔNG
tạo `{Module}DbContext` thứ hai — thiết kế có ≥6 FK **xuyên module** (vd.
`Listing.(TargetPageId, ShopId) → Page(Id, ShopId)`, `04` §4.1 — một biện pháp bảo mật ở tầng DB),
nhiều DbContext thì EF Core không diễn đạt được chúng.

**Entity nào kế thừa base class nào:**

| Kế thừa | Khi nào |
|---|---|
| `BaseEntity` | Không cần audit trail, không platform/shop-scoped cố định (vd. `PendingRegistration` — staging ngắn hạn) |
| `BaseAuditableEntity` | Cần `CreatedAt`/`UpdatedAt`/soft-delete, KHÔNG thuộc về một shop cụ thể (platform-scoped, vd. `User`, `Role`) |
| `TenantEntity` | Thuộc về một shop, KHÔNG cần audit trail (hiếm) |
| `TenantAuditableEntity` | Thuộc về một shop VÀ cần audit trail — phổ biến nhất cho entity nghiệp vụ (`UserShop`, sau này `Product`/`Service`/`Booking`...) |

Kế thừa `TenantEntity`/`TenantAuditableEntity` là **đủ** để có Global Query Filter theo `ShopId` —
không viết tay `HasQueryFilter` nữa (xem `Vsite.Infrastructure.Persistence.TenantQueryFilterExtensions`).
`ShopId` trên base là `init` — gán lúc tạo, không đổi được (không "chuyển" entity sang tenant khác).

**Tên base class ≠ tên entity:** base tenant là `TenantEntity`/`TenantAuditableEntity` (đổi từ
`ShopEntity`/`ShopAuditableEntity` ở REFACTOR-BE-001 vì đụng alias `ShopEntity = …Shop.Entities.Shop`).
Entity mới không đặt trùng tên module (`Website`, `Theme`…) — trùng thì phải alias khắp nơi.

**Entity có hành vi, không setter public:** thay đổi trạng thái qua method của entity để invariant
nằm ở Domain (mẫu: `MediaAsset`, `Shop.Update`/`SetLogo`). Validator vẫn chặn sớm để trả 422;
entity ném `DomainException` là lớp chặn thứ hai.

**Audit "by user":** `AppDbContext` tự ghi `CreatedByUserId`/`UpdatedByUserId` từ `IAuditActor`
(null khi chưa đăng nhập) — handler không gán tay. (`CreatedByUserId` đã có giá trị thì giữ nguyên —
chỉ dùng khi tạo thay người khác, vd. job nền.)

**Entity không cho set `Id` tự do:** `BaseEntity.Id` là `protected set`. Entity cần seed data với
GUID cố định phải tự expose constructor `public {Entity}(Guid id) : base(id) { }` — xem
`Vsite.Domain.Identity.Entities.Role` + `Vsite.Infrastructure.Persistence.Seed.RoleSeed`.

## Quy ước database (REFACTOR-DB-001)

- **snake_case cho mọi tên** — bảng, cột, PK/FK/index (`EFCore.NamingConventions`, bật trong
  `AppDbContext.OnConfiguring` để test tự dựng context cũng ra cùng model). Bảng = tên entity **số
  ít** (`shop`, `user_shop`, `media_asset`); `ToTable("...")` viết tay cũng phải snake_case.
- **Bảng `User` tên là `app_user`** — `user` là từ khoá Postgres (`SELECT * FROM user` trả về
  `current_user`, không lỗi). Tránh mọi tên bảng là từ khoá reserved (`user`, `order`, `group`…);
  `role` không reserved nên dùng được.
- **SQL viết tay** (CHECK, `HasFilter`, test, job) dùng tên snake_case **không quote**. Tên ràng buộc
  tự đặt: `ck_{bảng}_{ý}`, `ix_{bảng}_{ý}`, unique có lọc `ux_{bảng}_{ý}`.
- **Soft delete** = `IsDeleted` (cho Global Query Filter) + `DeletedAt` (`AppDbContext` tự đóng dấu,
  kể cả khi entity tự set `IsDeleted` không qua `Remove()`).
- **Unique index không tự lọc soft delete.** Mỗi unique phải chọn rõ: giữ toàn cục (vd. `shop.slug`
  — slug của shop đã xoá vẫn bị giữ) thì kiểm trùng bằng `IgnoreQueryFilters()` để trả 409; cho
  dùng lại thì thêm `.HasFilter("NOT is_deleted")`.
- **Migration đã gộp lại một `InitialSchema` ở REFACTOR-DB-001** (trước production). DB dev cũ phải
  xoá: `docker compose down -v` rồi chạy lại — triệu chứng nếu quên: `column "migration_id" does not
  exist` khi app khởi động/migrate. Từ giờ chỉ thêm migration mới, không gộp nữa.
- **Tên FK nối tới `app_user`/`shop` ghim bằng `HasConstraintName`** — NamingConventions đặt tên FK
  theo thứ tự cấu hình (có lúc lấy tên DbSet `users`/`shops`), không ghim thì thêm configuration sau
  có thể sinh migration đổi tên vô cớ.

## Ranh giới module (Quyết định #1) ⚠️

**Module = folder + namespace `Vsite.{Domain|Application|Infrastructure|Api}.{Module}`.**

- **Danh sách module, phase và chiều phụ thuộc cho phép có đúng MỘT nguồn:**
  `Docs/architecture/dependency-map.json`. Thêm module mới mà quên khai ở đó → `ModuleBoundaryTests`
  FAIL. Không viết danh sách thứ hai ở bất kỳ đâu (#17, #24).
- Namespace của module A chỉ được phụ thuộc namespace của module B nếu B nằm trong `A.dependsOn`.
- Namespace dùng chung (`Common`, `Abstractions`, `Exceptions`, `Persistence`, `Tenancy`…) không bị
  luật này ràng buộc — danh sách ở `ModuleBoundaryTests.SharedSegments`.
- Chiều phụ thuộc giữa các tầng: `Domain ← Application ← Infrastructure ← Api`, không đảo — enforce ở
  **compile-time** (ProjectReference một chiều).
- ⚠️ Ranh giới **giữa các module** chỉ có `ModuleBoundaryTests` chặn (test-time). Đó là lớp phòng
  thủ DUY NHẤT — thấy nó đỏ thì sửa code, **đừng nới luật**.
- Cross-module đọc dữ liệu: khai interface ở `Vsite.Application/{Module}/Interfaces/`, implement ở
  `Vsite.Infrastructure/{Module}/` (mẫu đang chạy: `IShopLookupService`,
  `IUserShopMembershipService`). Không cần Integration Event bus cho việc đọc.
- ⚠️ `Catalog` gộp Product + Service nhưng **bảng và enum tách hoàn toàn** (#40). `06` §1 gọi đây là
  "chỗ dễ nhầm nhất trong toàn hệ" — `backend/docs/modules/catalog.md` phải mở đầu bằng bảng phân
  biệt `Listing`/`Service`/`Product` trước khi viết dòng code nào.

## Tenant security invariants (Quyết định #21) ⚠️

Vi phạm là lỗi bảo mật, không phải code style:

1. Mọi entity tenant-scoped **phải** có `ShopId`.
2. Mọi query **phải** đi qua Global Query Filter theo `TenantContext`.
3. Child resource **phải** validate ownership **trong câu query** (`WHERE ParentId = ...`), KHÔNG load rồi check ở memory.
4. **Không bao giờ** nhận `ShopId` từ request body — chỉ lấy từ route hoặc `TenantContext`.
5. Quyền theo shop kiểm ở **Authorization Handler**, không tin claim trong token.
   Cách làm hiện tại: endpoint có `{shopId}` gắn `.RequireShopMembership()`; cần role Owner thì
   `.RequireShopOwner(errorCode, detail)`. **Handler không tự query `UserShop`/`RoleId`.**
6. **`IgnoreQueryFilters()` chỉ ở file trong allowlist** (`IgnoreQueryFiltersAllowlistTests`) — nó tắt
   CẢ filter tenant lẫn soft-delete. Đọc `UserShop` xuyên shop thì dùng `UserShopQueries`
   (Quyết định #90).

## Quy ước codegen bắt buộc (Quyết định #19)

| Quy ước | Cách làm |
|---|---|
| Enum serialize dạng string | `JsonStringEnumConverter` đăng ký global trong `Api` |
| Đủ error shape trong OpenAPI | Mọi endpoint có `[ProducesResponseType]` cho từng status code có thể trả |
| Lỗi trả ProblemDetails | RFC 7807, luôn có `error_code` machine-readable |
| Pagination một shape | `{ items, total, page, pageSize }` — không có shape thứ hai |
| Nested REST cho child resource | `/api/shops/{shopId}/services/{id}` — ownership validate ngay trong route |

## OpenAPI

`Microsoft.AspNetCore.OpenApi` (built-in), không Swashbuckle. Mỗi module **một document riêng**
(contract chia theo module): module mới thêm `AddOpenApi("{module}")` trong `Program.cs` +
`.WithGroupName("{module}")` cho endpoint — tên lowercase, khớp nhau. File tĩnh sinh lúc build
(`OpenApiGenerateDocumentsOnBuild`), lấy ra bằng `pnpm contract:export`; endpoint
`/openapi/{module}.json` chỉ để xem khi chạy local, không phải nguồn cho contract-sync.

**Property object nullable (#87):** transformer dùng chung
`Vsite.Api/OpenApi/DuplicateNullableSchemaDocumentTransformer.cs` đã đăng ký cho mọi document — nó
viết lại thành `{ "allOf": [ { "$ref": ... } ], "nullable": true }` để Orval không sinh type trùng
`XDto2`. **Không sửa từng DTO bằng tay**; module mới chỉ cần đăng ký document như mọi module khác.

**Prefix `/api` + `operationId` tường minh (#91, REFACTOR-API-001):**
- Mọi endpoint API map qua group `ApiRoutes.Prefix` (`/api`) ở `Program.cs` — module mới gọi
  `api.Map{Module}Endpoints()`, không map thẳng lên `app`. Ngoại lệ có chủ đích: `/media/*` (file
  ảnh public, không phải API) và `/openapi/*` (chỉ dev).
- Mọi endpoint **bắt buộc** `.WithName("{động từ}{Danh từ}")` camelCase (vd. `listShops`,
  `uploadToLibrary`) — đó là `operationId`, và là tên hàm/hook Orval sinh ra cho FE. Đặt theo nghiệp
  vụ, **không theo path**, để đổi path không làm đổi tên ở FE. Đổi tên một `operationId` = đổi
  contract (Gate 1).
- `ApiRoutePrefixTests` chặn cả hai: endpoint ngoài `/api`, endpoint thiếu/trùng `operationId`.

## Testing

Bắt buộc có, không phải tuỳ chọn:

- **Architecture test** — `Domain` không reference EF Core/MediatR; chiều phụ thuộc layer đúng;
  ranh giới namespace giữa module đúng `dependency-map.json`.
- **Tenant isolation test** — với mỗi entity tenant-scoped: query từ shop A không thấy dữ liệu shop B.
- **Unit test** cho handler và validator.
- **Integration test** cho endpoint, chạy trên database thật (Testcontainers). Máy không có Docker:
  **không** Skip — xem DoD bước 9.

---

## Definition of done (một task BE)

1. Domain / Application / Infrastructure / Api đúng ranh giới
2. Validation + authentication + authorization
3. Test pass, gồm cả architecture test và tenant isolation test
4. Tuân thủ đủ quy ước #19
5. Tuân thủ đủ 5 invariant #21
6. Export runtime OpenAPI theo document module
7. Chạy skill `contract-sync`, sinh `Docs/tasks/{ID}/contract-diff.md`
8. **Gate 1** (gọn trước production, #89): có câu hỏi/`REMOVED` thì dừng chờ duyệt; chỉ thêm và không câu hỏi thì promote. `brief.md` viết sau promote
9. **Đồng bộ tài liệu — không có bước này thì task CHƯA XONG**, kể cả khi code chạy và test xanh:
   - Viết `Docs/tasks/{ID}/changelog.md` — từng điểm thực thi lệch so với file `DesignIdeal/`
     tương ứng, chia rõ **"lệch có chủ đích"** và **"chưa làm xong"**. Mẫu:
     `Docs/tasks/IDENTITY-001/changelog.md`.
   - Cập nhật dòng `> **STATUS:**` ở đầu file `DesignIdeal/` mà task này làm lệch — trỏ changelog,
     ghi rõ mục nào **đừng tin nữa**. Banner đó là nguồn duy nhất; `00-INDEX.md` §2 sinh ra từ nó.
   - Quyết định mới người duyệt chốt giữa chừng → **cấp số tại `DesignIdeal/DECISIONS.md` trước**,
     rồi mới viết nội dung ở file chuyên đề.
   - Nợ test cần Docker → viết test đầy đủ rồi ghi vào `Docs/DOCKER-TEST-DEBT.md` (đọc quy ước
     trong file trước khi ghi), đừng báo miệng qua chat. Máy có Docker chạy pass thì xoá đúng mục.
   - Module mới → viết `backend/docs/modules/{module}.md`.
