# backend — vsite

.NET 9, Modular Monolith, Clean Architecture + DDD Lite. Chi tiết đầy đủ ở `DesignIdeal/02-tech-stack-and-decision.md` §1, §Quyết định #1.

## Cấu trúc module

```
backend/src/
  Api/                        ← host, middleware, DI, OpenAPI document setup
  Shared/                     ← primitives dùng chung, KHÔNG chứa logic module
  Modules/{ModuleName}/
    {ModuleName}.Domain/      ← không reference EF Core, MediatR, ASP.NET, hay project module khác
    {ModuleName}.Application/
    {ModuleName}.Infrastructure/
    {ModuleName}.Api/
backend/tests/
  ArchitectureTests/
  {ModuleName}.IntegrationTests/
```

## Ranh giới module (Quyết định #1)

- Module **không được** reference project của module khác.
- Cross-module giao tiếp qua **Integration Event** hoặc **Public Contract interface**, không qua reference trực tiếp.
- Chiều phụ thuộc trong một module: `Domain ← Application ← Infrastructure ← Api`, không đảo.
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
