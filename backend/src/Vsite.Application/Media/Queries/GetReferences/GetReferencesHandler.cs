using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media.Dtos;

namespace Vsite.Application.Media.Queries.GetReferences;

/// <summary>Hiện chỉ có một nguồn tham chiếu thật: `Shop.LogoId` (Media dependsOn Shop, đọc thẳng
/// `db.Shops`). Không require asset phải tồn tại/còn trong library — hỏi tham chiếu của một id bất
/// kỳ (kể cả đã soft delete) vẫn hợp lệ, trả rỗng nếu không có tham chiếu nào.</summary>
public sealed class GetReferencesHandler(IAppDbContext db) : IRequestHandler<GetReferencesQuery, MediaReferencesDto>
{
    public async Task<MediaReferencesDto> Handle(GetReferencesQuery request, CancellationToken cancellationToken)
    {
        var isShopLogo = await db.Shops
            .AnyAsync(s => s.Id == request.ShopId && s.LogoId == request.AssetId, cancellationToken);

        var references = new List<MediaReferenceDto>();
        if (isShopLogo)
        {
            references.Add(new MediaReferenceDto(MediaReferenceKind.ShopLogo));
        }

        // TODO(Bước 5/8, `DesignIdeal/08-media-asset-design.md` §4): thêm nguồn tham chiếu từ
        // PageDraft/SitePublication (Component Tree quét theo assetId) khi module Website tồn tại.

        return new MediaReferencesDto(references);
    }
}
