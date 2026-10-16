using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Routing;
using Vsite.Api.Auth;
using Vsite.Api.Identity;
using Vsite.Api.ExceptionHandling;
using Vsite.Api.Media;
using Vsite.Api.OpenApi;
using Vsite.Api.Shop;
using Vsite.Api.Tenancy;
using Vsite.Application.Common.Imaging;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Website.Interfaces;
using Vsite.Application.Website.Options;
using Vsite.Application.Website.Seeding;
using Vsite.Application.Website.Validation;
using Vsite.Application.Website.Versioning;
using Vsite.Domain.Abstractions;
using Vsite.Domain.ReservedRoutes;
using Vsite.Infrastructure;
using Vsite.Infrastructure.Configuration;
using Vsite.Infrastructure.Imaging;
using Vsite.Infrastructure.Website;

var builder = WebApplication.CreateBuilder(args);

// Enum serialize dạng string toàn cục (Quyết định #19).
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Một document OpenAPI riêng cho mỗi module (backend/CLAUDE.md — "OpenAPI: thư viện và cách xuất document").
// ProblemDetailsSchemaTransformer bắt buộc cho MỌI document — error_code chỉ gắn lúc runtime qua
// Extensions, reflection không tự thấy được (xem ghi chú trong file transformer).
// DuplicateNullableSchemaOccurrenceTagger + DuplicateNullableSchemaDocumentTransformer cũng bắt
// buộc cho MỌI document, và LUÔN đăng ký cùng nhau — Microsoft.AspNetCore.OpenApi tự sinh schema
// trùng lặp hậu tố số (XDto2, …) khi cùng CLR type vừa nullable vừa non-nullable (MEDIA-001:
// MediaAssetDto2), Orval không tự gộp lại (xem ghi chú trong file transformer, gồm cả lý do một
// document transformer đơn lẻ KHÔNG đủ để fix việc này).
builder.Services.AddOpenApi("identity", options => options
    .AddSchemaTransformer<ProblemDetailsSchemaTransformer>()
    .AddSchemaTransformer<DuplicateNullableSchemaOccurrenceTagger>()
    .AddDocumentTransformer<DuplicateNullableSchemaDocumentTransformer>());
builder.Services.AddOpenApi("shop", options => options
    .AddSchemaTransformer<ProblemDetailsSchemaTransformer>()
    .AddSchemaTransformer<DuplicateNullableSchemaOccurrenceTagger>()
    .AddDocumentTransformer<DuplicateNullableSchemaDocumentTransformer>());
builder.Services.AddOpenApi("media", options => options
    .AddSchemaTransformer<ProblemDetailsSchemaTransformer>()
    .AddSchemaTransformer<DuplicateNullableSchemaOccurrenceTagger>()
    .AddDocumentTransformer<DuplicateNullableSchemaDocumentTransformer>());

builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddExceptionHandler<UnauthorizedAccessExceptionHandler>();
builder.Services.AddExceptionHandler<BadHttpRequestExceptionHandler>();
builder.Services.AddProblemDetails();

// Mặc định ASP.NET Core trả 401/403 không có body khi authorization policy fail — thay bằng
// ProblemDetails có error_code (Quyết định #19), dùng chung mọi module.
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, ProblemDetailsAuthorizationMiddlewareResultHandler>();

builder.Services.AddSingleton<IReservedRoutesProvider>(_ =>
    new ReservedRoutesProvider(Path.Combine(AppContext.BaseDirectory, "config", "reserved-routes.json")));

// Preset catalog ảnh (T3, MEDIA-001, Quyết định #78/#86) — cùng pattern link + đọc một lần lúc
// startup như IReservedRoutesProvider ở trên. Resolve eager ngay dưới `builder.Build()` để fail
// fast (file thiếu/sai shape thì app KHÔNG khởi động), không đợi tới request đầu tiên.
builder.Services.AddSingleton<IImagePresetCatalog>(_ =>
    new ImagePresetCatalog(Path.Combine(AppContext.BaseDirectory, "config", "image-presets.json")));
builder.Services.AddSingleton<IDerivativePresetCatalog>(_ =>
    new DerivativePresetCatalog(Path.Combine(AppContext.BaseDirectory, "generated", "derivative-presets.json")));

// Module Website (T1, WEBSITE-001, #97/#98) — config dùng chung FE+BE + artifact codegen, cùng pattern
// link + đọc một lần. Resolve eager ngay dưới `builder.Build()`: thiếu/hỏng file nào thì app KHÔNG lên.
var websiteConfigDir = Path.Combine(AppContext.BaseDirectory, "config");
var websiteArtifactDir = Path.Combine(AppContext.BaseDirectory, "generated");
builder.Services.AddSingleton<ILocaleCatalog>(_ => new LocaleCatalog(Path.Combine(websiteConfigDir, "locales.json")));
builder.Services.AddSingleton<IThemeTokenCatalog>(_ => new ThemeTokenCatalog(Path.Combine(websiteConfigDir, "theme-tokens.json")));
builder.Services.AddSingleton<ISystemPageCatalog>(_ => new SystemPageCatalog(Path.Combine(websiteConfigDir, "system-pages.json")));
builder.Services.AddSingleton<ISanitizeProfileCatalog>(_ => new SanitizeProfileCatalog(Path.Combine(websiteConfigDir, "sanitize-profiles.json")));
builder.Services.AddSingleton<IBindingSourceCatalog>(_ => new BindingSourceCatalog(Path.Combine(websiteConfigDir, "binding-sources.json")));
builder.Services.AddSingleton<ITreeRulesCatalog>(_ => new TreeRulesCatalog(Path.Combine(websiteArtifactDir, "tree-rules.json")));
builder.Services.AddSingleton<IWebsiteSchemaProvider>(_ => new WebsiteSchemaProvider(
    Path.Combine(websiteArtifactDir, "tree-envelope.schema.json"),
    Path.Combine(websiteArtifactDir, "theme-tokens.schema.json"),
    Path.Combine(websiteArtifactDir, "props-schemas.json")));

