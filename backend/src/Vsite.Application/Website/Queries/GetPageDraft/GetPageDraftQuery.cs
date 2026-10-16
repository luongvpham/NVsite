using MediatR;
using Vsite.Application.Website.Dtos;

namespace Vsite.Application.Website.Queries.GetPageDraft;

/// <summary>Mở trang trong editor. Trang lạ / của shop khác / đã xoá → 404 <c>PAGE_NOT_FOUND</c> (không phân biệt, R2).</summary>
public sealed record GetPageDraftQuery(Guid PageId) : IRequest<PageDraftDto>;
