using MediatR;
using Vsite.Application.Website.Dtos;

namespace Vsite.Application.Website.Queries.GetTheme;

/// <summary>Theme của website thuộc shop hiện tại. Chưa có website → 404 <c>WEBSITE_NOT_FOUND</c>.</summary>
public sealed record GetThemeQuery : IRequest<ThemeDto>;
