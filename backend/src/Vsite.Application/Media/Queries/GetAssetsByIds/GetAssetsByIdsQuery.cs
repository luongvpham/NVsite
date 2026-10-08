using MediatR;
using Vsite.Application.Media.Dtos;

namespace Vsite.Application.Media.Queries.GetAssetsByIds;

/// <summary>T6, MEDIA-001 (#72) — `GET /shops/{shopId}/media/assets?ids=...`. Tối đa 200 id
/// (validator). Trả những id tìm thấy KỂ CẢ đã soft delete (clone của một bản Library đã xoá vẫn
/// phải render được) nhưng vẫn cùng shop — không bao giờ id của shop khác.</summary>
public sealed record GetAssetsByIdsQuery(Guid ShopId, IReadOnlyList<Guid> Ids) : IRequest<IReadOnlyList<MediaAssetDto>>;
