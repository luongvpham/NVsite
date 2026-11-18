using MediatR;
using Vsite.Application.Website.Dtos;

namespace Vsite.Application.Website.Commands.PublishWebsite;

/// <summary>Xuất bản toàn bộ website của shop hiện tại (<c>ITenantContext</c>, #99) thành một <c>SitePublication</c> bất biến.
/// Không mang <c>ShopId</c> (#21.4).</summary>
public sealed record PublishWebsiteCommand(string? Note) : IRequest<PublicationDto>;
