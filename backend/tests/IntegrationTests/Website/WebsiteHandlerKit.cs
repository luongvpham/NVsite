using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Website.Commands.CreatePage;
using Vsite.Application.Website.Commands.CreateWebsite;
using Vsite.Application.Website.Commands.DeletePage;
using Vsite.Application.Website.Commands.PublishWebsite;
using Vsite.Application.Website.Commands.RollbackPublication;
using Vsite.Application.Website.Queries.ListPublications;
using Vsite.Application.Website.Commands.SavePageDraft;
using Vsite.Application.Website.Commands.UpdatePage;
using Vsite.Application.Website.Commands.UpdateTheme;
using Vsite.Application.Website.Dtos;
using Vsite.Application.Website.Interfaces;
using Vsite.Application.Website.Options;
using Vsite.Application.Website.Publishing;
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
using Vsite.Domain.Website.Entities;
using Microsoft.Extensions.Logging.Abstractions;
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
                TreeFixtures.Generated("props-schemas.json"),
                TreeFixtures.Generated("site-snapshot.schema.json"));
            Versioning = new DocumentVersioning([]);
            Validator = TreeFixtures.CreateValidator(versioning: Versioning);
            ThemeValidator = new ThemeTokensValidator(Schemas, Versioning);
            Seeder = new WebsiteSeeder(SystemPages, Validator);
            Slugs = new SlugRules(Locales, SystemPages, new FixedReservedRoutes(ShopSiteReserved));
            RequiredProps = new RequiredPropsChecker(Rules);
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
        public RequiredPropsChecker RequiredProps { get; }
    }

    /// <summary>Ghi lại shop nào bị xoá con trỏ cache (test handler publish/rollback chỉ cần biết handler GỌI; bản Redis thật được
    /// test ở <c>SitePointerCacheTests</c>). Get luôn hụt, Set bỏ qua.</summary>
    public sealed class RecordingSitePointerCache : ISitePointerCache
    {
        public List<Guid> Invalidated { get; } = [];

        public Task<SitePointer?> GetAsync(Guid shopId, CancellationToken cancellationToken) => Task.FromResult<SitePointer?>(null);

        public Task SetAsync(Guid shopId, SitePointer pointer, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task InvalidateAsync(Guid shopId, CancellationToken cancellationToken)
        {
            Invalidated.Add(shopId);
            return Task.CompletedTask;
        }
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
            Actor = new FixedActor(actor);
            var builder = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString);
            if (interceptors.Length > 0)
            {
                builder.AddInterceptors(interceptors);
            }

            Db = new AppDbContext(builder.Options, Tenant, publisher: null, auditActor: Actor);
            Media = new MediaReferenceValidator(Db, Tenant);
        }

        public TestTenantContext Tenant { get; }
        public FixedActor Actor { get; }
        public RecordingSitePointerCache PointerCache { get; } = new();
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

        public Task<PublicationDto> Publish(string? note = null, Vsite.Application.Media.Interfaces.IMediaReferenceValidator? media = null) =>
            new PublishWebsiteHandler(
                    Db, Tenant, S.Validator, S.RequiredProps, media ?? Media, Actor, PointerCache, NullLogger<PublishWebsiteHandler>.Instance)
                .Handle(new PublishWebsiteCommand(note), default);

        public Task<IReadOnlyList<PublicationDto>> ListPublications() =>
            new ListPublicationsHandler(Db, Tenant).Handle(new ListPublicationsQuery(), default);

        public Task<PublicationDto> Rollback(Guid publicationId) =>
            new RollbackPublicationHandler(Db, Tenant, PointerCache, NullLogger<RollbackPublicationHandler>.Instance)
                .Handle(new RollbackPublicationCommand(publicationId), default);

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

    /// <summary>Một ảnh Library và <paramref name="clones"/> clone của nó trong shop (tree chỉ chứa id clone, `05`/MEDIA-001 #76).</summary>
    public static async Task<(Guid LibraryId, IReadOnlyList<Guid> CloneIds)> AddLibraryWithClonesAsync(string connectionString, Guid shopId, int clones)
    {
        await using var db = NewDb(connectionString, shopId);
        var library = MediaAsset.NewLibrary(shopId, $"shops/{shopId}/library/{Guid.NewGuid():N}.webp", 100, 100, 10, 0.5f, 0.5f, null, null);
        db.MediaAssets.Add(library);
        await db.SaveChangesAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < clones; i++)
        {
            var clone = MediaAsset.NewClone(library, $"shops/{shopId}/clone/{Guid.NewGuid():N}.webp", 100, 100, 10, "p,cover", 0.5f, 0.5f);
            db.MediaAssets.Add(clone);
            await db.SaveChangesAsync();
            ids.Add(clone.Id);
        }

        return (library.Id, ids);
    }

    /// <summary>Lưu cây vào draft của trang qua handler thật (đọc version hiện tại rồi ghi).</summary>
    public static async Task SaveTreeAsync(Request request, Guid pageId, string tree)
    {
        var draft = await request.GetDraft(pageId);
        await request.SaveDraft(pageId, tree, draft.Version);
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

    /// <summary>
    /// Mô phỏng một publish/rollback khác commit ngay TRƯỚC khi handler ghi: chạm dòng <c>website</c> (đổi <c>xmin</c>)
    /// mà không đổi giá trị nào (review I1 — <c>Website</c> có concurrency token).
    /// </summary>
    public sealed class BumpWebsiteXminInterceptor(string connectionString, Guid websiteId) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await using var other = NewDb(connectionString, null);
            await other.Database.ExecuteSqlInterpolatedAsync($"UPDATE website SET status = status WHERE id = {websiteId}", cancellationToken);
            return result;
        }
    }

    /// <summary>Mô phỏng tab khác publish xong ngay TRƯỚC khi handler ghi: chèn sẵn bản cùng <paramref name="version"/> (một lần).</summary>
    public sealed class InsertPublicationInterceptor(string connectionString, Guid shopId, Guid websiteId, int version) : SaveChangesInterceptor
    {
        private bool _done;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_done)
            {
                _done = true;
                await using var other = NewDb(connectionString, shopId);
                other.SitePublications.Add(SitePublication.Create(shopId, websiteId, version, "{}", null, null, DateTimeOffset.UtcNow));
                await other.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }

    /// <summary>Mô phỏng tab khác xoá mềm ảnh NGAY SAU khi handler kiểm ảnh hợp lệ (trước lúc nạp bảng media cho snapshot).</summary>
    public sealed class SoftDeleteAfterCheckMediaValidator(
        Vsite.Application.Media.Interfaces.IMediaReferenceValidator inner, string connectionString, Guid assetId)
        : Vsite.Application.Media.Interfaces.IMediaReferenceValidator
    {
        public async Task EnsureValidAsync(IReadOnlyCollection<Guid> treeImageIds, IReadOnlyCollection<Guid> businessImageIds, CancellationToken ct)
        {
            await inner.EnsureValidAsync(treeImageIds, businessImageIds, ct);
            if (treeImageIds.Contains(assetId) || businessImageIds.Contains(assetId))
            {
                await using var other = NewDb(connectionString, null);
                await other.Database.ExecuteSqlInterpolatedAsync($"UPDATE media_asset SET is_deleted = true WHERE id = {assetId}", ct);
            }
        }
    }
}
