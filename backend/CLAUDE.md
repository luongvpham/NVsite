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

## Cấu trúc thư mục — nguồn sự thật để biết tạo/tìm file ở đâu

**ĐÚNG 4 project cho toàn hệ** — `Vsite.Domain` / `Vsite.Application` / `Vsite.Infrastructure` /
`Vsite.Api`, chốt tại lần gộp 8→4 ngày 2026-09-13. **Module là FOLDER + NAMESPACE**,
không phải project riêng — xem mục "Ranh giới module" bên dưới.

```
backend/src/
  Vsite.Domain/                   ← thuần: không EF Core, không ASP.NET Core, không Npgsql
    Common/       BaseEntity.cs BaseAuditableEntity.cs ShopEntity.cs ShopAuditableEntity.cs
                  IShopScoped.cs BaseEvent.cs
    Abstractions/ ITenantContext.cs
    Exceptions/   AppException + NotFound/Domain/Conflict/ForbiddenAccess/TooManyRequests
    Authorization/ AuthPolicies.cs AudienceHelpers.cs
    Pagination/  ReservedRoutes/
    {Module}/     Entities/{Entity}.cs  Enums/{Name}.cs  ← vd. Identity/
  Vsite.Application/              ← CQRS, chỉ phụ thuộc Domain
    Common/       Behaviors/ValidationBehavior.cs  Exceptions/  Interfaces/{IAppDbContext,ICurrentUserContext}.cs
    {Module}/     Interfaces/  Options/  {Feature}/Commands/{UseCase}/  {Feature}/Queries/{UseCase}/
  Vsite.Infrastructure/
    Persistence/  AppDbContext.cs AppDbContextFactory.cs TenantQueryFilterExtensions.cs
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

**Deploy: chỉ MỘT thứ.** `Vsite.Api` là project duy nhất `Microsoft.NET.Sdk.Web` và có `Program.cs`.
`dotnet publish src/Vsite.Api` gom cả 4 assembly vào một output → 1 container, 1 process. Thêm module
= thêm folder, **không** thêm deployment.

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
| `ShopEntity` | Thuộc về một shop, KHÔNG cần audit trail (hiếm) |
| `ShopAuditableEntity` | Thuộc về một shop VÀ cần audit trail — phổ biến nhất cho entity nghiệp vụ (`UserShop`, sau này `Product`/`Service`/`Booking`...) |

Kế thừa `ShopEntity`/`ShopAuditableEntity` là **đủ** để có Global Query Filter theo `ShopId` —
không viết tay `HasQueryFilter` nữa (xem `Vsite.Infrastructure.Persistence.TenantQueryFilterExtensions`).

**Entity không cho set `Id` tự do:** `BaseEntity.Id` là `protected set`. Entity cần seed data với
GUID cố định phải tự expose constructor `public {Entity}(Guid id) : base(id) { }` — xem
`Vsite.Domain.Identity.Entities.Role` + `Vsite.Infrastructure.Persistence.Seed.RoleSeed`.

## Ranh giới module (Quyết định #1) ⚠️

**Module = folder + namespace `Vsite.{Domain|Application|Infrastructure|Api}.{Module}`**, KHÔNG phải
project riêng (sửa 2026-09-13 — trước đó mỗi module có 4 `.csproj`).

- **Danh sách module + chiều phụ thuộc cho phép có đúng MỘT nguồn:**
  `Docs/architecture/dependency-map.json`. Thêm module mới mà quên khai ở đó → `ModuleBoundaryTests`
  FAIL. Không viết danh sách thứ hai ở bất kỳ đâu (#17, #24).
- Namespace của module A chỉ được phụ thuộc namespace của module B nếu B nằm trong `A.dependsOn`.
- Namespace dùng chung (`Common`, `Abstractions`, `Exceptions`, `Persistence`, `Tenancy`…) không bị
  luật này ràng buộc — danh sách ở `ModuleBoundaryTests.SharedSegments`.
- Chiều phụ thuộc giữa các tầng: `Domain ← Application ← Infrastructure ← Api`, không đảo — cái này
  vẫn được enforce ở **compile-time** (4 assembly, ProjectReference một chiều).
- ⚠️ Ranh giới **giữa các module** giờ chỉ còn `ModuleBoundaryTests` chặn (test-time, không phải
  compile-time). Đó là lớp phòng thủ DUY NHẤT — thấy nó đỏ thì sửa code, **đừng nới luật**.
- Cross-module đọc dữ liệu: khai interface ở `Vsite.Application/{Module}/Interfaces/`, implement ở
  `Vsite.Infrastructure/{Module}/` (mẫu đang chạy: `IShopLookupService`,
  `IUserShopMembershipService`). Không cần Integration Event bus cho việc đọc.

## Tenant security invariants (Quyết định #21) ⚠️

Vi phạm là lỗi bảo mật, không phải code style:

1. Mọi entity tenant-scoped **phải** có `ShopId`.
2. Mọi query **phải** đi qua Global Query Filter theo `TenantContext`.
3. Child resource **phải** validate ownership **trong câu query** (`WHERE ParentId = ...`), KHÔNG load rồi check ở memory.
4. **Không bao giờ** nhận `ShopId` từ request body — chỉ lấy từ route hoặc `TenantContext`.
5. Quyền theo shop kiểm ở **Authorization Handler**, không tin claim trong token.

## Quy ước codegen bắt buộc (Quyết định #19)

| Quy ước | Cách làm |
|---|---|
| Enum serialize dạng string | `JsonStringEnumConverter` đăng ký global trong `Api` |
| Đủ error shape trong OpenAPI | Mọi endpoint có `[ProducesResponseType]` cho từng status code có thể trả |
| Lỗi trả ProblemDetails | RFC 7807, luôn có `error_code` machine-readable |
| Pagination một shape | `{ items, total, page, pageSize }` — không có shape thứ hai |
| Nested REST cho child resource | `/shops/{shopId}/services/{id}` — ownership validate ngay trong route |

## OpenAPI — thư viện và cách xuất document theo module ✅ Đã chốt

**Thư viện: `Microsoft.AspNetCore.OpenApi` (built-in .NET 9).** Không dùng Swashbuckle.

**Bắt buộc:** committed contract chia theo module (`contracts/openapi/{module}.v{n}.json`), nên runtime cũng phải xuất được **từng document riêng theo module** — không phải một file `v1` gộp tất cả.

Cách làm:

1. Đăng ký **một `AddOpenApi(documentName)` riêng cho mỗi module** trong `Api/Program.cs`, ví dụ `AddOpenApi("identity")`, `AddOpenApi("shop")`, `AddOpenApi("listing")`.
2. Gắn nhóm cho từng endpoint bằng `.WithGroupName("{module}")` (Minimal API) hoặc tag tương đương cho Controller-based — group name phải khớp `documentName` ở bước 1.
3. Endpoint HTTP (`/openapi/{documentName}.json`) chỉ dùng để **verify khi dev chạy local**, KHÔNG phải nguồn cho contract-sync.
4. **Xuất ra file lúc build/CLI** — dùng `Microsoft.Extensions.ApiDescription.Server` (package MSBuild có sẵn của .NET 9 cho OpenAPI) để generate file tĩnh `{module}.json` cho từng document vào `contracts/openapi/.staging/` khi chạy `dotnet build` hoặc một target MSBuild riêng (`dotnet build /t:GenerateOpenApiDocuments` hoặc script wrapper trong `tools/contract-sync/`). Đây là điều kiện bắt buộc theo skill bootstrap — thiếu bước này thì `tools/contract-sync` không có gì để diff.
5. Naming file: `contracts/openapi/.staging/{module}.v{n}.json`, `{module}` = tên document ở bước 1 (lowercase, khớp tên module), `{n}` bắt đầu từ `1`.

## Quy tắc normalize khi diff contract ✅ Đã chốt

`tools/contract-sync/` áp dụng đúng bộ tối thiểu sau trước khi so sánh hai bên (runtime vs committed):

- Sort key đệ quy (object keys) trước khi so sánh.
- Bỏ `servers` và `info.version` — hai trường này đổi theo môi trường/thời điểm build, không phản ánh thay đổi API thật.
- Chuẩn hoá whitespace trong mọi `description` (trim, collapse nhiều khoảng trắng thành một).
- So sánh **có cấu trúc theo từng operation** (method + path), không so text thô toàn file.

Không tự thêm luật normalize khác mà không hỏi — luật lỏng quá sẽ bỏ lọt breaking change thật; luật chặt quá sẽ báo diff giả liên tục.


## Testing

Bắt buộc có, không phải tuỳ chọn:

- **Architecture test** — enforce: `Domain` không reference EF Core/MediatR; module không reference project module khác; chiều phụ thuộc layer đúng. Ranh giới nào chỉ nằm trong tài liệu thì sẽ bị vi phạm.
- **Tenant isolation test** — với mỗi entity tenant-scoped: query từ shop A không thấy dữ liệu shop B.
- **Unit test** cho handler và validator.
- **Integration test** cho endpoint, chạy trên database thật (Testcontainers hoặc tương đương).

⚠️ **Môi trường không có Docker daemon** (nhiều dev/agent chạy nhiều máy, chỉ một máy cài Docker):
KHÔNG bỏ qua Testcontainers test — viết test đầy đủ, xác nhận build/logic đúng bằng mắt, rồi ghi
nợ lại vào `Docs/DOCKER-TEST-DEBT.md` (quy ước dùng chung, đọc file đó trước khi ghi) để máy có
Docker chạy xác nhận sau. Xoá đúng mục khỏi file đó khi đã chạy pass thật.

---

## Definition of done (một task BE)

1. Domain / Application / Infrastructure / Api đúng ranh giới
2. Validation + authentication + authorization
3. Test pass, gồm cả architecture test và tenant isolation test
4. Tuân thủ đủ quy ước #19
5. Tuân thủ đủ 5 invariant #21
6. Export runtime OpenAPI theo document module
7. Chạy skill `contract-sync`, sinh `Docs/tasks/{ID}/contract-diff.md`
8. **Dừng lại chờ Gate 1.** Chỉ viết `brief.md` sau khi contract được duyệt
9. **Đồng bộ tài liệu — không có bước này thì task CHƯA XONG**, kể cả khi code chạy và test xanh:
   - Viết `Docs/tasks/{ID}/changelog.md` — từng điểm thực thi lệch so với file `DesignIdeal/`
     tương ứng, chia rõ **"lệch có chủ đích"** và **"chưa làm xong"**. Mẫu:
     `Docs/tasks/IDENTITY-001/changelog.md`.
   - Cập nhật dòng `> **STATUS:**` ở đầu file `DesignIdeal/` mà task này làm lệch — trỏ changelog,
     ghi rõ mục nào **đừng tin nữa**. Banner đó là nguồn duy nhất; `00-INDEX.md` §2 sinh ra từ nó.
   - Quyết định mới người duyệt chốt giữa chừng → **cấp số tại `DesignIdeal/DECISIONS.md` trước**,
     rồi mới viết nội dung ở file chuyên đề.
   - Nợ test cần Docker → ghi vào `Docs/DOCKER-TEST-DEBT.md`, đừng báo miệng qua chat.

## Thứ tự module thật (Phase 1)

Sample module (throwaway, đã chứng minh pipeline Bước 1) đã bị xoá — xem `Docs/tasks/CLEANUP-SAMPLE.md` cho lịch sử dọn dẹp.

`Identity` → `Shop` → `Marketplace`. Phase 2: `Media` → `Website` → `Catalog`.
Danh sách đầy đủ + chiều phụ thuộc: `Docs/architecture/dependency-map.json` (nguồn duy nhất).

| Module | Gồm | Tài liệu thiết kế |
|---|---|---|
| `Identity` | User, ExternalLogin, Role, UserShop, PendingRegistration, RefreshToken, PasswordResetToken | `03` |
| `Shop` | Shop (đầy đủ), ShopDomain | `04` §2.1 |
| `Marketplace` | ServiceCategory, Listing, ShopCategoryHistory, Review, Lead + index ES | `04` |
| `Media` | MediaAsset + pipeline ảnh | `05` §9 |
| `Website` | Website, Theme, Page, PageDraft, SitePublication, NavigationConfig, WebsiteTemplate | `05` §1–§12 |
| `Catalog` | Product (+attribute/variant/image) **và** Service (+ShopServiceGroup) | `05` §13–§22, `06` |

⚠️ `Catalog` gộp Product + Service ở mức project nhưng **bảng và enum tách hoàn toàn** (Quyết định
#40). `06` §1 gọi đây là "chỗ dễ nhầm nhất trong toàn hệ" — `backend/docs/modules/catalog.md` phải
mở đầu bằng bảng phân biệt `Listing`/`Service`/`Product` trước khi viết dòng code nào.

## Tài liệu từng module

| Module | File |
|---|---|
| Identity | `backend/docs/modules/identity.md` |
| Shop | `backend/docs/modules/shop.md` |
