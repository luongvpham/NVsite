using MediatR;
using Vsite.Application.Media.Dtos;

namespace Vsite.Application.Media.Queries.GetReferences;

/// <summary>T6, MEDIA-001 (#71) — `GET /shops/{shopId}/media/library/{assetId}/references`. Ghi chú
/// TODO tree/snapshot ở <see cref="MediaReferencesDto"/>.</summary>
public sealed record GetReferencesQuery(Guid ShopId, Guid AssetId) : IRequest<MediaReferencesDto>;
