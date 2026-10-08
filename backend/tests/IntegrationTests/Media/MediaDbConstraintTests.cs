using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vsite.Domain.Media.Entities;
using Vsite.Domain.Shop.Entities;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests;

/// <summary>
/// `08-media-asset-design.md` §2.1 — ràng buộc phải nằm ở DB (Quyết định #17). Mỗi test dưới đây
/// phải FAIL THẬT khi bị vi phạm, không chỉ là tài liệu (task T4, MEDIA-001).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class MediaDbConstraintTests(PostgresFixture postgres)
{
    /// <summary>[test bắt buộc 11] `ck_media_library_preset` — chiều `IsInLibrary=false, Preset=NULL`.</summary>
    [Fact]
    public async Task Check_constraint_rejects_non_library_asset_without_preset()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var shop = NewShop();
        db.Shops.Add(shop);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "MediaAsset"
                ("Id", "ShopId", "StorageKey", "MimeType", "Width", "Height", "SizeBytes",
                 "FocalPointX", "FocalPointY", "IsInLibrary", "Preset", "SourceAssetId",
                 "CreatedAt", "IsDeleted")
            VALUES
                ({Guid.NewGuid()}, {shop.Id}, {$"shops/{shop.Id}/direct/{Guid.NewGuid():N}.webp"}, 'image/webp', 100, 100, 10,
                 0.5, 0.5, false, NULL, NULL,
                 now(), false)
            """));

        Assert.Equal("ck_media_library_preset", ex.ConstraintName);
    }

    /// <summary>Chiều ngược lại — `IsInLibrary=true` nhưng có `Preset`.</summary>
    [Fact]
    public async Task Check_constraint_rejects_library_asset_with_preset()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var shop = NewShop();
        db.Shops.Add(shop);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "MediaAsset"
                ("Id", "ShopId", "StorageKey", "MimeType", "Width", "Height", "SizeBytes",
                 "FocalPointX", "FocalPointY", "IsInLibrary", "Preset", "SourceAssetId",
                 "CreatedAt", "IsDeleted")
            VALUES
                ({Guid.NewGuid()}, {shop.Id}, {$"shops/{shop.Id}/library/{Guid.NewGuid():N}.webp"}, 'image/webp', 100, 100, 10,
                 0.5, 0.5, true, {"96x96,cover"}, NULL,
                 now(), false)
            """));

        Assert.Equal("ck_media_library_preset", ex.ConstraintName);
    }

    [Fact]
    public async Task Unique_index_rejects_duplicate_StorageKey()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var shop = NewShop();
        db.Shops.Add(shop);
        await db.SaveChangesAsync();

        var key = $"shops/{shop.Id}/library/{Guid.NewGuid():N}.webp";
        db.MediaAssets.Add(MediaAsset.NewLibrary(shop.Id, key, 100, 100, 10, 0.5f, 0.5f, null, null));
        await db.SaveChangesAsync();

        db.MediaAssets.Add(MediaAsset.NewLibrary(shop.Id, key, 100, 100, 10, 0.5f, 0.5f, null, null));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    /// <summary>[test bắt buộc 8] FK ghép `Shop(LogoId, Id) → MediaAsset(Id, ShopId)` — gán logo
    /// trỏ tới asset của shop KHÁC phải bị chặn ở tầng DB.</summary>
    [Fact]
    public async Task Composite_FK_rejects_LogoId_pointing_to_another_shops_asset()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var shopA = NewShop();
        var shopB = NewShop();
        db.Shops.AddRange(shopA, shopB);
        await db.SaveChangesAsync();

        var assetOfB = MediaAsset.NewLibrary(
            shopB.Id, $"shops/{shopB.Id}/library/{Guid.NewGuid():N}.webp", 100, 100, 10, 0.5f, 0.5f, null, null);
        db.MediaAssets.Add(assetOfB);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "Shop" SET "LogoId" = {assetOfB.Id} WHERE "Id" = {shopA.Id}"""));

        Assert.Equal("FK_Shop_MediaAsset_LogoId_Id", ex.ConstraintName);
    }

    /// <summary>Xoá cứng bản Library → `SourceAssetId` của clone thành NULL (`ON DELETE SET NULL`).</summary>
    [Fact]
    public async Task Hard_delete_of_library_asset_sets_clone_SourceAssetId_to_null()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var shop = NewShop();
        db.Shops.Add(shop);
        await db.SaveChangesAsync();

        var library = MediaAsset.NewLibrary(
            shop.Id, $"shops/{shop.Id}/library/{Guid.NewGuid():N}.webp", 1200, 800, 12345, 0.5f, 0.5f, null, null);
        db.MediaAssets.Add(library);
        await db.SaveChangesAsync();

        var clone = MediaAsset.NewDerived(
            library, $"shops/{shop.Id}/clone/{Guid.NewGuid():N}.webp", 320, 96, 500, "320x96,inside", 0.5f, 0.5f);
        db.MediaAssets.Add(clone);
        await db.SaveChangesAsync();

        // Xoá CỨNG (raw SQL) — EF interceptor chặn Delete thành soft-delete cho BaseAuditableEntity
        // (AppDbContext.InterceptSoftDelete), nên bypass bằng SQL thô để mô phỏng job dọn thật.
        await db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "MediaAsset" WHERE "Id" = {library.Id}""");

        await using var verifyDb = CreateContext();
        var reloadedClone = await verifyDb.MediaAssets.IgnoreQueryFilters().SingleAsync(m => m.Id == clone.Id);

        Assert.Null(reloadedClone.SourceAssetId);
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        return new AppDbContext(options, new TestTenantContext());
    }

    private static Shop NewShop()
    {
        var id = Guid.NewGuid();
        return new Shop(id)
        {
            Name = "Test Shop",
            Slug = $"test-shop-{id:N}",
            Kind = ShopKind.Hosted,
        };
    }
}
