using Vsite.Application.Website.Queries.GetPublishedSite;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// T7 (M2 từ review T6) — đường HANDLER của <c>GET /api/site</c>, tách khỏi middleware: audience ≠ Shop hoặc ShopId null ⇒
/// <c>NotFound</c> (→ 404 <c>SITE_NOT_FOUND</c>) TRƯỚC mọi truy cập DB/cache. Không cần Docker: mọi phụ thuộc là <c>null!</c>, nên
/// nếu handler đi quá cổng audience thì <see cref="NullReferenceException"/> làm test đỏ.
/// </summary>
public sealed class GetPublishedSiteHandlerTests
{
    private static GetPublishedSiteHandler HandlerFor(TestTenantContext tenant) =>
        new(db: null!, tenant, pointerCache: null!, logoReader: null!, versioning: null!);

    [Theory]
    [InlineData(TenantAudienceKind.Main)]
    [InlineData(TenantAudienceKind.Portal)]
    public async Task Non_shop_audience_is_NotFound_even_when_ShopId_is_set(TenantAudienceKind audience)
    {
        // Portal có thể mang ShopId (route {shopId} → SHOP-001): handler vẫn không được phục vụ site cho audience đó.
        var tenant = new TestTenantContext(Guid.NewGuid()) { AudienceKind = audience };

        await Assert.ThrowsAsync<NotFoundException>(() => HandlerFor(tenant).Handle(new GetPublishedSiteQuery(null), default));
    }

    [Fact]
    public async Task Shop_audience_without_ShopId_is_NotFound()
    {
        var tenant = new TestTenantContext { AudienceKind = TenantAudienceKind.Shop, ShopId = null };

        await Assert.ThrowsAsync<NotFoundException>(() => HandlerFor(tenant).Handle(new GetPublishedSiteQuery(null), default));
    }

    [Fact]
    public async Task Anonymous_main_audience_without_ShopId_is_NotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => HandlerFor(new TestTenantContext()).Handle(new GetPublishedSiteQuery(null), default));
    }
}
