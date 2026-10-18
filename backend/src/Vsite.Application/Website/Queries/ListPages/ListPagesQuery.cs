using MediatR;
using Vsite.Application.Website.Dtos;

namespace Vsite.Application.Website.Queries.ListPages;

/// <summary>Toàn bộ trang còn sống của website (3–7 trang) — không phân trang, sắp theo <c>SortOrder</c>.</summary>
public sealed record ListPagesQuery : IRequest<IReadOnlyList<PageDto>>;
