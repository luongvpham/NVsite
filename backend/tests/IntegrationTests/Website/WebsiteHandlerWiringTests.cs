using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Application.Website.Commands.CreateWebsite;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// Handler Website chưa có endpoint (T10 ở đợt sau) nên không request HTTP nào chạm tới chúng; test này khoá phần wiring DI của host thật:
/// mọi request trong namespace <c>Vsite.Application.Website</c> phải resolve được handler (đủ service ở Program.cs). Quên đăng ký
/// <c>SlugRules</c>/<c>WebsiteSeeder</c>/… là đỏ ở đây chứ không đợi tới lúc có endpoint. Không cần Docker.
/// </summary>
public sealed class WebsiteHandlerWiringTests
{
    [Fact]
    public void Every_website_request_resolves_a_handler_from_the_real_host()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Identity"] = "Host=localhost;Port=1;Database=fake;Username=fake;Password=fake",
                ["ConnectionStrings:Redis"] = "localhost:1",
                ["Jwt:SigningKey"] = "test-signing-key-not-for-production-use-32-chars-min",
                ["Jwt:Issuer"] = "vsite-test",
                ["Jwt:AccessTokenLifetimeMinutes"] = "15",
                ["Auth:ApiBaseUrl"] = "http://localhost",
            }));
        });
        using var scope = factory.Services.CreateScope();

        var requests = typeof(CreateWebsiteCommand).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                && t.Namespace?.StartsWith("Vsite.Application.Website", StringComparison.Ordinal) == true
                && typeof(IBaseRequest).IsAssignableFrom(t))
            .ToList();

        Assert.True(requests.Count >= 10, $"Chỉ tìm thấy {requests.Count} request Website — reflection hỏng?");
        foreach (var request in requests)
        {
            var response = request.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
                ?.GetGenericArguments()[0];
            var handlerType = response is null
                ? typeof(IRequestHandler<>).MakeGenericType(request)
                : typeof(IRequestHandler<,>).MakeGenericType(request, response);

            Assert.NotNull(scope.ServiceProvider.GetService(handlerType));
        }
    }
}