// Validate cây (T4/T5, WEBSITE-001): giới hạn cứng từ appsettings (G6), sanitizer richText, khung upcaster (#94).
// Chưa có IDocumentUpcaster nào (phiên bản hiện hành = 1) — thêm bước nâng = AddSingleton<IDocumentUpcaster, ...>.
builder.Services.AddSingleton(builder.Configuration.GetSection(PageTreeLimits.SectionName).Get<PageTreeLimits>() ?? new PageTreeLimits());
builder.Services.AddSingleton<IRichTextSanitizer, RichTextSanitizer>();
builder.Services.AddSingleton(sp => new DocumentVersioning(sp.GetServices<IDocumentUpcaster>()));
builder.Services.AddSingleton<PageTreeValidator>();

// Handler Website (T6/T8): seed trang, luật slug, validator tokens — singleton vì chỉ giữ catalog/validator bất biến.
builder.Services.AddSingleton<ThemeTokensValidator>();
builder.Services.AddSingleton<WebsiteSeeder>();
builder.Services.AddSingleton<SlugRules>();

// Quyết định #7 (thu hẹp — xem TenantResolutionMiddleware). Scoped: một TenantContext per-request,
// middleware set giá trị, mọi handler đọc qua ITenantContext (không handler nào tự resolve ShopId).
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

// Toàn bộ wiring Infrastructure (DbContext, Redis, MediatR, service từng module) nằm sau MỘT
// method này — Program.cs không tự đăng ký implementation nào (architecture-guide.md §1).
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddScoped<IAuditActor, HttpAuditActor>();

// JWT Bearer validation + policy RequireGlobalScope (Quyết định #32).
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

// Fail fast (T3, MEDIA-001): buộc resolve ngay lúc startup thay vì đợi request/handler đầu tiên
// chạm tới — file config sai shape hoặc thiếu thì app dừng ở đây, không chạy tiếp với catalog rỗng.
app.Services.GetRequiredService<IImagePresetCatalog>();
app.Services.GetRequiredService<IDerivativePresetCatalog>();
app.Services.GetRequiredService<IReservedRoutesProvider>();
app.Services.GetRequiredService<ILocaleCatalog>();
app.Services.GetRequiredService<IThemeTokenCatalog>();
app.Services.GetRequiredService<ISystemPageCatalog>();
app.Services.GetRequiredService<ISanitizeProfileCatalog>();
app.Services.GetRequiredService<IBindingSourceCatalog>();
app.Services.GetRequiredService<ITreeRulesCatalog>();
app.Services.GetRequiredService<IWebsiteSchemaProvider>();
WebsiteConfigConsistency.EnsureEveryTypeHasPropsSchema(
    app.Services.GetRequiredService<ITreeRulesCatalog>(), app.Services.GetRequiredService<IWebsiteSchemaProvider>());
app.Services.GetRequiredService<PageTreeValidator>();

if (app.Environment.IsDevelopment())
{
    app.UseCors("LocalDev");
}

app.UseExceptionHandler();

// Xem doc trên UnsupportedMediaTypeStatusCodeHandler — routing tự trả 415 rỗng body cho endpoint
// multipart khi Content-Type không khớp, TRƯỚC khi endpoint chạy, nên UseExceptionHandler (cần
// exception) không bắt được. Đặt sớm, cùng khu vực UseExceptionHandler theo đúng khuyến nghị của
// ASP.NET Core cho UseStatusCodePages (early trong pipeline, trước UseRouting/mapping).
//
// MultipartRouteMatcher nhận thẳng app.DataSources (collection sống, chưa có endpoint nào lúc này —
// mọi Map*Endpoints() còn ở phía dưới) — xem doc trên class đó vì sao an toàn nhờ Lazy<T>.
var multipartRouteMatcher = new MultipartRouteMatcher(((IEndpointRouteBuilder)app).DataSources);
app.UseStatusCodePages(context => UnsupportedMediaTypeStatusCodeHandler.HandleAsync(context, multipartRouteMatcher));

// T9, MEDIA-001 (#53, #81, #83) — phục vụ /media/* TRÊN MỌI HOST, TRƯỚC TenantResolutionMiddleware:
// không tra Redis, không kiểm tenant, không auth (đọc ảnh luôn public). Map() branch off request
// khớp prefix "/media" khỏi pipeline chính — request đó không bao giờ chạm TenantResolutionMiddleware
// / Authentication / Authorization phía dưới. Vẫn nằm SAU UseExceptionHandler (phòng thủ thêm dù
// MediaFileMiddleware tự bắt hết ArgumentException, không để lọt request nào ra ngoài thành 500).
app.Map(Vsite.Application.Common.Imaging.ImagePaths.MediaPathPrefix, branch => branch.UseMiddleware<MediaFileMiddleware>());

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

// REFACTOR-API-001 — mọi endpoint API dưới /api (xem ApiRoutes). /media/* (file ảnh) và /openapi/*
// cố ý KHÔNG nằm trong group này.
var api = app.MapGroup(Vsite.Application.Common.ApiRoutes.Prefix);
api.MapIdentityEndpoints();
api.MapShopEndpoints();
api.MapMediaEndpoints();

app.Run();

// Cho phép WebApplicationFactory<Program> trong integration test module thật (khi có) tham chiếu
// entry point này.
public partial class Program;
