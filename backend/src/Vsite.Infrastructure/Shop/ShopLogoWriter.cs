using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Shop.Interfaces;
using Vsite.Domain.Exceptions;

namespace Vsite.Infrastructure.Shop;

/// <summary>Implementation thật của <see cref="IShopLogoWriter"/> (T7, MEDIA-001, #73/#82) — nhận
/// <see cref="IAppDbContext"/> qua DI Scoped, CÙNG instance với <c>MediaAssetWriter</c> trong một
/// request (đăng ký ở <c>DependencyInjection.AddInfrastructure</c>: cả hai resolve từ
/// <c>AppDbContext</c> Scoped). Chỉ set <c>LogoId</c> trên entity đang track, không gọi
/// SaveChanges — xem XML doc trên interface vì sao.</summary>
public sealed class ShopLogoWriter(IAppDbContext db) : IShopLogoWriter
{
    public async Task SetLogoAsync(Guid shopId, Guid libraryAssetId, CancellationToken ct)
    {
        var shop = await db.Shops.FirstOrDefaultAsync(s => s.Id == shopId, ct)
            ?? throw new NotFoundException("Shop", shopId);

        shop.LogoId = libraryAssetId;
    }
}
