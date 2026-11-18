using System.Text.Json;
using MediatR;
using Vsite.Domain.Website.Enums;

namespace Vsite.Application.Website.Queries.GetPublishedSite;

/// <summary>
/// Đọc công khai Shop Site (#108). KHÔNG mang tham số định danh shop nào — shop đến từ <c>ITenantContext</c> (suy từ Host).
/// <paramref name="IfNoneMatch"/> là giá trị thô của header <c>If-None-Match</c>.
/// </summary>
public sealed record GetPublishedSiteQuery(string? IfNoneMatch) : IRequest<PublishedSiteResult>;

/// <summary><paramref name="Body"/> = null khi <paramref name="NotModified"/>. <paramref name="ETag"/> null với response <c>Suspended</c>.</summary>
public sealed record PublishedSiteResult(PublishedSiteDto? Body, string? ETag, bool NotModified);

/// <summary>Response <c>GET /api/site</c>. <paramref name="Publication"/> null khi <c>Suspended</c>.</summary>
public sealed record PublishedSiteDto(WebsiteStatus Status, PublishedShopDto Shop, PublishedPublicationDto? Publication);

public sealed record PublishedShopDto(string Name, string? LogoUrl);

/// <summary><paramref name="Snapshot"/> là JSON bất biến của bản publish (shape ở plan §4); OpenAPI ra schema rỗng, FE dùng Zod.</summary>
public sealed record PublishedPublicationDto(Guid Id, int Version, DateTimeOffset PublishedAt, JsonElement Snapshot);
