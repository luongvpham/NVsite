using Vsite.Domain.Exceptions;
using Vsite.Domain.Shop.Enums;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;

namespace Vsite.IntegrationTests;

/// <summary>REFACTOR-BE-001 — invariant `Kind = ExternalOnly ⇒ ExternalUrl` nằm ở entity (trước đây
/// chỉ có validator + CHECK ở DB). Không cần DB.</summary>
public sealed class ShopEntityTests
{
    [Fact]
    public void Creating_ExternalOnly_shop_without_url_is_rejected()
    {
        var ex = Assert.Throws<DomainException>(() => new ShopEntity("Shop", "shop-abc", ShopKind.ExternalOnly));
        Assert.Equal("SHOP_EXTERNAL_URL_REQUIRED", ex.ErrorCode);
    }

    [Fact]
    public void Switching_to_ExternalOnly_without_url_is_rejected_and_leaves_shop_unchanged()
    {
        var shop = new ShopEntity("Shop", "shop-abc", ShopKind.Hosted);

        Assert.Throws<DomainException>(() => shop.Update("Shop", "shop-abc", ShopKind.ExternalOnly, "  ", ShopStatus.Active));

        Assert.Equal(ShopKind.Hosted, shop.Kind);
        Assert.Equal(ShopStatus.Draft, shop.Status);
    }

    [Fact]
    public void Update_with_url_switches_to_ExternalOnly()
    {
        var shop = new ShopEntity("Shop", "shop-abc", ShopKind.Hosted);

        shop.Update("Shop 2", "shop-xyz", ShopKind.ExternalOnly, "https://example.com", ShopStatus.Active);

        Assert.Equal(("Shop 2", "shop-xyz", ShopKind.ExternalOnly, "https://example.com", ShopStatus.Active),
            (shop.Name, shop.Slug, shop.Kind, shop.ExternalUrl, shop.Status));
    }
}
