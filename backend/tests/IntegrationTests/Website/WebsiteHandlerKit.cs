using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Website.Commands.CreatePage;
using Vsite.Application.Website.Commands.CreateWebsite;
using Vsite.Application.Website.Commands.DeletePage;
using Vsite.Application.Website.Commands.SavePageDraft;
using Vsite.Application.Website.Commands.UpdatePage;
using Vsite.Application.Website.Commands.UpdateTheme;
using Vsite.Application.Website.Dtos;
using Vsite.Application.Website.Options;
using Vsite.Application.Website.Queries.GetPageDraft;
using Vsite.Application.Website.Queries.GetTheme;
using Vsite.Application.Website.Queries.GetWebsite;
using Vsite.Application.Website.Queries.ListPages;
using Vsite.Application.Website.Seeding;
using Vsite.Application.Website.Validation;
using Vsite.Application.Website.Versioning;
using Vsite.Domain.Media.Entities;
using Vsite.Domain.ReservedRoutes;
using Vsite.Domain.Shop.Entities;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Media;
using Vsite.Infrastructure.Persistence;
using Vsite.Infrastructure.Website;

namespace Vsite.IntegrationTests.Website;

/// <summary>Dựng handler Website THẬT (catalog/artifact thật ở repo root, Postgres thật) cho test handler-level. Không qua HTTP.</summary>
internal static class WebsiteHandlerKit
{
    /// <summary>Cấu hình có key <c>reservedShopSitePaths</c> (config/reserved-routes.json chưa có — người sẽ thêm).</summary>
    public static readonly ReservedRoutes ShopSiteReserved = new(["admin"], ["www"])
    {
        ReservedShopSitePaths = ["media", "api", "assets", "static"],
    };

    private static readonly Lazy<Services> Shared = new(() => new Services());

    public static Services S => Shared.Value;

    public sealed class Services
    {
        public Services()
        {
            Locales = new LocaleCatalog(TreeFixtures.Config("locales.json"));
            ThemeTokens = new ThemeTokenCatalog(TreeFixtures.Config("theme-tokens.json"));
            SystemPages = new SystemPageCatalog(TreeFixtures.Config("system-pages.json"));
            Rules = new TreeRulesCatalog(TreeFixtures.Generated("tree-rules.json"));
            Schemas = new WebsiteSchemaProvider(
                TreeFixtures.Generated("tree-envelope.schema.json"),
                TreeFixtures.Generated("theme-tokens.schema.json"),
                TreeFixtures.Generated("props-schemas.json"));
            Versioning = new DocumentVersioning([]);
            Validator = TreeFixtures.CreateValidator(versioning: Versioning);
            ThemeValidator = new ThemeTokensValidator(Schemas, Versioning);
            Seeder = new WebsiteSeeder(SystemPages, Validator);
            Slugs = new SlugRules(Locales, SystemPages, new FixedReservedRoutes(ShopSiteReserved));
        }

        public LocaleCatalog Locales { get; }
        public ThemeTokenCatalog ThemeTokens { get; }
        public SystemPageCatalog SystemPages { get; }
        public TreeRulesCatalog Rules { get; }
        public WebsiteSchemaProvider Schemas { get; }
        public DocumentVersioning Versioning { get; }
        public PageTreeValidator Validator { get; }
        public ThemeTokensValidator ThemeValidator { get; }
        public WebsiteSeeder Seeder { get; }
        public SlugRules Slugs { get; }
    }

    public sealed class FixedReservedRoutes(ReservedRoutes routes) : IReservedRoutesProvider
    {
        public ReservedRoutes Routes { get; } = routes;
    }

    public sealed class FixedActor(Guid? userId) : IAuditActor
    {
        public Guid? UserId { get; } = userId;
    }

