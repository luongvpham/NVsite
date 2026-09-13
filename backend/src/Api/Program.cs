using System.Text.Json.Serialization;
using Api.ExceptionHandling;
using Api.OpenApi;
using Api.Tenancy;
using Identity.Api;
using Shared.Domain.Abstractions;
using Shared.ReservedRoutes;

var builder = WebApplication.CreateBuilder(args);

// Enum serialize dạng string toàn cục (Quyết định #19).
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Một document OpenAPI riêng cho mỗi module (backend/CLAUDE.md — "OpenAPI: thư viện và cách xuất document").
// ProblemDetailsSchemaTransformer bắt buộc cho MỌI document — error_code chỉ gắn lúc runtime qua
// Extensions, reflection không tự thấy được (xem ghi chú trong file transformer).
builder.Services.AddOpenApi("identity", options => options.AddSchemaTransformer<ProblemDetailsSchemaTransformer>());

builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddExceptionHandler<UnauthorizedAccessExceptionHandler>();
builder.Services.AddProblemDetails();

// Mặc định ASP.NET Core trả 401/403 không có body khi authorization policy fail — thay bằng
// ProblemDetails có error_code (Quyết định #19), dùng chung mọi module.
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, ProblemDetailsAuthorizationMiddlewareResultHandler>();

builder.Services.AddSingleton<IReservedRoutesProvider>(_ =>
    new ReservedRoutesProvider(Path.Combine(AppContext.BaseDirectory, "config", "reserved-routes.json")));

// Quyết định #7 (thu hẹp — xem TenantResolutionMiddleware). Scoped: một TenantContext per-request,
// middleware set giá trị, mọi handler đọc qua ITenantContext (không handler nào tự resolve ShopId).
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

builder.Services.AddIdentityModule(builder.Configuration);

// JWT Bearer validation + policy RequireGlobalScope (Quyết định #32) — wiring thật sống ở
// Identity.Api vì cần JwtOptions (Identity.Infrastructure); root Api chỉ gọi đúng method này.
builder.Services.AddIdentityAuthentication();

// Dev only — apps/web (TanStack Start, :3000) và apps/portal (Vite CSR, :5173) gọi API
// từ browser sau khi hydrate, khác port = khác origin. Production dùng domain thật sau Caddy,
// chính sách CORS thật (nếu cần) sẽ chốt cùng lúc với Quyết định #7/#9.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("LocalDev", policy =>
            policy.WithOrigins("http://localhost:3000", "http://localhost:5173").AllowAnyHeader().AllowAnyMethod());
    });
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors("LocalDev");
}

app.UseExceptionHandler();

// Sau CORS (preflight OPTIONS không cần resolve tenant), TRƯỚC mọi endpoint — mọi handler đọc
// ShopId/audience qua ITenantContext, không handler nào tự parse Host/route.
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthentication();

// Quyết định #21.5/#31 — JWT hợp lệ chữ ký/hạn dùng KHÔNG đồng nghĩa quyền còn hiệu lực. Chạy
// SAU UseAuthentication (cần ClaimsPrincipal), TRƯỚC UseAuthorization (chặn sớm nếu membership đã
// bị thu hồi hoặc token dùng sai domain, trước khi tới bước kiểm policy theo endpoint).
app.UseMiddleware<ShopMembershipValidationMiddleware>();

app.UseAuthorization();

app.MapOpenApi();

app.MapIdentityEndpoints();

app.Run();

// Cho phép WebApplicationFactory<Program> trong integration test module thật (khi có) tham chiếu
// entry point này.
public partial class Program;
