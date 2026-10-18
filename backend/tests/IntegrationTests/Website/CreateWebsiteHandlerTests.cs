using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Website.Seeding;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Shop.Enums;
using Vsite.Domain.Website.Enums;
using static Vsite.IntegrationTests.Website.WebsiteHandlerKit;

namespace Vsite.IntegrationTests.Website;

/// <summary>T6 (WEBSITE-001, R11, #46, #98) — tạo website trắng + System page seed. Postgres thật (Testcontainers).</summary>
[Collection(PostgresCollection.Name)]
public sealed partial class CreateWebsiteHandlerTests(PostgresFixture postgres) : IAsyncLifetime
{
    public Task InitializeAsync() => MigrateAsync(postgres.ConnectionString);

    public Task DisposeAsync() => Task.CompletedTask;

    [GeneratedRegex(@"^c_[a-z0-9]{5}$")]
    private static partial Regex NodeIdPattern();

    [Fact]
    public async Task Creates_website_theme_three_pages_and_three_drafts_in_one_go()
    {
        var shopId = await AddShopAsync(postgres.ConnectionString);
        await using var request = new Request(postgres.ConnectionString, shopId);

        var dto = await request.CreateWebsite(shopId);

        Assert.Equal(WebsiteStatus.Draft, dto.Status);
        Assert.Equal("vi", dto.DefaultLocale);
        Assert.Equal(["vi"], dto.Locales);

        await using var verify = NewDb(postgres.ConnectionString, shopId);
        var pages = await verify.Pages.Where(p => p.WebsiteId == dto.Id).OrderBy(p => p.SortOrder).ToListAsync();
        Assert.Equal(3, pages.Count);
        var pageIds = pages.Select(p => p.Id).ToList();
        Assert.Equal(3, await verify.PageDrafts.CountAsync(d => pageIds.Contains(d.Id)));
        Assert.Equal(1, await verify.Themes.CountAsync(t => t.Id == dto.Id));

        var home = pages[0];
        Assert.Equal(PageKind.Composable, home.Kind);
        Assert.Equal("", home.Slug);
        Assert.Null(home.SystemType);
        Assert.Equal(dto.HomePageId, home.Id);

        var contact = pages.Single(p => p.SystemType == SystemType.Contact);
        Assert.Equal(PageKind.System, contact.Kind);
        Assert.Equal("lien-he", contact.Slug);
        Assert.False(string.IsNullOrWhiteSpace(contact.Title));

        var notFound = pages.Single(p => p.SystemType == SystemType.NotFound);
        Assert.Null(notFound.Slug);
        Assert.True(notFound.NoIndex);
        Assert.Equal(PageKind.System, notFound.Kind);

        // G12: SortOrder server cấp, tăng dần, không trùng.
        Assert.Equal(pages.Select(p => p.SortOrder).OrderBy(x => x), pages.Select(p => p.SortOrder));
        Assert.Equal(3, pages.Select(p => p.SortOrder).Distinct().Count());

        // G11: draft IsDirty = true ngay từ seed.
        Assert.All(await verify.PageDrafts.Where(d => pageIds.Contains(d.Id)).ToListAsync(), d => Assert.True(d.IsDirty));
    }

    [Fact]
    public async Task Seeded_trees_pass_PageTreeValidator_for_their_page_kind_with_server_generated_node_ids()
    {
        var seeded = await AddWebsiteAsync(postgres.ConnectionString);
        await using var request = new Request(postgres.ConnectionString, seeded.ShopId);

        foreach (var page in seeded.Pages)
        {
            var draft = await request.GetDraft(page.Id);
            var tree = draft.Tree.GetRawText();
            var result = S.Validator.Validate(tree, page.Kind);
            Assert.True(result.IsValid, $"{page.Title}: {string.Join(";", result.Errors.Select(e => $"{e.Path} {e.Code}"))}");
            Assert.False(result.Sanitized, "seed phải đã sạch sẵn");

            var root = JsonNode.Parse(tree)!.AsObject();
            foreach (var child in root["children"]!.AsArray())
            {
                Assert.Matches(NodeIdPattern(), child!["id"]!.GetValue<string>());
            }
        }

        // Trang chủ tree rỗng; Contact/NotFound = Page > RichText.
        Assert.Empty(JsonNode.Parse((await request.GetDraft(seeded.Home.Id)).Tree.GetRawText())!["children"]!.AsArray());
        foreach (var system in new[] { seeded.Contact, seeded.NotFound })
        {
            var children = JsonNode.Parse((await request.GetDraft(system.Id)).Tree.GetRawText())!["children"]!.AsArray();
            Assert.Equal("RichText", Assert.Single(children)!["type"]!.GetValue<string>());
        }
    }

