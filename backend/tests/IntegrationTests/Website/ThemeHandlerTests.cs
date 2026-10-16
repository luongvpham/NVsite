using System.Text.Json.Nodes;
using Vsite.Application.Website.Exceptions;
using Vsite.Domain.Exceptions;
using static Vsite.IntegrationTests.Website.WebsiteHandlerKit;

namespace Vsite.IntegrationTests.Website;

/// <summary>T8 — GetTheme/UpdateTheme (Flow 4, #94, #97). THEME_INVALID dùng cùng dạng errors[] {path,code,message}. Postgres thật.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ThemeHandlerTests(PostgresFixture postgres) : IAsyncLifetime
{
    private string Cs => postgres.ConnectionString;

    public Task InitializeAsync() => MigrateAsync(Cs);

    public Task DisposeAsync() => Task.CompletedTask;

    private static string Tokens(Action<JsonObject> edit)
    {
        var tokens = JsonNode.Parse(S.ThemeTokens.DefaultsJson)!.AsObject();
        edit(tokens);
        return tokens.ToJsonString();
    }

    [Fact]
    public async Task GetTheme_returns_defaults_after_creation()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);

        var theme = await request.GetTheme();

        Assert.Null(theme.PresetKey);
        Assert.Equal("#7c3aed", theme.Tokens.GetProperty("colors").GetProperty("primary").GetString());
        Assert.True(theme.UpdatedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task GetTheme_before_website_exists_is_404_WEBSITE_NOT_FOUND()
    {
        var shopId = await AddShopAsync(Cs);
        await using var request = new Request(Cs, shopId);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => request.GetTheme());

        Assert.Equal("WEBSITE_NOT_FOUND", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateTheme_writes_the_whole_document_and_preset_key()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var tokens = Tokens(t =>
        {
            t["colors"]!["primary"] = "#ff0000";
            t["typography"]!["headingFont"] = "Lora";
        });

        var result = await request.UpdateTheme(tokens, "spa-lavender");

        Assert.Equal("spa-lavender", result.PresetKey);
        await using var verify = new Request(Cs, seeded.ShopId);
        var read = await verify.GetTheme();
        Assert.Equal("#ff0000", read.Tokens.GetProperty("colors").GetProperty("primary").GetString());
        Assert.Equal("Lora", read.Tokens.GetProperty("typography").GetProperty("headingFont").GetString());
        Assert.Equal("spa-lavender", read.PresetKey);
    }

    [Theory]
    [InlineData("not-a-color")]
    [InlineData("#12345")]
    [InlineData("red")]
    public async Task UpdateTheme_bad_color_is_422_THEME_INVALID_with_a_path(string color)
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);

        var ex = await Assert.ThrowsAsync<DocumentInvalidException>(
            () => request.UpdateTheme(Tokens(t => t["colors"]!["primary"] = color)));

        Assert.Equal("THEME_INVALID", ex.ErrorCode);
        Assert.Equal(422, ex.StatusCode);
        Assert.Contains(ex.Details, e => e.Path.Contains("primary", StringComparison.Ordinal));
        Assert.All(ex.Details, e => Assert.False(string.IsNullOrEmpty(e.Code)));
        Assert.Equal("#7c3aed", (await request.GetTheme()).Tokens.GetProperty("colors").GetProperty("primary").GetString());
    }

    [Fact]
    public async Task UpdateTheme_font_outside_whitelist_is_422()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);

        var ex = await Assert.ThrowsAsync<DocumentInvalidException>(
            () => request.UpdateTheme(Tokens(t => t["typography"]!["headingFont"] = "Comic Sans MS")));

        Assert.Equal("THEME_INVALID", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateTheme_newer_schema_version_is_422_SCHEMA_VERSION_UNSUPPORTED_and_garbage_is_THEME_INVALID()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);

        var future = await Assert.ThrowsAsync<DocumentInvalidException>(
            () => request.UpdateTheme(Tokens(t => t["schemaVersion"] = 999)));
        var missing = await Assert.ThrowsAsync<DocumentInvalidException>(
            () => request.UpdateTheme(Tokens(t => t.Remove("schemaVersion"))));
        var garbage = await Assert.ThrowsAsync<DocumentInvalidException>(() => request.UpdateTheme("{oops"));

        Assert.Equal("SCHEMA_VERSION_UNSUPPORTED", future.ErrorCode);
        Assert.Equal("THEME_INVALID", missing.ErrorCode);
        Assert.Equal("THEME_INVALID", garbage.ErrorCode);
    }

    [Fact]
    public async Task UpdateTheme_rejects_duplicate_keys_and_oversize_documents_without_throwing_500()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);

        var dup = await Assert.ThrowsAsync<DocumentInvalidException>(
            () => request.UpdateTheme("""{"schemaVersion":1,"schemaVersion":1}"""));
        var big = await Assert.ThrowsAsync<DocumentInvalidException>(
            () => request.UpdateTheme(Tokens(t => t["pad"] = new string('a', 100_000))));

        Assert.Equal("THEME_INVALID", dup.ErrorCode);
        Assert.Equal("THEME_INVALID", big.ErrorCode);
    }

    [Fact]
    public async Task UpdateTheme_preset_key_longer_than_50_is_rejected_by_the_command_validator()
    {
        var validator = new Vsite.Application.Website.Commands.UpdateTheme.UpdateThemeValidator();

        Assert.False(validator.Validate(new Vsite.Application.Website.Commands.UpdateTheme.UpdateThemeCommand(new string('a', 51), "{}")).IsValid);
        Assert.True(validator.Validate(new Vsite.Application.Website.Commands.UpdateTheme.UpdateThemeCommand(new string('a', 50), "{}")).IsValid);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Theme_of_another_shop_is_never_touched()
    {
        var a = await AddWebsiteAsync(Cs);
        var b = await AddWebsiteAsync(Cs);
        await using var requestA = new Request(Cs, a.ShopId);
        await using var requestB = new Request(Cs, b.ShopId);

        await requestA.UpdateTheme(Tokens(t => t["colors"]!["primary"] = "#00ff00"));

        Assert.Equal("#7c3aed", (await requestB.GetTheme()).Tokens.GetProperty("colors").GetProperty("primary").GetString());
    }

    [Fact]
    public async Task Unresolved_tenant_fails_closed()
    {
        await using var request = new Request(Cs, shopId: null);

        Assert.Equal("TENANT_REQUIRED", (await Assert.ThrowsAsync<ForbiddenAccessException>(() => request.GetTheme())).ErrorCode);
        Assert.Equal("TENANT_REQUIRED", (await Assert.ThrowsAsync<ForbiddenAccessException>(() => request.UpdateTheme("{}"))).ErrorCode);
    }
}
