using Microsoft.EntityFrameworkCore;
using Vsite.Application.Media.Interfaces;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Media.Entities;
using Vsite.Infrastructure.Media;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests.Media;

/// <summary>
/// T8, MEDIA-001 (#77, `08` §3.5) — <see cref="MediaReferenceValidator"/>, Public Contract mà Bước 5
/// (Website/PageDraft, chưa build) sẽ gọi khi lưu draft/page. Bước 4 chưa có endpoint lưu draft, nên
/// test ở đây kiểm thẳng service, không qua endpoint (xem `Docs/tasks/MEDIA-001/changelog.md`: "chưa
/// làm xong — nối vào handler lưu draft ở Bước 5").
///
/// Test double + khuôn EF Core InMemory giống <c>LibraryHandlerTests</c> — không cần Docker.
/// Test "đúng 1 lệnh SQL" (đòi hỏi provider quan hệ thật, InMemory không phát SQL) nằm riêng ở
/// <c>MediaReferenceValidatorSqlCountTests</c> (Postgres/Testcontainers).
///
/// Chạy: <c>dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~MediaReferenceValidatorTests"</c>.
/// </summary>
public sealed class MediaReferenceValidatorTests
{
    // ---- id bản Library trong treeImageIds -> ném lỗi (test bắt buộc 6) ----

