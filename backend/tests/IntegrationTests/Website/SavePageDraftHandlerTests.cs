using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Website.Exceptions;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Website.Enums;
using static Vsite.IntegrationTests.Website.WebsiteHandlerKit;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// T7 + T9 (WEBSITE-001, R1, R2, R7, #71, #77) — GetPageDraft/SavePageDraft trên Postgres thật, validator Media THẬT
/// (không fake): đóng nợ "không có test nào chứng minh validator chạy trong luồng lưu" (plan §2 phát hiện 7).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SavePageDraftHandlerTests(PostgresFixture postgres) : IAsyncLifetime
{
    private string Cs => postgres.ConnectionString;

    public Task InitializeAsync() => MigrateAsync(Cs);

    public Task DisposeAsync() => Task.CompletedTask;

    private static string Children(string json) => JsonNode.Parse(json)!["children"]!.ToJsonString();

    // ---- GetPageDraft ----

    [Fact]
    public async Task GetDraft_returns_tree_version_dirty_updatedAt_and_page_kind()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);

        var draft = await request.GetDraft(seeded.Contact.Id);

        Assert.Equal(PageKind.System, draft.Page.Kind);
        Assert.Equal(SystemType.Contact, draft.Page.SystemType);
        Assert.Equal(seeded.Contact.Id, draft.Page.Id);
        Assert.True(draft.Version > 0);
        Assert.True(draft.IsDirty);
        Assert.True(draft.UpdatedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
        Assert.Equal(1, draft.Tree.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(JsonValueKind.Array, draft.Tree.GetProperty("children").ValueKind);
    }

    [Fact]
    public async Task GetDraft_of_another_shops_page_or_unknown_page_is_NotFound()
    {
        var a = await AddWebsiteAsync(Cs);
        var b = await AddWebsiteAsync(Cs);
        await using var requestA = new Request(Cs, a.ShopId);

        var foreign = await Assert.ThrowsAsync<NotFoundException>(() => requestA.GetDraft(b.Home.Id));
        var unknown = await Assert.ThrowsAsync<NotFoundException>(() => requestA.GetDraft(Guid.NewGuid()));

        Assert.Equal("PAGE_NOT_FOUND", foreign.ErrorCode);
        Assert.Equal(unknown.ErrorCode, foreign.ErrorCode); // không phân biệt "lạ" với "của shop khác"
        Assert.Equal(404, foreign.StatusCode);
    }

    [Fact]
    public async Task GetDraft_fails_closed_when_tenant_is_unresolved()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, shopId: null);

        var ex = await Assert.ThrowsAsync<ForbiddenAccessException>(() => request.GetDraft(seeded.Home.Id));

        Assert.Equal("TENANT_REQUIRED", ex.ErrorCode);
    }

    // ---- SavePageDraft: đường vui ----

    [Fact]
    public async Task Save_round_trips_returns_new_version_and_keeps_dirty()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var before = await request.GetDraft(seeded.Home.Id);
        var tree = TreeWith(RichText("c_abc12", "<p>Xin chào</p>"));

        var result = await request.SaveDraft(seeded.Home.Id, tree, before.Version);

        Assert.NotEqual(before.Version, result.Version);
        Assert.True(result.IsDirty);
        Assert.False(result.Sanitized);
        await using var reader = new Request(Cs, seeded.ShopId);
        var after = await reader.GetDraft(seeded.Home.Id);
        Assert.Equal(result.Version, after.Version);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(tree)!["children"], JsonNode.Parse(Children(after.Tree.GetRawText()))));
    }

    [Fact]
    public async Task Save_with_the_version_just_returned_works_again()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var draft = await request.GetDraft(seeded.Home.Id);

        var first = await request.SaveDraft(seeded.Home.Id, TreeWith(RichText("c_abc12", "<p>1</p>")), draft.Version);
        var second = await request.SaveDraft(seeded.Home.Id, TreeWith(RichText("c_abc12", "<p>2</p>")), first.Version);

        Assert.NotEqual(first.Version, second.Version);
    }

    // ---- R7: hai tab ----

    [Fact]
    public async Task Stale_version_from_second_tab_is_409_DRAFT_CONFLICT_with_current_version_and_actor()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var userB = Guid.NewGuid();
        await using var tabA = new Request(Cs, seeded.ShopId);
        await using var tabB = new Request(Cs, seeded.ShopId, userB);
        var read = await tabA.GetDraft(seeded.Home.Id);

        var saved = await tabB.SaveDraft(seeded.Home.Id, TreeWith(RichText("c_bbbbb", "<p>B</p>")), read.Version);
        var ex = await Assert.ThrowsAsync<DraftConflictException>(
            () => tabA.SaveDraft(seeded.Home.Id, TreeWith(RichText("c_aaaaa", "<p>A</p>")), read.Version));

        Assert.Equal("DRAFT_CONFLICT", ex.ErrorCode);
        Assert.Equal(409, ex.StatusCode);
        var info = Assert.IsType<DraftConflictParams>(ex.ErrorParams);
        Assert.Equal(saved.Version, info.CurrentVersion);
        Assert.Equal(userB, info.UpdatedByUserId);
        Assert.True(info.UpdatedAt > DateTimeOffset.UtcNow.AddMinutes(-5));

        // bản của B còn nguyên
        var current = await tabB.GetDraft(seeded.Home.Id);
        Assert.Contains("c_bbbbb", current.Tree.GetRawText());
        Assert.DoesNotContain("c_aaaaa", current.Tree.GetRawText());
    }

    [Fact]
    public async Task Two_parallel_saves_with_the_same_version_yield_exactly_one_success_and_a_409_not_a_500()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var setup = new Request(Cs, seeded.ShopId);
        var read = await setup.GetDraft(seeded.Home.Id);

        for (var round = 0; round < 5; round++)
        {
            var current = await setup.GetDraft(seeded.Home.Id);
            await using var a = new Request(Cs, seeded.ShopId);
            await using var b = new Request(Cs, seeded.ShopId);

            var results = await Task.WhenAll(
                Attempt(a.SaveDraft(seeded.Home.Id, TreeWith(RichText("c_aaaaa", $"<p>A{round}</p>")), current.Version)),
                Attempt(b.SaveDraft(seeded.Home.Id, TreeWith(RichText("c_bbbbb", $"<p>B{round}</p>")), current.Version)));

            Assert.Equal(1, results.Count(r => r is null));
            var failure = Assert.Single(results.OfType<AppException>());
            Assert.Equal("DRAFT_CONFLICT", failure.ErrorCode);
        }

        Assert.NotEqual(read.Version, (await setup.GetDraft(seeded.Home.Id)).Version);
    }

    [Fact]
    public async Task Concurrency_exception_at_SaveChanges_is_translated_to_DRAFT_CONFLICT()
    {
        // Version khớp lúc so sánh trong handler, nhưng ngay trước UPDATE có người khác ghi → xmin lệch → 0 dòng.
        var seeded = await AddWebsiteAsync(Cs);
        await using var read = new Request(Cs, seeded.ShopId);
        var draft = await read.GetDraft(seeded.Home.Id);
        await using var racing = new Request(Cs, seeded.ShopId, null, new BumpXminInterceptor(Cs, seeded.Home.Id));

        var ex = await Assert.ThrowsAsync<DraftConflictException>(
            () => racing.SaveDraft(seeded.Home.Id, TreeWith(RichText("c_aaaaa", "<p>A</p>")), draft.Version));

        Assert.Equal("DRAFT_CONFLICT", ex.ErrorCode);
        var info = Assert.IsType<DraftConflictParams>(ex.ErrorParams);
        Assert.NotEqual(draft.Version, info.CurrentVersion);
    }

    // ---- R2: tenant ----

    [Fact]
    public async Task Saving_another_shops_page_is_NotFound_and_changes_nothing()
    {
        var a = await AddWebsiteAsync(Cs);
        var b = await AddWebsiteAsync(Cs);
        await using var requestA = new Request(Cs, a.ShopId);
        await using var requestB = new Request(Cs, b.ShopId);
        var draftB = await requestB.GetDraft(b.Home.Id);

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => requestA.SaveDraft(b.Home.Id, TreeWith(RichText("c_aaaaa", "<p>hack</p>")), draftB.Version));

        Assert.Equal("PAGE_NOT_FOUND", ex.ErrorCode);
        Assert.Equal(draftB.Version, (await requestB.GetDraft(b.Home.Id)).Version);
    }

    // ---- R1: ảnh qua validator THẬT ----

    [Fact]
    public async Task Valid_clone_image_of_this_shop_is_accepted()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var clone = await AddAssetAsync(Cs, seeded.ShopId, "Clone");
        await using var request = new Request(Cs, seeded.ShopId);
        var draft = await request.GetDraft(seeded.Home.Id);

        var result = await request.SaveDraft(seeded.Home.Id, TreeWith(Hero("c_hero1", clone.ToString("D"))), draft.Version);

        Assert.NotEqual(draft.Version, result.Version);
    }

    [Theory]
    [InlineData("Library")]
    [InlineData("OtherShopClone")]
    [InlineData("DeletedClone")]
    [InlineData("Unknown")]
    public async Task Invalid_image_reference_is_422_MEDIA_INVALID_IMAGE_REFERENCE_and_draft_is_unchanged(string which)
    {
        var seeded = await AddWebsiteAsync(Cs);
        var other = await AddShopAsync(Cs);
        var imageId = which switch
        {
            "Library" => await AddAssetAsync(Cs, seeded.ShopId, "Library"),
            "OtherShopClone" => await AddAssetAsync(Cs, other, "Clone"),
            "DeletedClone" => await AddAssetAsync(Cs, seeded.ShopId, "DeletedClone"),
            _ => Guid.NewGuid(),
        };
        await using var request = new Request(Cs, seeded.ShopId);
        var draft = await request.GetDraft(seeded.Home.Id);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => request.SaveDraft(seeded.Home.Id, TreeWith(Hero("c_hero1", imageId.ToString("D"))), draft.Version));

        Assert.Equal("MEDIA_INVALID_IMAGE_REFERENCE", ex.ErrorCode);
        Assert.DoesNotContain(imageId.ToString("D"), ex.Message); // không lộ id có tồn tại hay không
        Assert.Equal(draft.Version, (await request.GetDraft(seeded.Home.Id)).Version);
    }

    [Theory]
    [InlineData("media_clone_1")]
    [InlineData("not-a-guid")]
    [InlineData("{00000000-0000-0000-0000-000000000001}")]
    [InlineData("00000000000000000000000000000001")]
    public async Task Image_id_that_is_not_a_D_format_guid_is_422_MEDIA_INVALID_IMAGE_REFERENCE(string imageId)
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var draft = await request.GetDraft(seeded.Home.Id);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => request.SaveDraft(seeded.Home.Id, TreeWith(Hero("c_hero1", imageId)), draft.Version));

        Assert.Equal("MEDIA_INVALID_IMAGE_REFERENCE", ex.ErrorCode);
    }

    [Fact]
    public async Task One_bad_image_among_good_ones_rejects_the_whole_tree()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var good = await AddAssetAsync(Cs, seeded.ShopId, "Clone");
        var library = await AddAssetAsync(Cs, seeded.ShopId, "Library");
        await using var request = new Request(Cs, seeded.ShopId);
        var draft = await request.GetDraft(seeded.Home.Id);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() => request.SaveDraft(
            seeded.Home.Id,
            TreeWith(Hero("c_hero1", good.ToString("D")), Gallery("c_gall1", library.ToString("D"))),
            draft.Version));

        Assert.Equal("MEDIA_INVALID_IMAGE_REFERENCE", ex.ErrorCode);
    }

    // ---- richText: sanitize / unsafe ----

    [Fact]
    public async Task Dirty_but_harmless_richtext_is_saved_clean_and_flagged_sanitized()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var draft = await request.GetDraft(seeded.Home.Id);
        const string dirty = "<p class=\"x\" style=\"color:red\"><span style=\"white-space: pre-wrap\">Xin chào</span></p>";

        var result = await request.SaveDraft(seeded.Home.Id, TreeWith(RichText("c_abc12", dirty)), draft.Version);

        Assert.True(result.Sanitized);
        var stored = await request.GetDraft(seeded.Home.Id);
        var content = JsonNode.Parse(stored.Tree.GetRawText())!["children"]![0]!["props"]!["content"]!.GetValue<string>();
        Assert.Contains("Xin chào", content);
        Assert.DoesNotContain("style", content);
        Assert.DoesNotContain("class", content);
    }

    [Fact]
    public async Task Unsafe_richtext_is_422_TREE_INVALID_with_RICHTEXT_UNSAFE_error_list()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var draft = await request.GetDraft(seeded.Home.Id);

        var ex = await Assert.ThrowsAsync<DocumentInvalidException>(() => request.SaveDraft(
            seeded.Home.Id, TreeWith(RichText("c_abc12", "<p>a</p><script>alert(1)</script>")), draft.Version));

        Assert.Equal("TREE_INVALID", ex.ErrorCode);
        Assert.Equal(422, ex.StatusCode);
        var error = Assert.Single(ex.Details);
        Assert.Equal("RICHTEXT_UNSAFE", error.Code);
        Assert.Equal("/children/0/props/content", error.Path);
        Assert.Equal(draft.Version, (await request.GetDraft(seeded.Home.Id)).Version);
    }

    [Fact]
    public async Task Unsupported_schema_version_is_422_with_that_problem_code_and_garbage_is_TREE_INVALID()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var draft = await request.GetDraft(seeded.Home.Id);

        var future = await Assert.ThrowsAsync<DocumentInvalidException>(() => request.SaveDraft(
            seeded.Home.Id, """{"schemaVersion":999,"id":"root","type":"Page","children":[]}""", draft.Version));
        var garbage = await Assert.ThrowsAsync<DocumentInvalidException>(
            () => request.SaveDraft(seeded.Home.Id, "{not json", draft.Version));

        Assert.Equal("SCHEMA_VERSION_UNSUPPORTED", future.ErrorCode);
        Assert.Equal("TREE_INVALID", garbage.ErrorCode);
        Assert.Equal(422, garbage.StatusCode);
    }

    [Fact]
    public async Task Oversized_body_is_rejected_with_a_tree_error_before_deep_validation()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var draft = await request.GetDraft(seeded.Home.Id);
        var huge = TreeWith(RichText("c_abc12", new string('a', 1_100_000)));

        var ex = await Assert.ThrowsAsync<DocumentInvalidException>(() => request.SaveDraft(seeded.Home.Id, huge, draft.Version));

        Assert.Equal("TREE_INVALID", ex.ErrorCode);
        Assert.Contains(ex.Details, e => e.Code == "TREE_BODY_TOO_LARGE");
    }

    [Fact]
    public async Task Errors_are_also_exposed_as_path_keyed_dictionary_for_the_generic_problem_shape()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var draft = await request.GetDraft(seeded.Home.Id);

        var ex = await Assert.ThrowsAsync<DocumentInvalidException>(() => request.SaveDraft(
            seeded.Home.Id, TreeWith(RichText("c_abc12", "<script>x</script>")), draft.Version));

        Assert.Contains("/children/0/props/content", ex.Errors.Keys);
        var detail = Assert.IsType<DocumentInvalidParams>(ex.ErrorParams);
        Assert.Equal(ex.Details, detail.Errors);
        Assert.False(detail.Truncated);
    }

    // ---- System page ----

    [Fact]
    public async Task System_page_content_edit_is_allowed()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var draft = await request.GetDraft(seeded.Contact.Id);
        var tree = JsonNode.Parse(draft.Tree.GetRawText())!;
        tree["children"]![0]!["props"]!["content"] = "<p>Số điện thoại mới 0900 000 000</p>";

        var result = await request.SaveDraft(seeded.Contact.Id, tree.ToJsonString(), draft.Version);

        Assert.NotEqual(draft.Version, result.Version);
        Assert.Contains("0900 000 000", (await request.GetDraft(seeded.Contact.Id)).Tree.GetRawText());
    }

    [Fact]
    public async Task System_page_structure_change_is_422_TREE_INVALID_with_SYSTEM_PAGE_STRUCTURE_CHANGED()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var draft = await request.GetDraft(seeded.Contact.Id);
        var tree = JsonNode.Parse(draft.Tree.GetRawText())!.AsObject();
        tree["children"]!.AsArray().Add(JsonNode.Parse(RichText("c_extra1", "<p>thêm</p>")));

        var added = await Assert.ThrowsAsync<DocumentInvalidException>(
            () => request.SaveDraft(seeded.Contact.Id, tree.ToJsonString(), draft.Version));
        var emptied = await Assert.ThrowsAsync<DocumentInvalidException>(
            () => request.SaveDraft(seeded.Contact.Id, TreeWith(), draft.Version));

        Assert.Equal("TREE_INVALID", added.ErrorCode);
        Assert.Contains(added.Details, e => e.Code == "SYSTEM_PAGE_STRUCTURE_CHANGED");
        Assert.Contains(emptied.Details, e => e.Code == "SYSTEM_PAGE_STRUCTURE_CHANGED");
        Assert.Equal(draft.Version, (await request.GetDraft(seeded.Contact.Id)).Version);
    }

    [Fact]
    public async Task Composable_page_may_change_structure_freely()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var draft = await request.GetDraft(seeded.Home.Id);
        var one = await request.SaveDraft(seeded.Home.Id, TreeWith(RichText("c_aaaaa", "<p>1</p>"), RichText("c_bbbbb", "<p>2</p>")), draft.Version);

        var two = await request.SaveDraft(seeded.Home.Id, TreeWith(RichText("c_bbbbb", "<p>2</p>")), one.Version);

        Assert.NotEqual(one.Version, two.Version);
    }

    [Fact]
    public async Task Unresolved_tenant_fails_closed_on_save()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, shopId: null);

        var ex = await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => request.SaveDraft(seeded.Home.Id, TreeWith(), 1));

        Assert.Equal("TENANT_REQUIRED", ex.ErrorCode);
    }

    [Fact]
    public async Task Draft_of_a_soft_deleted_page_is_not_reachable()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var page = await request.CreatePage(new Vsite.Application.Website.Commands.CreatePage.CreatePageCommand("tam", "Tạm"));
        var draft = await request.GetDraft(page.Id);
        await request.DeletePage(page.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => request.GetDraft(page.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => request.SaveDraft(page.Id, TreeWith(), draft.Version));
        await using var verify = NewDb(Cs, seeded.ShopId);
        Assert.NotNull(await verify.PageDrafts.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == page.Id));
    }

    private static async Task<AppException?> Attempt<T>(Task<T> task)
    {
        try
        {
            await task;
            return null;
        }
        catch (AppException ex)
        {
            return ex;
        }
    }
}
