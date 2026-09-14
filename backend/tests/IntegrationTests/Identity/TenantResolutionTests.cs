using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Api.Identity;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Identity.Enums;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests;

/// <summary>
/// Thay thế phần verify thủ công của DOCKER-TEST-DEBT mục 1 (sửa hosts file + `dotnet run` +
/// curl tay) — dùng chính <see cref="TestServer"/> của <see cref="IdentityApiFactory"/> nên
/// Host header không cần khớp DNS thật. Collection RIÊNG (không dùng chung
/// <see cref="IdentityApiCollection"/> của <see cref="TokenScopeTests"/>).
/// </summary>
[Collection(TenantResolutionCollection.Name)]
public sealed class TenantResolutionTests
{
    private const string Password = "Password123!";

    private readonly IdentityApiFactory _factory;
    private readonly HttpClient _client;

    public TenantResolutionTests(IdentityApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_on_shop_host_assigns_matching_ShopId()
    {
        var (shopId, _, host) = await CreateShopAsync();
        var email = $"shop-{Guid.NewGuid():N}@example.test";

        await RegisterAsync(host, email);

        Assert.Equal(shopId, await GetPendingShopIdAsync(email));
    }

    [Fact]
    public async Task Register_on_main_host_has_null_ShopId()
    {
        // Không có label nào khớp Shop.Slug -> context vsite-main (03 §5, "null = vsite.vn").
        var email = $"main-{Guid.NewGuid():N}@example.test";

        await RegisterAsync(host: null, email);

        Assert.Null(await GetPendingShopIdAsync(email));
    }

    private async Task<(Guid ShopId, string Slug, string Host)> CreateShopAsync()
    {
        var shopId = Guid.NewGuid();
        var slug = $"tenantres-{shopId:N}";

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Shops.Add(new Shop(shopId) { Name = "Tenant Resolution Test Shop", Slug = slug, Kind = ShopKind.Hosted, Status = ShopStatus.Active });
        await db.SaveChangesAsync();

        return (shopId, slug, $"{slug}.vsite.local");
    }

    private async Task RegisterAsync(string? host, string email)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/register")
        {
            Content = JsonContent.Create(new RegisterRequest(email, Password, "Tenant Resolution Test")),
        };
        if (host is not null)
        {
            request.Headers.Host = host;
        }

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<Guid?> GetPendingShopIdAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var normalized = email.Trim().ToUpperInvariant();
        return await db.PendingRegistrations
            .Where(p => p.EmailNormalized == normalized)
            .Select(p => p.ShopId)
            .SingleAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class TenantResolutionCollection : ICollectionFixture<IdentityApiFactory>
{
    public const string Name = "TenantResolution";
}
