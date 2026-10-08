using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Application.Common.Behaviors;
using Vsite.Application.Identity.Auth.Commands.Register;
using Vsite.Application.Identity.Auth.Commands.ResetPassword;
using AppValidationException = Vsite.Application.Common.Exceptions.ValidationException;

namespace Vsite.IntegrationTests;

/// <summary>
/// REFACTOR-BE-001 — `ValidationBehavior` từng có ràng buộc `where TRequest : IRequest&lt;TResponse&gt;`,
/// khiến DI âm thầm bỏ qua nó cho mọi command trả về void (`: IRequest`): validator của
/// ChangePassword/ResetPassword/ForgotPassword không bao giờ chạy, `MinimumLength(8)` bị lách.
///
/// Test duyệt MỌI request trong assembly Application có validator đăng ký, khẳng định pipeline
/// thật (DI của host thật) có `ValidationBehavior` cho đúng cặp `(TRequest, TResponse)` — thêm
/// command mới có validator mà behavior không chạy là RED ngay (#17).
///
/// Không cần Docker: chỉ build host với connection string giả, không gửi request nào chạm DB.
/// </summary>
public sealed class ValidationPipelineCoverageTests
{
    [Fact]
    public void Every_request_with_a_validator_runs_through_ValidationBehavior()
    {
        using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();

        var requestTypes = typeof(RegisterCommand).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IBaseRequest).IsAssignableFrom(t))
            .ToList();

        var checkedCount = 0;
        var violations = new List<string>();
        foreach (var requestType in requestTypes)
        {
            var validators = scope.ServiceProvider.GetServices(typeof(IValidator<>).MakeGenericType(requestType));
            if (!validators.Any())
            {
                continue;
            }

            var responseType = ResponseTypeOf(requestType);
            var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);
            var expected = typeof(ValidationBehavior<,>).MakeGenericType(requestType, responseType);

            checkedCount++;
            if (!scope.ServiceProvider.GetServices(behaviorType).Any(b => b?.GetType() == expected))
            {
                violations.Add(requestType.Name);
            }
        }

        // Chống test "xanh vì không duyệt gì" — hiện có 16 validator.
        Assert.True(checkedCount >= 3, $"Chỉ tìm thấy {checkedCount} request có validator — reflection hỏng?");
        Assert.True(
            violations.Count == 0,
            "Request có validator nhưng ValidationBehavior KHÔNG nằm trong pipeline: " + string.Join(", ", violations));
    }

    [Fact]
    public async Task Void_command_with_invalid_input_is_rejected_before_handler()
    {
        using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var behaviors = scope.ServiceProvider.GetServices<IPipelineBehavior<ResetPasswordCommand, Unit>>();
        var validation = Assert.Single(behaviors.OfType<ValidationBehavior<ResetPasswordCommand, Unit>>());

        var handlerCalled = false;
        await Assert.ThrowsAsync<AppValidationException>(() => validation.Handle(
            new ResetPasswordCommand("token", "short"),
            () =>
            {
                handlerCalled = true;
                return Task.FromResult(Unit.Value);
            },
            CancellationToken.None));

        Assert.False(handlerCalled);
    }

    /// <summary>REFACTOR-BE-001 — `AppDbContext` nhận `IAuditActor?` là tham số optional: quên đăng ký
    /// ở `Program.cs` thì audit âm thầm về NULL (cùng kiểu lỗi im lặng với ValidationBehavior).
    /// Khẳng định DI thật đưa đúng actor vào context.</summary>
    [Fact]
    public void Real_host_injects_IAuditActor_into_AppDbContext()
    {
        using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Vsite.Infrastructure.Persistence.AppDbContext>();

        // Primary-constructor parameter được compiler lưu thành field ẩn tên chứa "auditActor".
        var field = typeof(Vsite.Infrastructure.Persistence.AppDbContext)
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single(f => f.Name.Contains("auditActor", StringComparison.Ordinal));

        Assert.IsType<Vsite.Api.Auth.HttpAuditActor>(field.GetValue(db));
    }

    private static Type ResponseTypeOf(Type requestType)
    {
        var generic = requestType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));
        return generic?.GetGenericArguments()[0] ?? typeof(Unit);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Giá trị giả — không request nào chạm DB/Redis (cùng khuôn ShopScopedRouteFilterTests).
                    ["ConnectionStrings:Identity"] = "Host=localhost;Port=1;Database=fake;Username=fake;Password=fake",
                    ["ConnectionStrings:Redis"] = "localhost:1",
                    ["Jwt:SigningKey"] = "test-signing-key-not-for-production-use-32-chars-min",
                    ["Jwt:Issuer"] = "vsite-test",
                    ["Jwt:AccessTokenLifetimeMinutes"] = "15",
                    ["Auth:ApiBaseUrl"] = "http://localhost",
                });
            });
        });
}
