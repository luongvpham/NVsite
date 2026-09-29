using Microsoft.EntityFrameworkCore;
using Vsite.Application.Media;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Media.Entities;
using Vsite.Infrastructure.Imaging;
using Vsite.Infrastructure.Media;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests.Media;

/// <summary>
/// MEDIA-001 D4 (#88) — adapter <see cref="ShopLogoReader"/> (port `IShopLogoReader` của Shop). Handler-level,
/// KHÔNG cần Docker (EF InMemory + fake tenant context, cùng khuôn `GetDerivativesHandlerTests`).
/// </summary>
public sealed class ShopLogoReaderTests
{
    private const string Header = LogoPresets.Header;

    private readonly FakeTenantContext _tenant = new();

    [Fact]
    public async Task Logo_set_and_derivative_exists_returns_its_StorageKey()
    {
        await using var db = CreateDb();
        var shopId = Guid.NewGuid();
        _tenant.ShopId = shopId;
        var source = NewLibrary(shopId);
        var header = NewDerived(source, Header);
        var square = NewDerived(source, "96x96,cover");
        db.MediaAssets.AddRange(source, header, square);
        await db.SaveChangesAsync();

        var key = await new ShopLogoReader(db).GetLogoStorageKeyAsync(shopId, source.Id, CancellationToken.None);

        Assert.Equal(header.StorageKey, key);
        Assert.DoesNotContain("/media/", key);
    }

    [Fact]
    public async Task Null_LogoId_returns_null()
    {
        await using var db = CreateDb();
        var shopId = Guid.NewGuid();
        _tenant.ShopId = shopId;

        Assert.Null(await new ShopLogoReader(db).GetLogoStorageKeyAsync(shopId, null, CancellationToken.None));
    }

    [Fact]
    public async Task LogoId_without_header_derivative_returns_null_and_does_not_throw()
    {
        await using var db = CreateDb();
        var shopId = Guid.NewGuid();
        _tenant.ShopId = shopId;
        var source = NewLibrary(shopId);
        db.MediaAssets.AddRange(source, NewDerived(source, "96x96,cover"));
        await db.SaveChangesAsync();

        Assert.Null(await new ShopLogoReader(db).GetLogoStorageKeyAsync(shopId, source.Id, CancellationToken.None));
        Assert.Null(await new ShopLogoReader(db).GetLogoStorageKeyAsync(shopId, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Soft_deleted_source_library_record_still_resolves()
    {
        await using var db = CreateDb();
        var shopId = Guid.NewGuid();
        _tenant.ShopId = shopId;
        var source = NewLibrary(shopId);
        var header = NewDerived(source, Header);
        db.MediaAssets.AddRange(source, header);
        await db.SaveChangesAsync();
        source.SoftDeleteFromLibrary();
        await db.SaveChangesAsync();

        var key = await new ShopLogoReader(db).GetLogoStorageKeyAsync(shopId, source.Id, CancellationToken.None);

        Assert.Equal(header.StorageKey, key);
    }

    [Fact]
    public async Task Other_shops_derivative_with_same_SourceAssetId_is_never_returned()
    {
        await using var db = CreateDb();
        var shopA = Guid.NewGuid();
        var shopB = Guid.NewGuid();
        _tenant.ShopId = shopA;
        var sourceA = NewLibrary(shopA);
        var forgedB = NewDerived(sourceA, Header, shopIdOverride: shopB);
        db.MediaAssets.AddRange(sourceA, forgedB);
        await db.SaveChangesAsync();

        var reader = new ShopLogoReader(db);

        Assert.Null(await reader.GetLogoStorageKeyAsync(shopA, sourceA.Id, CancellationToken.None));
        Assert.Equal(forgedB.StorageKey, await reader.GetLogoStorageKeyAsync(shopB, sourceA.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Soft_deleted_derivative_is_excluded()
    {
        await using var db = CreateDb();
        var shopId = Guid.NewGuid();
        _tenant.ShopId = shopId;
        var source = NewLibrary(shopId);
        var dead = NewDerived(source, Header);
        db.MediaAssets.AddRange(source, dead);
        await db.SaveChangesAsync();
        dead.SoftDeleteFromLibrary();
        await db.SaveChangesAsync();

        Assert.Null(await new ShopLogoReader(db).GetLogoStorageKeyAsync(shopId, source.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Replaced_logo_two_sources_in_same_shop_each_resolve_to_their_own_derivative()
    {
        await using var db = CreateDb();
        var shopId = Guid.NewGuid();
        _tenant.ShopId = shopId;
        var source1 = NewLibrary(shopId);
        var header1 = NewDerived(source1, Header);
        db.MediaAssets.AddRange(source1, header1);
        await db.SaveChangesAsync();
        await Task.Delay(20);
        var source2 = NewLibrary(shopId);
        var header2 = NewDerived(source2, Header);
        db.MediaAssets.AddRange(source2, header2);
        await db.SaveChangesAsync();

        var reader = new ShopLogoReader(db);

        Assert.True(header1.CreatedAt < header2.CreatedAt);
        Assert.Equal(header2.StorageKey, await reader.GetLogoStorageKeyAsync(shopId, source2.Id, CancellationToken.None));
        Assert.Equal(header1.StorageKey, await reader.GetLogoStorageKeyAsync(shopId, source1.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Resolves_regardless_of_ambient_tenant_context_because_filters_are_ignored()
    {
        await using var db = CreateDb();
        var shopId = Guid.NewGuid();
        _tenant.ShopId = shopId;
        var source = NewLibrary(shopId);
        var header = NewDerived(source, Header);
        db.MediaAssets.AddRange(source, header);
        await db.SaveChangesAsync();

        _tenant.ShopId = null;
        Assert.Equal(header.StorageKey, await new ShopLogoReader(db).GetLogoStorageKeyAsync(shopId, source.Id, CancellationToken.None));

        _tenant.ShopId = Guid.NewGuid();
        Assert.Equal(header.StorageKey, await new ShopLogoReader(db).GetLogoStorageKeyAsync(shopId, source.Id, CancellationToken.None));
    }

    [Fact]
    public void Derivative_preset_catalog_for_Shop_contains_the_header_logo_preset()
    {
        var catalog = new DerivativePresetCatalog(
            Path.Combine(FindRepoRoot(), "packages", "builder-components", "generated", "derivative-presets.json"));

        Assert.Contains(LogoPresets.Header, catalog.For("Shop"));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "pnpm-workspace.yaml")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Không tìm thấy repo root (pnpm-workspace.yaml).");
    }

    private static MediaAsset NewLibrary(Guid shopId) =>
        MediaAsset.NewLibrary(shopId, $"shops/{shopId}/{Guid.NewGuid():N}.webp", 1600, 1200, 1000, 0.5f, 0.5f, "a.jpg", null);

    private static MediaAsset NewDerived(MediaAsset source, string preset, Guid? shopIdOverride = null)
    {
        var derived = MediaAsset.NewDerived(source, $"shops/{source.ShopId}/{Guid.NewGuid():N}.webp", 320, 96, 500, preset, 0.5f, 0.5f);
        if (shopIdOverride is { } other)
        {
            derived.ShopId = other;
        }

        return derived;
    }

    private AppDbContext CreateDb() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options,
        _tenant);

    private sealed class FakeTenantContext : ITenantContext
    {
        public TenantAudienceKind AudienceKind => TenantAudienceKind.Portal;
        public Guid? ShopId { get; set; }
    }
}
