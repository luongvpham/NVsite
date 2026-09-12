using System.Text.Json.Serialization;
using Api.ExceptionHandling;
using Shared.ReservedRoutes;

var builder = WebApplication.CreateBuilder(args);

// Enum serialize dạng string toàn cục (Quyết định #19).
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// TODO(Bước 3 — Identity): builder.Services.AddOpenApi("identity", options => options.AddSchemaTransformer<ProblemDetailsSchemaTransformer>());
// Một document OpenAPI riêng cho mỗi module (backend/CLAUDE.md — "OpenAPI: thư viện và cách xuất document").
// ProblemDetailsSchemaTransformer (Api/OpenApi/ProblemDetailsSchemaTransformer.cs) bắt buộc cho MỌI
// document — error_code chỉ gắn lúc runtime qua Extensions, reflection không tự thấy được (xem ghi
// chú trong file transformer). Sample đã bị xoá (docs/tasks/CLEANUP-SAMPLE.md) — chưa có module thật
// nào đăng ký OpenAPI document; bật lại dòng trên khi Identity landing.

builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddSingleton<IReservedRoutesProvider>(_ =>
    new ReservedRoutesProvider(Path.Combine(AppContext.BaseDirectory, "config", "reserved-routes.json")));

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

// TODO(Bước 3 — Identity): app.MapOpenApi(); — cần ít nhất 1 AddOpenApi() đã đăng ký ở trên, nếu
// không WebApplication sẽ throw lúc build do document provider rỗng.

app.Run();

// Cho phép WebApplicationFactory<Program> trong integration test module thật (khi có) tham chiếu
// entry point này.
public partial class Program;