    [Fact]
    public async Task Theme_tokens_equal_catalog_defaults_and_pass_tokens_validator()
    {
        var seeded = await AddWebsiteAsync(postgres.ConnectionString);
        await using var request = new Request(postgres.ConnectionString, seeded.ShopId);

        var theme = await request.GetTheme();

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(S.ThemeTokens.DefaultsJson), JsonNode.Parse(theme.Tokens.GetRawText())));
        Assert.True(S.ThemeValidator.Validate(theme.Tokens.GetRawText()).IsValid);
    }

    [Fact]
    public async Task ExternalOnly_shop_is_rejected_with_422_and_nothing_is_created()
    {
        var shopId = await AddShopAsync(postgres.ConnectionString, ShopKind.ExternalOnly);
        await using var request = new Request(postgres.ConnectionString, shopId);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() => request.CreateWebsite(shopId));

        Assert.Equal("WEBSITE_REQUIRES_HOSTED_SHOP", ex.ErrorCode);
        await using var verify = NewDb(postgres.ConnectionString, shopId);
        Assert.Empty(await verify.Websites.ToListAsync());
    }

    [Fact]
    public async Task Second_create_returns_409_WEBSITE_ALREADY_EXISTS()
    {
        var seeded = await AddWebsiteAsync(postgres.ConnectionString);
        await using var request = new Request(postgres.ConnectionString, seeded.ShopId);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => request.CreateWebsite(seeded.ShopId));

        Assert.Equal("WEBSITE_ALREADY_EXISTS", ex.ErrorCode);
    }

    [Fact]
    public async Task Two_parallel_creates_yield_exactly_one_success_and_one_409()
    {
        var shopId = await AddShopAsync(postgres.ConnectionString);
        await using var a = new Request(postgres.ConnectionString, shopId);
        await using var b = new Request(postgres.ConnectionString, shopId);

        var results = await Task.WhenAll(Attempt(a, shopId), Attempt(b, shopId));

        Assert.Equal(1, results.Count(r => r is null));
        var failure = Assert.Single(results.OfType<AppException>());
        Assert.Equal("WEBSITE_ALREADY_EXISTS", failure.ErrorCode);
        await using var verify = NewDb(postgres.ConnectionString, shopId);
        Assert.Equal(1, await verify.Websites.CountAsync());
        Assert.Equal(3, await verify.Pages.CountAsync());
    }

    [Fact]
    public async Task Unknown_shop_and_shop_of_another_tenant_return_NotFound()
    {
        var mine = await AddShopAsync(postgres.ConnectionString);
        var other = await AddShopAsync(postgres.ConnectionString);

        await using var request = new Request(postgres.ConnectionString, mine);

        var unknown = await Assert.ThrowsAsync<NotFoundException>(() => request.CreateWebsite(Guid.NewGuid()));
        var foreign = await Assert.ThrowsAsync<NotFoundException>(() => request.CreateWebsite(other));

        Assert.Equal("SHOP_NOT_FOUND", unknown.ErrorCode);
        Assert.Equal("SHOP_NOT_FOUND", foreign.ErrorCode);
    }

    [Fact]
    public async Task Unresolved_tenant_fails_closed()
    {
        var shopId = await AddShopAsync(postgres.ConnectionString);
        await using var request = new Request(postgres.ConnectionString, shopId: null);

        var ex = await Assert.ThrowsAsync<ForbiddenAccessException>(() => request.CreateWebsite(shopId));

        Assert.Equal("TENANT_REQUIRED", ex.ErrorCode);
    }

    // ---- WebsiteSeeder.EnsureSystemPages: idempotent, chỉ thêm cái thiếu ----

    [Fact]
    public void EnsureSystemPages_adds_only_missing_seedOnCreate_types()
    {
        var shop = Guid.NewGuid();
        var website = Guid.NewGuid();

        var all = S.Seeder.EnsureSystemPages(shop, website, [], startSortOrder: 1);
        Assert.Equal(
            [SystemType.Contact, SystemType.NotFound],
            all.Pages.Select(p => p.SystemType!.Value).OrderBy(t => t));
        Assert.Equal(all.Pages.Count, all.Drafts.Count);
        Assert.Equal([1, 2], all.Pages.Select(p => p.SortOrder).OrderBy(x => x));

        var onlyNotFound = S.Seeder.EnsureSystemPages(shop, website, [SystemType.Contact], startSortOrder: 5);
        Assert.Equal(SystemType.NotFound, Assert.Single(onlyNotFound.Pages).SystemType);
        Assert.Equal(5, onlyNotFound.Pages[0].SortOrder);

        Assert.Empty(S.Seeder.EnsureSystemPages(shop, website, [SystemType.Contact, SystemType.NotFound], startSortOrder: 9).Pages);
        // loại có seedOnCreate = false (Booking...) không bao giờ được seed
        Assert.DoesNotContain(all.Pages, p => p.SystemType == SystemType.Booking);
    }

    [Fact]
    public void Node_ids_match_envelope_pattern_and_are_not_constant()
    {
        var ids = Enumerable.Range(0, 200).Select(_ => NodeIds.New()).ToList();

        Assert.All(ids, id => Assert.Matches(NodeIdPattern(), id));
        Assert.True(ids.Distinct().Count() > 150);
    }

    private static async Task<AppException?> Attempt(Request request, Guid shopId)
    {
        try
        {
            await request.CreateWebsite(shopId);
            return null;
        }
        catch (AppException ex)
        {
            return ex;
        }
    }
}
