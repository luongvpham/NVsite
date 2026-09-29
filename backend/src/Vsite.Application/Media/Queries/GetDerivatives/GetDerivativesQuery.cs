using MediatR;
using Vsite.Application.Media.Dtos;

namespace Vsite.Application.Media.Queries.GetDerivatives;

/// <summary>MEDIA-001 D3 (#73) — `GET /shops/{shopId}/media/library/{assetId}/derivatives?preset=`.
/// Lookup (bản Library gốc, preset) → bản phái sinh. <see cref="Preset"/> null = mọi phái sinh.</summary>
public sealed record GetDerivativesQuery(Guid ShopId, Guid AssetId, string? Preset) : IRequest<IReadOnlyList<MediaAssetDto>>;
