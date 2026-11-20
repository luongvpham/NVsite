using MediatR;
using Vsite.Application.Website.Dtos;

namespace Vsite.Application.Website.Queries.GetNavigation;

/// <summary>Menu đã ghép của website thuộc shop hiện tại (WEBSITE-007, Flow 1/3). Chưa có website → 404 <c>WEBSITE_NOT_FOUND</c>.</summary>
public sealed record GetNavigationQuery : IRequest<NavigationDto>;
