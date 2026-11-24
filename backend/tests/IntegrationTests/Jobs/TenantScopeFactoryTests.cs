using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Vsite.Api.Tenancy;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Shop.Interfaces;
using Vsite.Domain.Abstractions;
using Vsite.Domain.ReservedRoutes;

namespace Vsite.IntegrationTests.Jobs;

/// <summary>#116 — tenant scope ngoài request. Không cần Docker.</summary>
public sealed class TenantScopeFactoryTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddSingleton<ITenantScopeFactory, TenantScopeFactory>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task New_scope_has_the_requested_shop_tenant()
    {
        await using var provider = BuildProvider();
        var shopId = Guid.NewGuid();

        await using var scope = provider.GetRequiredService<ITenantScopeFactory>().BeginShopScope(shopId);

        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        Assert.Equal(TenantAudienceKind.Shop, tenant.AudienceKind);
        Assert.Equal(shopId, tenant.ShopId);
    }

    [Fact]
    public void Setting_tenant_twice_throws_and_keeps_the_old_value()
    {
        var tenant = new TenantContext();
        var first = Guid.NewGuid();
        tenant.Set(TenantAudienceKind.Shop, first);

        Assert.Throws<InvalidOperationException>(() => tenant.Set(TenantAudienceKind.Shop, Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => tenant.Set(TenantAudienceKind.Main, null));
        Assert.Throws<InvalidOperationException>(() => tenant.AttachShop(Guid.NewGuid()));

        Assert.Equal(TenantAudienceKind.Shop, tenant.AudienceKind);
        Assert.Equal(first, tenant.ShopId);
    }

    [Fact]
    public async Task Two_parallel_scopes_do_not_mix_shop_ids()
    {
        await using var provider = BuildProvider();
        var factory = provider.GetRequiredService<ITenantScopeFactory>();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        await using var scopeA = factory.BeginShopScope(a);
        await using var scopeB = factory.BeginShopScope(b);

        Assert.Equal(a, scopeA.ServiceProvider.GetRequiredService<ITenantContext>().ShopId);
        Assert.Equal(b, scopeB.ServiceProvider.GetRequiredService<ITenantContext>().ShopId);
    }

    [Fact]
    public async Task Scope_cannot_be_started_for_an_empty_shop_id()
    {
        await using var provider = BuildProvider();

        Assert.Throws<ArgumentException>(() => provider.GetRequiredService<ITenantScopeFactory>().BeginShopScope(Guid.Empty));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Request_middleware_still_sets_tenant_once_per_request()
    {
        var shopId = Guid.NewGuid();
        var tenant = new TenantContext();
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var http = new DefaultHttpContext();
        http.Request.Host = new HostString("spa-abc.vsite.local", 5270);

        await middleware.InvokeAsync(http, tenant, new FakeShopLookup(shopId), new ReservedRoutesProvider());

        Assert.True(nextCalled);
        Assert.Equal(TenantAudienceKind.Shop, tenant.AudienceKind);
        Assert.Equal(shopId, tenant.ShopId);
        // request thứ hai trên cùng scope = lỗi lập trình, không được ghi đè.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(http, tenant, new FakeShopLookup(Guid.NewGuid()), new ReservedRoutesProvider()));
        Assert.Equal(shopId, tenant.ShopId);
    }

    private sealed class FakeShopLookup(Guid shopId) : IShopLookupService
    {
        public Task<Guid?> FindShopIdBySlugAsync(string slug, CancellationToken ct) => Task.FromResult<Guid?>(shopId);
        public Task InvalidateAsync(string slug, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class ReservedRoutesProvider : IReservedRoutesProvider
    {
        public ReservedRoutes Routes { get; } = new([], ["api", "www"]);
    }
}
