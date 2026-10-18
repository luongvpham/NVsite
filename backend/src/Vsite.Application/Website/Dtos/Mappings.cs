using System.Text.Json;
using Vsite.Domain.Website.Entities;
using WebsiteEntity = Vsite.Domain.Website.Entities.Website;

namespace Vsite.Application.Website.Dtos;

internal static class Mappings
{
    public static PageDto ToDto(Page page, bool isDirty) => new(
        page.Id,
        page.Kind,
        page.SystemType,
        page.Slug,
        page.Title,
        page.SeoTitle,
        page.SeoDescription,
        page.OgImageId,
        page.NoIndex,
        page.IsEnabled,
        page.SortOrder,
        page.IsHomePage,
        isDirty,
        page.CanBeDeleted,
        page.UpdatedAt ?? page.CreatedAt);

    public static WebsiteDto ToDto(WebsiteEntity website, Guid homePageId) => new(
        website.Id,
        website.Status,
        website.DefaultLocale,
        website.Locales,
        website.DefaultSeoTitle,
        website.DefaultSeoDescription,
        website.DefaultOgImageId,
        website.FaviconAssetId,
        website.TrackingSnippets.ToJsonElement(),
        homePageId);
}
