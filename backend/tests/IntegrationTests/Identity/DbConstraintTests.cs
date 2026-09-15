using Microsoft.EntityFrameworkCore;
using Vsite.Domain.Identity;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Identity.Enums;
using Vsite.Domain.Shop.Entities;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests;

/// <summary>
/// 03 §4 — ràng buộc phải nằm ở DB, không chỉ ở code (Quyết định #17: "codegen > skill > CLAUDE.md
/// > hy vọng agent nhớ"). Mỗi test dưới đây phải FAIL THẬT khi bị vi phạm, không chỉ là tài liệu.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DbConstraintTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migration_applies_and_seeds_seven_fixed_roles()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var roles = await db.Roles.ToListAsync();

        Assert.Equal(7, roles.Count);
        Assert.Contains(roles, r => r.Code == "Owner" && r.Scope == RoleScope.Shop);
        Assert.Contains(roles, r => r.Code == "PlatformAdmin" && r.Scope == RoleScope.Platform);
    }

    [Fact]
    public async Task Composite_FK_rejects_Platform_role_id_assigned_to_UserShop()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var user = NewUser();
        var shop = NewShop();
        db.Users.Add(user);
        db.Shops.Add(shop);
        await db.SaveChangesAsync();

        // Shop có thật (FK ShopId hợp lệ) — chỉ cố tình sai RoleId trỏ tới role Scope=Platform
        // (PlatformUser), trong khi UserShop.RoleScope generated luôn là 'Shop'. Composite FK
        // (RoleId, RoleScope) → Role(Id, Scope) phải reject vì không có row Role nào có
        // (Id=PlatformUserId, Scope='Shop') — đây là VI PHẠM DUY NHẤT trong record này.
        db.UserShops.Add(new UserShop
        {
            UserId = user.Id,
            ShopId = shop.Id,
            RoleId = WellKnownRoles.PlatformUserId,
            Source = UserShopSource.RegisteredOnShop,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Check_constraint_rejects_email_without_verified_at()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        // [2] Có email <=> đã verify — vi phạm CHECK ck_user_email_verified.
        db.Users.Add(new User
        {
            Email = $"unverified-{Guid.NewGuid():N}@example.test",
            EmailNormalized = null,
            EmailVerifiedAt = null,
            PrimaryIdentityKind = PrimaryIdentityKind.Email,
            RoleId = WellKnownRoles.PlatformUserId,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Check_constraint_rejects_primary_identity_email_without_email()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        // [3] PrimaryIdentityKind = Email nhưng Email null — vi phạm CHECK ck_user_primary_identity.
        db.Users.Add(new User
        {
            Email = null,
            PrimaryIdentityKind = PrimaryIdentityKind.Email,
            RoleId = WellKnownRoles.PlatformUserId,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Unique_partial_index_rejects_duplicate_normalized_email()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var normalized = $"DUP-{Guid.NewGuid():N}@EXAMPLE.TEST";
        db.Users.Add(NewUser(normalized));
        await db.SaveChangesAsync();

        db.Users.Add(NewUser(normalized));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Check_constraint_rejects_ExternalOnly_shop_without_ExternalUrl()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        // 04 §2.1 — vi phạm CHECK ck_shop_external_url. SHOP-001 §4.5 yêu cầu chặn CẢ ở validator
        // (422, xem CreateShopValidator/UpdateShopValidator) LẪN ở DB — đây là lớp phòng thủ thứ hai.
        db.Shops.Add(NewShop(kind: ShopKind.ExternalOnly, externalUrl: null));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        return new AppDbContext(options, new TestTenantContext());
    }

    private static Shop NewShop(ShopKind kind = ShopKind.Hosted, string? externalUrl = null)
    {
        var id = Guid.NewGuid();
        return new Shop(id)
        {
            Name = "Test Shop",
            Slug = $"test-shop-{id:N}",
            Kind = kind,
            ExternalUrl = externalUrl,
        };
    }

    private static User NewUser(string? emailNormalized = null)
    {
        var id = Guid.NewGuid();
        var normalized = emailNormalized ?? $"user-{id:N}@EXAMPLE.TEST";
        return new User(id)
        {
            Email = normalized.ToLowerInvariant(),
            EmailNormalized = normalized,
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            PrimaryIdentityKind = PrimaryIdentityKind.Email,
            RoleId = WellKnownRoles.PlatformUserId,
        };
    }
}