    [Fact]
    public async Task Library_id_in_tree_list_throws()
    {
        var shopId = Guid.NewGuid();
        var tenantContext = new FakeTenantContext { ShopId = shopId };
        await using var db = CreateDb(tenantContext);
        var library = AddLibrary(db, shopId);
        await db.SaveChangesAsync();

        var validator = new MediaReferenceValidator(db, tenantContext);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            validator.EnsureValidAsync([library.Id], [], CancellationToken.None));
        Assert.Equal("MEDIA_INVALID_IMAGE_REFERENCE", ex.ErrorCode);
    }

    // ---- REFACTOR-DB-001: phái sinh ảnh nghiệp vụ (logo) KHÔNG được vào tree ----

    [Fact]
    public async Task Derivative_id_in_tree_list_throws()
    {
        var shopId = Guid.NewGuid();
        var tenantContext = new FakeTenantContext { ShopId = shopId };
        await using var db = CreateDb(tenantContext);
        var library = AddLibrary(db, shopId);
        var derivative = MediaAsset.NewDerivative(
            library, $"shops/{shopId}/d/{Guid.NewGuid():N}.webp", 320, 96, 500, "320x96,inside", 0.5f, 0.5f);
        var clone = AddClone(db, library);
        db.MediaAssets.Add(derivative);
        await db.SaveChangesAsync();

        var validator = new MediaReferenceValidator(db, tenantContext);

        await validator.EnsureValidAsync([clone.Id], [], CancellationToken.None);
        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            validator.EnsureValidAsync([derivative.Id], [], CancellationToken.None));
        Assert.Equal("MEDIA_INVALID_IMAGE_REFERENCE", ex.ErrorCode);
    }

    // ---- id clone của shop B trong treeImageIds (tenant context = A) -> ném lỗi (test bắt buộc 7) ----

    [Fact]
    public async Task Clone_belonging_to_another_shop_in_tree_list_throws()
    {
        var shopAId = Guid.NewGuid();
        var shopBId = Guid.NewGuid();
        var tenantContext = new FakeTenantContext { ShopId = shopBId };
        await using var db = CreateDb(tenantContext);
        var libraryB = AddLibrary(db, shopBId);
        await db.SaveChangesAsync();
        var cloneB = AddClone(db, libraryB);
        await db.SaveChangesAsync();

        // Chuyển tenant context sang shop A (đúng như production: cùng scoped ITenantContext instance
        // dùng chung giữa AppDbContext và MediaReferenceValidator, request kế tiếp resolve shop khác).
        tenantContext.ShopId = shopAId;

        var validator = new MediaReferenceValidator(db, tenantContext);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            validator.EnsureValidAsync([cloneB.Id], [], CancellationToken.None));
        Assert.Equal("MEDIA_INVALID_IMAGE_REFERENCE", ex.ErrorCode);
    }

    // ---- id clone trong businessImageIds -> ném lỗi ----

    [Fact]
    public async Task Clone_id_in_business_list_throws()
    {
        var shopId = Guid.NewGuid();
        var tenantContext = new FakeTenantContext { ShopId = shopId };
        await using var db = CreateDb(tenantContext);
        var library = AddLibrary(db, shopId);
        await db.SaveChangesAsync();
        var clone = AddClone(db, library);
        await db.SaveChangesAsync();

        var validator = new MediaReferenceValidator(db, tenantContext);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            validator.EnsureValidAsync([], [clone.Id], CancellationToken.None));
        Assert.Equal("MEDIA_INVALID_IMAGE_REFERENCE", ex.ErrorCode);
    }

    // ---- id đã soft delete -> ném lỗi ----

    [Fact]
    public async Task Soft_deleted_id_throws()
    {
        var shopId = Guid.NewGuid();
        var tenantContext = new FakeTenantContext { ShopId = shopId };
        await using var db = CreateDb(tenantContext);
        var library = AddLibrary(db, shopId);
        await db.SaveChangesAsync();
        var clone = AddClone(db, library);
        await db.SaveChangesAsync();
        clone.SoftDeleteFromLibrary();
        await db.SaveChangesAsync();

        var validator = new MediaReferenceValidator(db, tenantContext);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            validator.EnsureValidAsync([clone.Id], [], CancellationToken.None));
        Assert.Equal("MEDIA_INVALID_IMAGE_REFERENCE", ex.ErrorCode);
    }

    // ---- hợp lệ, có id lặp -> không ném (đếm theo id khác nhau) ----

    [Fact]
    public async Task Valid_ids_with_duplicates_does_not_throw()
    {
        var shopId = Guid.NewGuid();
        var tenantContext = new FakeTenantContext { ShopId = shopId };
        await using var db = CreateDb(tenantContext);
        var library = AddLibrary(db, shopId);
        await db.SaveChangesAsync();
        var clone = AddClone(db, library);
        await db.SaveChangesAsync();

        var validator = new MediaReferenceValidator(db, tenantContext);

        await validator.EnsureValidAsync(
            treeImageIds: [clone.Id, clone.Id],
            businessImageIds: [library.Id, library.Id],
            CancellationToken.None);
    }

    // ---- danh sách rỗng -> không ném, không query ----

    [Fact]
    public async Task Empty_lists_does_not_throw()
    {
        var shopId = Guid.NewGuid();
        var tenantContext = new FakeTenantContext { ShopId = shopId };
        await using var db = CreateDb(tenantContext);
        var validator = new MediaReferenceValidator(db, tenantContext);

        await validator.EnsureValidAsync([], [], CancellationToken.None);
    }

    // ---- id xuất hiện ở CẢ HAI danh sách -> luôn ném, kể cả khi state khớp một trong hai vế ----

    [Fact]
    public async Task Id_present_in_both_lists_throws()
    {
        var shopId = Guid.NewGuid();
        var tenantContext = new FakeTenantContext { ShopId = shopId };
        await using var db = CreateDb(tenantContext);
        var library = AddLibrary(db, shopId);
        await db.SaveChangesAsync();
        var clone = AddClone(db, library);
        await db.SaveChangesAsync();

        var validator = new MediaReferenceValidator(db, tenantContext);

        // clone.Id hợp lệ về mặt state cho tree list, nhưng nó CŨNG có trong business list -> phải fail.
        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            validator.EnsureValidAsync([clone.Id], [clone.Id], CancellationToken.None));
        Assert.Equal("MEDIA_INVALID_IMAGE_REFERENCE", ex.ErrorCode);
    }

    // ---- TenantContext.ShopId null -> ném lỗi (fail closed), không query ----

    [Fact]
    public async Task Null_tenant_shop_id_throws()
    {
        var tenantContext = new FakeTenantContext { ShopId = null };
        await using var db = CreateDb(tenantContext);
        var validator = new MediaReferenceValidator(db, tenantContext);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            validator.EnsureValidAsync([Guid.NewGuid()], [], CancellationToken.None));
        Assert.Equal("MEDIA_INVALID_IMAGE_REFERENCE", ex.ErrorCode);
    }

    // ---- helpers ----

    private static AppDbContext CreateDb(FakeTenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options, tenantContext);
    }

    private static MediaAsset AddLibrary(AppDbContext db, Guid shopId)
    {
        var asset = MediaAsset.NewLibrary(
            shopId, $"shops/{shopId}/library/{Guid.NewGuid():N}.webp", 1200, 800, 12345, 0.5f, 0.5f, null, null);
        db.MediaAssets.Add(asset);
        return asset;
    }

    private static MediaAsset AddClone(AppDbContext db, MediaAsset library)
    {
        var clone = MediaAsset.NewClone(
            library, $"shops/{library.ShopId}/clone/{Guid.NewGuid():N}.webp", 320, 96, 500, "320x96,inside", 0.5f, 0.5f);
        db.MediaAssets.Add(clone);
        return clone;
    }

    private sealed class FakeTenantContext : Vsite.Domain.Abstractions.ITenantContext
    {
        public Vsite.Domain.Abstractions.TenantAudienceKind AudienceKind => Vsite.Domain.Abstractions.TenantAudienceKind.Portal;
        public Guid? ShopId { get; set; }
    }
}
