using MediatR;
using Vsite.Application.Media.Dtos;

namespace Vsite.Application.Media.Queries.GetUsage;

/// <summary>T6, MEDIA-001 (#55) — `GET /shops/{shopId}/media/usage`.</summary>
public sealed record GetUsageQuery(Guid ShopId) : IRequest<MediaUsageDto>;
