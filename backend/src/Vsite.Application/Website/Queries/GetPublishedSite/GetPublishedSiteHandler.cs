using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Shop.Interfaces;
using Vsite.Application.Website.Interfaces;
using Vsite.Application.Website.Versioning;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Shop.Enums;
using Vsite.Domain.Website.Enums;

namespace Vsite.Application.Website.Queries.GetPublishedSite;

/// <summary>
/// Flow 4 (WEBSITE-004 T6, #108/#109). Shop đến DUY NHẤT từ <see cref="ITenantContext"/> (Host đã được middleware suy ra) và chỉ khi
/// audience = <c>Shop</c>; handler không đọc tham số định danh nào, không <c>IgnoreQueryFilters()</c>.
/// <para><b>Ranh giới cứng (05 §7):</b> handler này KHÔNG được chạm <c>db.PageDrafts</c>, <c>db.Pages</c>, <c>db.Themes</c> — chỉ bản
/// publish bất biến (<c>SitePublications</c>) + con trỏ <c>Websites</c> + dòng <c>Shops</c>. T7 có test kiến trúc quét file này.</para>
/// <para>Dòng <c>Shop</c> đọc SỐNG mỗi request (một query theo khoá chính) nên đình chỉ/đóng shop có hiệu lực ngay; con trỏ Website
/// đi qua Redis. Mọi "không có site" → <c>404 SITE_NOT_FOUND</c> (một mã, không lộ lý do).</para>
/// </summary>
public sealed class GetPublishedSiteHandler(
    IAppDbContext db,
    ITenantContext tenant,
    ISitePointerCache pointerCache,
    IShopLogoReader logoReader,
    DocumentVersioning versioning)
    : IRequestHandler<GetPublishedSiteQuery, PublishedSiteResult>
{
    public async Task<PublishedSiteResult> Handle(GetPublishedSiteQuery request, CancellationToken cancellationToken)
    {
        if (tenant.AudienceKind != TenantAudienceKind.Shop || tenant.ShopId is not { } shopId)
        {
            throw NotFound();
        }

        var shop = await db.Shops.AsNoTracking()
            .Where(s => s.Id == shopId)
            .Select(s => new ShopRow(s.Name, s.Status, s.LogoId, s.UpdatedAt ?? s.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
        if (shop is null || shop.Status == ShopStatus.Closed)
        {
            throw NotFound();
        }

        var pointer = await pointerCache.GetAsync(shopId, cancellationToken)
            ?? await ReadPointerFromDatabaseAsync(shopId, cancellationToken);

        var result = await TryServeAsync(shopId, shop, pointer, request.IfNoneMatch, cancellationToken);
        if (result is not null)
        {
            return result;
        }

        // Con trỏ cũ trỏ tới bản không còn (cache sống lâu hơn dữ liệu): xoá, đọc lại DB ĐÚNG MỘT lần.
        await pointerCache.InvalidateAsync(shopId, cancellationToken);
        pointer = await ReadPointerFromDatabaseAsync(shopId, cancellationToken);
        return await TryServeAsync(shopId, shop, pointer, request.IfNoneMatch, cancellationToken) ?? throw NotFound();
    }

    private async Task<SitePointer> ReadPointerFromDatabaseAsync(Guid shopId, CancellationToken cancellationToken)
    {
        var row = await db.Websites.AsNoTracking()
            .Where(w => w.ShopId == shopId)
            .Select(w => new { w.Status, w.CurrentPublicationId })
            .FirstOrDefaultAsync(cancellationToken);

        var pointer = row is null ? new SitePointer(null, null) : new SitePointer(row.Status, row.CurrentPublicationId);
        await pointerCache.SetAsync(shopId, pointer, cancellationToken);
        return pointer;
    }

    /// <summary>Trả null CHỈ khi con trỏ có <c>PublicationId</c> nhưng bản đó không đọc được; mọi "không có site" ném 404.</summary>
    private async Task<PublishedSiteResult?> TryServeAsync(
        Guid shopId, ShopRow shop, SitePointer pointer, string? ifNoneMatch, CancellationToken cancellationToken)
    {
        // R6: quyết định theo CẢ Status lẫn CurrentPublicationId — Draft có dòng publication lệch vẫn 404.
        if (pointer.WebsiteStatus is null or WebsiteStatus.Draft || pointer.PublicationId is not { } publicationId)
        {
            throw NotFound();
        }

        if (pointer.WebsiteStatus == WebsiteStatus.Suspended || shop.Status == ShopStatus.Suspended)
        {
            var logo = await logoReader.GetLogoUrlAsync(shopId, shop.LogoId, cancellationToken);
            return new PublishedSiteResult(
                new PublishedSiteDto(WebsiteStatus.Suspended, new PublishedShopDto(shop.Name, logo), null), null, false);
        }

        // Strong ETag gồm cả Shop.UpdatedAt: đổi tên/logo shop làm ETag đổi mà không cần publish (R12).
        var etag = $"\"{publicationId:N}-{shop.UpdatedAt.ToUnixTimeMilliseconds()}\"";
        if (Matches(ifNoneMatch, etag))
        {
            return new PublishedSiteResult(null, etag, true);
        }

        var publication = await db.SitePublications.AsNoTracking()
            .Where(p => p.Id == publicationId && p.ShopId == shopId)
            .Select(p => new { p.Id, p.Version, p.PublishedAt, p.Snapshot })
            .FirstOrDefaultAsync(cancellationToken);
        if (publication is null)
        {
            return null;
        }

        var snapshotJson = StoredDocuments.ReadCurrent(DocumentKind.Snapshot, publication.Snapshot, versioning);
        using var snapshotDoc = JsonDocument.Parse(snapshotJson);
        var logoUrl = await logoReader.GetLogoUrlAsync(shopId, shop.LogoId, cancellationToken);

        return new PublishedSiteResult(
            new PublishedSiteDto(
                WebsiteStatus.Published,
                new PublishedShopDto(shop.Name, logoUrl),
                new PublishedPublicationDto(publication.Id, publication.Version, publication.PublishedAt, snapshotDoc.RootElement.Clone())),
            etag,
            false);
    }

    /// <summary>So <c>If-None-Match</c> theo so sánh yếu của RFC 9110: danh sách phân tách dấu phẩy, bỏ tiền tố <c>W/</c>, <c>*</c> khớp mọi bản.</summary>
    private static bool Matches(string? ifNoneMatch, string etag)
    {
        if (string.IsNullOrWhiteSpace(ifNoneMatch))
        {
            return false;
        }

        foreach (var raw in ifNoneMatch.Split(','))
        {
            var candidate = raw.Trim();
            if (candidate == "*")
            {
                return true;
            }

            if (candidate.StartsWith("W/", StringComparison.Ordinal))
            {
                candidate = candidate[2..];
            }

            if (string.Equals(candidate, etag, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static NotFoundException NotFound() => new("Site", "current host");

    private sealed record ShopRow(string Name, ShopStatus Status, Guid? LogoId, DateTimeOffset UpdatedAt);
}
