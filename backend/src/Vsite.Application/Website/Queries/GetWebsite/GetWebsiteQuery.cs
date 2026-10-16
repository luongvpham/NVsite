using MediatR;
using Vsite.Application.Website.Dtos;

namespace Vsite.Application.Website.Queries.GetWebsite;

/// <summary>Website của shop hiện tại (<c>ITenantContext</c>). Chưa tạo → 404 <c>WEBSITE_NOT_FOUND</c> (Portal dựa vào đó hiện nút "Tạo website").</summary>
public sealed record GetWebsiteQuery : IRequest<WebsiteDto>;
