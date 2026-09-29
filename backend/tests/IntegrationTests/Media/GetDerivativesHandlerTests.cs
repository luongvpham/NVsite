using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media.Queries.GetDerivatives;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Media.Entities;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests.Media;

/// <summary>
/// MEDIA-001 D3 — `GET /shops/{shopId}/media/library/{assetId}/derivatives?preset=`. Handler-level,
/// KHÔNG cần Docker (EF InMemory + fake tenant context, cùng khuôn `LibraryHandlerTests`).
/// Endpoint-level (Docker) ở `GetDerivativesEndpointTests`.
/// </summary>
public sealed class GetDerivativesHandlerTests
{
    private const string LogoInside = "320x96,inside";
    private const string LogoSquare = "96x96,cover";

    private readonly FakeTenantContext _tenant = new();

    [Fact]
    public async Task Returns_only_derivatives_of_that_source_ordered_by_preset_then_CreatedAt()
    {
        await using var db = CreateDb();
        var shopId = Guid.NewGuid();
        _tenant.ShopId = shopId;
        var source = NewLibrary(shopId);
        var other = NewLibrary(shopId);
        var square = NewDerived(source, LogoSquare);
        var inside = NewDerived(source, LogoInside);
        var otherDerived = NewDerived(other, LogoInside);
        db.MediaAssets.AddRange(source, other, square, inside, otherDerived);
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(new GetDerivativesQuery(shopId, source.Id, null), CancellationToken.None);

        Assert.Equal(new[] { inside.Id, square.Id }, result.Select(d => d.Id).ToArray());
        Assert.All(result, d => Assert.Equal(source.Id, d.SourceAssetId));
        Assert.All(result, d => Assert.False(d.IsInLibrary));
    }

    [Fact]
    public async Task Preset_filter_returns_exactly_the_matching_one_including_comma_value()
    {
        await using var db = CreateDb();
        var shopId = Guid.NewGuid();
        _tenant.ShopId = shopId;
        var source = NewLibrary(shopId);
        var square = NewDerived(source, LogoSquare);
        var inside = NewDerived(source, LogoInside);
        db.MediaAssets.AddRange(source, square, inside);
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(new GetDerivativesQuery(shopId, source.Id, LogoInside), CancellationToken.None);

        var only = Assert.Single(result);
        Assert.Equal(inside.Id, only.Id);
        Assert.Equal(LogoInside, only.Preset);
    }

    [Fact]
    public async Task Derivatives_still_resolve_after_source_library_record_is_soft_deleted()
    {
        await using var db = CreateDb();
        var shopId = Guid.NewGuid();
        _tenant.ShopId = shopId;
        var source = NewLibrary(shopId);
        var inside = NewDerived(source, LogoInside);
        db.MediaAssets.AddRange(source, inside);
        await db.SaveChangesAsync();
        source.SoftDeleteFromLibrary();
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(new GetDerivativesQuery(shopId, source.Id, LogoInside), CancellationToken.None);

        Assert.Equal(inside.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task Other_shops_derivatives_never_appear_even_with_the_same_source_id()
    {
        await using var db = CreateDb();
        var shopA = Guid.NewGuid();
        var shopB = Guid.NewGuid();
        _tenant.ShopId = shopA;
        var sourceA = NewLibrary(shopA);
        var derivedA = NewDerived(sourceA, LogoInside);
        // Shop B: bản derivative cố tình trỏ SourceAssetId = id của source shop A (id đoán được).
        var forgedB = NewDerived(sourceA, LogoInside, shopIdOverride: shopB);
        db.MediaAssets.AddRange(sourceA, derivedA, forgedB);
        await db.SaveChangesAsync();

        var asA = await Handler(db).Handle(new GetDerivativesQuery(shopA, sourceA.Id, null), CancellationToken.None);
        var asB = await Handler(db).Handle(new GetDerivativesQuery(shopB, sourceA.Id, null), CancellationToken.None);
        var stranger = await Handler(db).Handle(new GetDerivativesQuery(Guid.NewGuid(), sourceA.Id, null), CancellationToken.None);

        Assert.Equal(derivedA.Id, Assert.Single(asA).Id);
        Assert.Equal(forgedB.Id, Assert.Single(asB).Id);
        Assert.Empty(stranger);
    }

    [Fact]
    public async Task Unknown_id_returns_empty_list()
    {
        await using var db = CreateDb();
        var shopId = Guid.NewGuid();
        _tenant.ShopId = shopId;

        var result = await Handler(db).Handle(new GetDerivativesQuery(shopId, Guid.NewGuid(), null), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Soft_deleted_derivative_is_excluded()
    {
        await using var db = CreateDb();
        var shopId = Guid.NewGuid();
        _tenant.ShopId = shopId;
        var source = NewLibrary(shopId);
        var alive = NewDerived(source, LogoInside);
        var dead = NewDerived(source, LogoSquare);
        db.MediaAssets.AddRange(source, alive, dead);
        await db.SaveChangesAsync();
        dead.SoftDeleteFromLibrary();
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(new GetDerivativesQuery(shopId, source.Id, null), CancellationToken.None);

        Assert.Equal(alive.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task Omitted_preset_returns_all_derivatives()
    {
        await using var db = CreateDb();
        var shopId = Guid.NewGuid();
        _tenant.ShopId = shopId;
        var source = NewLibrary(shopId);
        db.MediaAssets.AddRange(source, NewDerived(source, LogoInside), NewDerived(source, LogoSquare), NewDerived(source, "800x600,cover"));
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(new GetDerivativesQuery(shopId, source.Id, null), CancellationToken.None);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Validator_rejects_preset_longer_than_40_chars_and_accepts_unknown_names()
    {
        var validator = new GetDerivativesValidator();

        Assert.False(validator.Validate(new GetDerivativesQuery(Guid.NewGuid(), Guid.NewGuid(), new string('x', 41))).IsValid);
        Assert.True(validator.Validate(new GetDerivativesQuery(Guid.NewGuid(), Guid.NewGuid(), new string('x', 40))).IsValid);
        Assert.True(validator.Validate(new GetDerivativesQuery(Guid.NewGuid(), Guid.NewGuid(), "no-such-preset")).IsValid);
        Assert.True(validator.Validate(new GetDerivativesQuery(Guid.NewGuid(), Guid.NewGuid(), null)).IsValid);
    }

    private static GetDerivativesHandler Handler(IAppDbContext db) => new(db);

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
