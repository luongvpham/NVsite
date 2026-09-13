# backend — vsite

.NET 9, Modular Monolith, Clean Architecture + DDD Lite + CQRS. Chi tiết đầy đủ ở
`DesignIdeal/02-tech-stack-and-decision.md` §1, §Quyết định #1, và **`DesignIdeal/architecture-guide.md`**
(quy ước layout Domain/Application/Infrastructure chi tiết — feature folders, CQRS/MediatR, base
entity, exception hierarchy — đọc trước khi thêm module/entity/use-case mới).

## Cấu trúc thư mục — nguồn sự thật để biết tạo/tìm file ở đâu

```
backend/src/
  Api/                            ← host, middleware, DI, OpenAPI document setup
  Shared/                         ← primitives DÙNG CHUNG MỌI MODULE, thuần (không EF Core/ASP.NET Core)
    Domain/
      BaseEntity.cs                ← Id (protected set — chỉ gán qua constructor), DomainEvents
      BaseAuditableEntity.cs       ← + CreatedAt/UpdatedAt/CreatedByUserId/UpdatedByUserId/IsDeleted
      ShopEntity.cs                ← BaseEntity + ShopId (không audit)
      ShopAuditableEntity.cs       ← BaseAuditableEntity + ShopId (dùng phổ biến hơn ShopEntity)
      IShopScoped.cs               ← marker `ShopId` cho TenantQueryFilterExtensions tìm bằng reflection
      BaseEvent.cs                 ← domain event, implements MediatR.INotification
      Abstractions/ITenantContext.cs
    Exceptions/                    ← AppException + NotFoundException/DomainException/ConflictException/ForbiddenAccessException
    Pagination/, ReservedRoutes/   ← primitives khác đã có từ Bước 1
  Shared.Persistence/              ← DUY NHẤT project Shared được phép reference EF Core
    AppDbContextBase.cs            ← mọi {Module}DbContext PHẢI kế thừa từ đây, không kế thừa DbContext thẳng
    TenantQueryFilterExtensions.cs ← Global Query Filter TỰ ĐỘNG cho IShopScoped + soft-delete, gọi 1 lần trong AppDbContextBase
  Modules/{ModuleName}/
    {ModuleName}.Domain/           ← không reference EF Core, MediatR (trừ BaseEvent qua Shared), ASP.NET, hay project module khác
      Entities/{Entity}.cs         ← kế thừa BaseEntity/BaseAuditableEntity/ShopEntity/ShopAuditableEntity
      Enums/{Name}.cs
    {ModuleName}.Application/      ← CQRS: Commands/Queries theo feature folder (xem architecture-guide.md §3)
    {ModuleName}.Infrastructure/
      Persistence/{ModuleName}DbContext.cs
      Persistence/Configurations/{Entity}Configuration.cs
      Persistence/Migrations/
      DependencyInjection.cs
    {ModuleName}.Api/
backend/tests/
  ArchitectureTests/
  {ModuleName}.IntegrationTests/
```

**Entity nào kế thừa base class nào:**

| Kế thừa | Khi nào |
|---|---|
| `BaseEntity` | Không cần audit trail, không platform/shop-scoped cố định (vd. `PendingRegistration` — staging ngắn hạn) |
| `BaseAuditableEntity` | Cần `CreatedAt`/`UpdatedAt`/soft-delete, KHÔNG thuộc về một shop cụ thể (platform-scoped, vd. `User`, `Role`) |
| `ShopEntity` | Thuộc về một shop, KHÔNG cần audit trail (hiếm) |
| `ShopAuditableEntity` | Thuộc về một shop VÀ cần audit trail — phổ biến nhất cho entity nghiệp vụ (`UserShop`, sau này `Product`/`Service`/`Booking`...) |

Kế thừa `ShopEntity`/`ShopAuditableEntity` là **đủ** để có Global Query Filter theo `ShopId` —
không viết tay `HasQueryFilter` nữa (xem `Shared.Persistence.TenantQueryFilterExtensions`).

**Entity không cho set `Id` tự do:** `BaseEntity.Id` là `protected set`. Entity cần seed data với
GUID cố định phải tự expose constructor `public {Entity}(Guid id) : base(id) { }` — xem
`Identity.Domain.Entities.Role` + `Identity.Infrastructure.Persistence.RoleSeed`.

## Ranh giới module (Quyết định #1)

- Module **không được** reference project của module khác.
- Cross-module giao tiếp qua **Integration Event** hoặc **Public Contract interface**, không qua reference trực tiếp.
- Chiều phụ thuộc trong một module: `Domain ← Application ← Infrastructure ← Api`, không đảo.
- Mọi module **được phép** reference `Shared`/`Shared.Persistence` — đây không phải module, là hạ tầng dùng chung (không tính vào rule "module không reference module khác").
- Enforce bằng `ArchitectureTests` (NetArchTest hoặc tương đương) — test phải **fail thật** khi vi phạm, không chỉ nằm ở tài liệu (#17).

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

---

## Definition of done (một task BE)

1. Domain / Application / Infrastructure / Api đúng ranh giới
2. Validation + authentication + authorization
3. Test pass, gồm cả architecture test và tenant isolation test
4. Tuân thủ đủ quy ước #19
5. Tuân thủ đủ 5 invariant #21
6. Export runtime OpenAPI theo document module
7. Chạy skill `contract-sync`, sinh `docs/tasks/{ID}/contract-diff.md`
8. **Dừng lại chờ Gate 1.** Chỉ viết `brief.md` sau khi contract được duyệt

## Thứ tự module thật (Phase 1)

Sample module (throwaway, đã chứng minh pipeline Bước 1) đã bị xoá — xem `docs/tasks/CLEANUP-SAMPLE.md` cho lịch sử dọn dẹp.

`Identity` → `Shop` → `Category` → `Listing` → `Search` / `Review` / `Lead`. Xem `docs/architecture/dependency-map.json` và `DesignIdeal/ai-agent-development-workflow.md` §14.
