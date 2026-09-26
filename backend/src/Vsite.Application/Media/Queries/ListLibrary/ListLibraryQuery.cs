using MediatR;
using Vsite.Application.Media.Dtos;
using Vsite.Domain.Pagination;

namespace Vsite.Application.Media.Queries.ListLibrary;

/// <summary>T6, MEDIA-001 — `GET /shops/{shopId}/media/library?page&amp;pageSize`. `ShopId` từ route
/// (Quyết định #21.4), Global Query Filter ràng cả `ShopId` lẫn `NOT IsDeleted`. `PageSize` mặc định
/// 24, tối đa 100 (validator).</summary>
public sealed record ListLibraryQuery(Guid ShopId, int Page, int PageSize) : IRequest<PagedResult<MediaAssetDto>>;
