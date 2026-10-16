using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media.Dtos;
using Vsite.Application.Media.Interfaces;

namespace Vsite.Application.Media.Queries.GetReferences;

/// <summary>Gom tham chiếu từ `Shop.LogoId` (Media dependsOn Shop, đọc thẳng `db.Shops`) và mọi <see cref="IMediaReferenceSource"/>
/// đã đăng ký (T11: PageDraft của Website). Không require asset phải tồn tại/còn trong library — hỏi tham chiếu của một id bất kỳ
/// (kể cả đã soft delete) vẫn hợp lệ, trả rỗng nếu không có tham chiếu nào.
/// TODO(Bước 8, `08` §4): SitePublication — thêm một <see cref="IMediaReferenceSource"/> khi entity tồn tại.</summary>
public sealed class GetReferencesHandler(IAppDbContext db, IEnumerable<IMediaReferenceSource> sources)
    : IRequestHandler<GetReferencesQuery, MediaReferencesDto>
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

        foreach (var source in sources)
        {
            references.AddRange(await source.FindAsync(request.AssetId, cancellationToken));
        }

        return new MediaReferencesDto(references);
    }
}