    public static AppDbContext NewDb(string connectionString, Guid? shopId, Guid? actor = null, params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString);
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        return new AppDbContext(builder.Options, new TestTenantContext(shopId), publisher: null, auditActor: new FixedActor(actor));
    }

    /// <summary>Context + tenant dùng chung cho một "request" (handler đọc cùng ITenantContext với DbContext).</summary>
    public sealed class Request : IAsyncDisposable
    {
        public Request(string connectionString, Guid? shopId, Guid? actor = null, params IInterceptor[] interceptors)
        {
            Tenant = new TestTenantContext(shopId);
            var builder = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString);
            if (interceptors.Length > 0)
            {
                builder.AddInterceptors(interceptors);
            }

            Db = new AppDbContext(builder.Options, Tenant, publisher: null, auditActor: new FixedActor(actor));
            Media = new MediaReferenceValidator(Db, Tenant);
        }

        public TestTenantContext Tenant { get; }
        public AppDbContext Db { get; }
        public MediaReferenceValidator Media { get; }

        public Task<WebsiteDto> CreateWebsite(Guid shopId) =>
            new CreateWebsiteHandler(Db, Tenant, S.Seeder, S.ThemeValidator, S.ThemeTokens)
                .Handle(new CreateWebsiteCommand(shopId), default);

        public Task<WebsiteDto> GetWebsite() => new GetWebsiteHandler(Db, Tenant).Handle(new GetWebsiteQuery(), default);

        public Task<IReadOnlyList<PageDto>> ListPages() => new ListPagesHandler(Db, Tenant).Handle(new ListPagesQuery(), default);

        public Task<PageDto> CreatePage(CreatePageCommand command) =>
            new CreatePageHandler(Db, Tenant, S.Slugs, Media, S.Seeder).Handle(command, default);

        public Task<PageDto> UpdatePage(UpdatePageCommand command) =>
            new UpdatePageHandler(Db, Tenant, S.Slugs, Media).Handle(command, default);

        public Task DeletePage(Guid pageId) => new DeletePageHandler(Db, Tenant).Handle(new DeletePageCommand(pageId), default);

        public Task<PageDraftDto> GetDraft(Guid pageId) =>
            new GetPageDraftHandler(Db, Tenant, S.Versioning).Handle(new GetPageDraftQuery(pageId), default);

        public Task<SavePageDraftResultDto> SaveDraft(Guid pageId, string treeJson, long version) =>
            new SavePageDraftHandler(Db, Tenant, S.Validator, S.Rules, S.Versioning, Media)
                .Handle(new SavePageDraftCommand(pageId, treeJson, version), default);

        public Task<ThemeDto> GetTheme() => new GetThemeHandler(Db, Tenant, S.Versioning).Handle(new GetThemeQuery(), default);

        public Task<ThemeDto> UpdateTheme(string tokensJson, string? presetKey = null) =>
            new UpdateThemeHandler(Db, Tenant, S.ThemeValidator).Handle(new UpdateThemeCommand(presetKey, tokensJson), default);

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    public static async Task MigrateAsync(string connectionString)
    {
        await using var db = NewDb(connectionString, null);
        await db.Database.MigrateAsync();
    }

    public static async Task<Guid> AddShopAsync(string connectionString, ShopKind kind = ShopKind.Hosted)
    {
        await using var db = NewDb(connectionString, null);
        var id = Guid.NewGuid();
        db.Shops.Add(new Shop(
            id, "Shop test", $"shop-{id:N}", kind, kind == ShopKind.ExternalOnly ? "https://example.vn" : null));
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>Shop Hosted + website trắng (qua handler thật). Trả id shop, website, và các page.</summary>
    public static async Task<Seeded> AddWebsiteAsync(string connectionString)
    {
        var shopId = await AddShopAsync(connectionString);
        await using var request = new Request(connectionString, shopId);
        var website = await request.CreateWebsite(shopId);
        var pages = await request.ListPages();
        return new Seeded(shopId, website, pages);
    }

    public sealed record Seeded(Guid ShopId, WebsiteDto Website, IReadOnlyList<PageDto> Pages)
    {
        public PageDto Home => Pages.Single(p => p.Slug == "");
        public PageDto Contact => Pages.Single(p => p.SystemType == Vsite.Domain.Website.Enums.SystemType.Contact);
        public PageDto NotFound => Pages.Single(p => p.SystemType == Vsite.Domain.Website.Enums.SystemType.NotFound);
    }

    public static async Task<Guid> AddAssetAsync(string connectionString, Guid shopId, string kind)
    {
        await using var db = NewDb(connectionString, shopId);
        var library = MediaAsset.NewLibrary(
            shopId, $"shops/{shopId}/library/{Guid.NewGuid():N}.webp", 100, 100, 10, 0.5f, 0.5f, null, null);
        db.MediaAssets.Add(library);
        await db.SaveChangesAsync();
        if (kind == "Library")
        {
            return library.Id;
        }

        var clone = MediaAsset.NewClone(library, $"shops/{shopId}/clone/{Guid.NewGuid():N}.webp", 100, 100, 10, "p,cover", 0.5f, 0.5f);
        db.MediaAssets.Add(clone);
        await db.SaveChangesAsync();
        if (kind == "DeletedClone")
        {
            clone.SoftDeleteFromLibrary();
            await db.SaveChangesAsync();
        }

        return clone.Id;
    }

    public static string TreeWith(params string[] nodes) =>
        "{\"schemaVersion\":1,\"id\":\"root\",\"type\":\"Page\",\"children\":[" + string.Join(",", nodes) + "]}";

    public static string RichText(string id, string html) =>
        "{\"id\":\"" + id + "\",\"type\":\"RichText\",\"variant\":\"RichText01\",\"props\":{\"content\":"
        + JsonSerializer.Serialize(html) + "}}";

    public static string Hero(string id, string imageId) =>
        "{\"id\":\"" + id + "\",\"type\":\"Hero\",\"variant\":\"Hero01\",\"props\":{\"title\":\"Chào\",\"image\":{\"imageId\":\""
        + imageId + "\",\"alt\":\"x\"}}}";

    public static string Gallery(string id, string imageId) =>
        "{\"id\":\"" + id + "\",\"type\":\"Gallery\",\"variant\":\"Gallery01\",\"props\":{\"items\":[{\"image\":{\"imageId\":\""
        + imageId + "\",\"alt\":\"x\"}}]}}";

    /// <summary>Bump xmin của một draft từ kết nối khác ngay TRƯỚC khi handler ghi — mô phỏng race thật ở SaveChanges.</summary>
    public sealed class BumpXminInterceptor(string connectionString, Guid pageId) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await using var other = NewDb(connectionString, null);
            await other.Database.ExecuteSqlInterpolatedAsync($"UPDATE page_draft SET is_dirty = is_dirty WHERE page_id = {pageId}", cancellationToken);
            return result;
        }
    }
}
