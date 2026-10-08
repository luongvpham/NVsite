using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;

namespace Vsite.IntegrationTests;

/// <summary>REFACTOR-BE-001 — `CreatedByUserId`/`UpdatedByUserId` trước đây luôn NULL vì
/// `AppDbContext.StampAuditFields` không ghi. EF InMemory, không cần Docker.</summary>
public sealed class AuditStampingTests
{
    [Fact]
    public async Task Insert_update_and_soft_delete_stamp_the_acting_user()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var creator = Guid.NewGuid();
        var editor = Guid.NewGuid();
        var shop = new ShopEntity("Shop", $"shop-{Guid.NewGuid():N}", ShopKind.Hosted);

        await using (var db = CreateDb(dbName, creator))
        {
            db.Shops.Add(shop);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDb(dbName, editor))
        {
            var loaded = await db.Shops.SingleAsync(s => s.Id == shop.Id);
            Assert.Equal(creator, loaded.CreatedByUserId);
            Assert.Null(loaded.UpdatedByUserId);

            loaded.Update("Renamed", loaded.Slug, loaded.Kind, loaded.ExternalUrl, loaded.Status);
            await db.SaveChangesAsync();
            Assert.Equal(editor, loaded.UpdatedByUserId);
            Assert.Equal(creator, loaded.CreatedByUserId);

            db.Shops.Remove(loaded);
            await db.SaveChangesAsync();
            Assert.True(loaded.IsDeleted);
            Assert.Equal(editor, loaded.UpdatedByUserId);
        }
    }

    /// <summary>REFACTOR-DB-001 — `DeletedAt` đóng dấu qua mọi đường xoá mềm, giữ mốc lần đầu, xoá
    /// khi khôi phục.</summary>
    [Fact]
    public async Task DeletedAt_is_stamped_on_every_soft_delete_path_and_cleared_on_restore()
    {
        await using var db = CreateDb(Guid.NewGuid().ToString("N"), actor: null);

        // 1. Remove() → InterceptSoftDelete.
        var removed = new ShopEntity("A", $"a-{Guid.NewGuid():N}", ShopKind.Hosted);
        // 2. Entity tự set IsDeleted (khuôn MediaAsset.SoftDeleteFromLibrary).
        var flagged = new ShopEntity("B", $"b-{Guid.NewGuid():N}", ShopKind.Hosted);
        // 3. Insert sẵn ở trạng thái xoá (seed/job/test).
        var insertedDeleted = new ShopEntity("C", $"c-{Guid.NewGuid():N}", ShopKind.Hosted) { IsDeleted = true };
        db.Shops.AddRange(removed, flagged, insertedDeleted);
        await db.SaveChangesAsync();
        Assert.NotNull(insertedDeleted.DeletedAt);
        Assert.Null(removed.DeletedAt);

        db.Shops.Remove(removed);
        flagged.IsDeleted = true;
        await db.SaveChangesAsync();
        Assert.NotNull(removed.DeletedAt);
        Assert.NotNull(flagged.DeletedAt);

        // Xoá lần hai giữ mốc lần đầu.
        var firstStamp = flagged.DeletedAt;
        flagged.Update("B2", flagged.Slug, flagged.Kind, flagged.ExternalUrl, flagged.Status);
        await db.SaveChangesAsync();
        Assert.Equal(firstStamp, flagged.DeletedAt);

        // Khôi phục → null.
        flagged.IsDeleted = false;
        await db.SaveChangesAsync();
        Assert.Null(flagged.DeletedAt);
    }

    [Fact]
    public async Task Anonymous_actor_leaves_audit_user_null_without_throwing()
    {
        await using var db = CreateDb(Guid.NewGuid().ToString("N"), actor: null);
        var shop = new ShopEntity("Shop", $"shop-{Guid.NewGuid():N}", ShopKind.Hosted);
        db.Shops.Add(shop);

        await db.SaveChangesAsync();

        Assert.Null(shop.CreatedByUserId);
    }

    private static AppDbContext CreateDb(string name, Guid? actor) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options,
        new TestTenantContext(),
        publisher: null,
        auditActor: new FakeActor(actor));

    private sealed class FakeActor(Guid? userId) : IAuditActor
    {
        public Guid? UserId => userId;
    }
}
