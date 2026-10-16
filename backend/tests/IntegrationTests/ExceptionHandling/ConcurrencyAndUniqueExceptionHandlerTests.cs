using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vsite.Api.ExceptionHandling;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Shop.Entities;
using Vsite.Domain.Shop.Enums;
using Vsite.Infrastructure.Persistence;
using static Vsite.IntegrationTests.Website.WebsiteHandlerKit;

namespace Vsite.IntegrationTests.ExceptionHandling;

/// <summary>
/// T9 (WEBSITE-001, R15, REFACTOR-DB-001 #1/#2) — handler toàn cục cho DbUpdateConcurrencyException và 23505.
/// Phần unit không cần Docker; hai test cuối dùng Postgres thật để chứng minh 23505 THẬT (không dựng tay) không rò constraint.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ConcurrencyAndUniqueExceptionHandlerTests(PostgresFixture postgres)
{
    private readonly ConcurrencyAndUniqueExceptionHandler _handler = new();

    private static async Task<(bool Handled, int Status, JsonElement Body, string Raw)> RunAsync(
        ConcurrencyAndUniqueExceptionHandler handler, Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var raw = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var body = raw.Length == 0 ? default : JsonDocument.Parse(raw).RootElement.Clone();
        return (handled, context.Response.StatusCode, body, raw);
    }

    private static PostgresException Pg(string sqlState, string constraint = "ux_secret_constraint_name") =>
        new("duplicate key value violates unique constraint \"ux_secret_constraint_name\"", "ERROR", "ERROR", sqlState,
            detail: "Key (shop_id)=(11111111-1111-1111-1111-111111111111) already exists.",
            schemaName: "public", tableName: "website", constraintName: constraint);

    [Fact]
    public async Task DbUpdateConcurrencyException_is_409_CONCURRENCY_CONFLICT()
    {
        var (handled, status, body, _) = await RunAsync(_handler, new DbUpdateConcurrencyException("0 rows affected"));

        Assert.True(handled);
        Assert.Equal(409, status);
        Assert.Equal("CONCURRENCY_CONFLICT", body.GetProperty("error_code").GetString());
        Assert.Equal(409, body.GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData("ux_secret_constraint_name")]
    [InlineData("pk_website")]
    [InlineData("ux_page_slug")]
    public async Task Any_23505_is_409_CONFLICT_UNIQUE_and_never_leaks_constraint_table_or_values(string constraint)
    {
        var exception = new DbUpdateException("save failed: INSERT INTO website ...", Pg("23505", constraint));

        var (handled, status, body, raw) = await RunAsync(_handler, exception);

        Assert.True(handled);
        Assert.Equal(409, status);
        Assert.Equal("CONFLICT_UNIQUE", body.GetProperty("error_code").GetString());
        Assert.DoesNotContain(constraint, raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("website", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("11111111", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("duplicate key", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Bare_PostgresException_23505_is_also_handled()
    {
        var (handled, status, body, _) = await RunAsync(_handler, Pg("23505"));

        Assert.True(handled);
        Assert.Equal(409, status);
        Assert.Equal("CONFLICT_UNIQUE", body.GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task Wrapped_23505_deeper_in_the_inner_exception_chain_is_handled()
    {
        var wrapped = new InvalidOperationException("outer", new DbUpdateException("mid", Pg("23505")));

        var (handled, status, _, _) = await RunAsync(_handler, wrapped);

        Assert.True(handled);
        Assert.Equal(409, status);
    }

    [Theory]
    [InlineData("23503")] // FK violation — chỗ khác lo, không phải 409 chung
    [InlineData("23514")]
    [InlineData("40001")]
    public async Task Other_sql_states_are_not_handled(string sqlState)
    {
        var (handled, _, _, raw) = await RunAsync(_handler, new DbUpdateException("x", Pg(sqlState)));

        Assert.False(handled);
        Assert.Equal("", raw);
    }

    [Fact]
    public async Task AppException_and_unrelated_exceptions_are_left_to_other_handlers()
    {
        Assert.False((await RunAsync(_handler, new ConflictException("X", "x"))).Handled);
        Assert.False((await RunAsync(_handler, new InvalidOperationException("boom"))).Handled);
        Assert.False((await RunAsync(_handler, new DbUpdateException("no inner"))).Handled);
    }

    [Fact]
    public async Task Existing_AppExceptionHandler_is_unaffected_and_keeps_the_problem_shape()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await new AppExceptionHandler().TryHandleAsync(context, new ConflictException("PAGE_SLUG_TAKEN", "trùng"), CancellationToken.None);

        Assert.True(handled);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = JsonDocument.Parse(await new StreamReader(context.Response.Body).ReadToEndAsync()).RootElement;
        Assert.Equal("PAGE_SLUG_TAKEN", body.GetProperty("error_code").GetString());
        Assert.Equal(409, context.Response.StatusCode);
    }

    // ---- 23505 THẬT từ Postgres ----

    [Fact]
    public async Task Real_unique_violation_from_postgres_maps_to_409_without_the_constraint_name()
    {
        await MigrateAsync(postgres.ConnectionString);
        var shopId = await AddShopAsync(postgres.ConnectionString);
        await using var db = NewDb(postgres.ConnectionString, shopId);
        db.Websites.Add(Vsite.Domain.Website.Entities.Website.Create(shopId));
        await db.SaveChangesAsync();
        await using var duplicate = NewDb(postgres.ConnectionString, shopId);
        duplicate.Websites.Add(Vsite.Domain.Website.Entities.Website.Create(shopId));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
        var (handled, status, body, raw) = await RunAsync(_handler, ex);

        Assert.True(handled);
        Assert.Equal(409, status);
        Assert.Equal("CONFLICT_UNIQUE", body.GetProperty("error_code").GetString());
        Assert.DoesNotContain("ux_website_shop", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(shopId.ToString(), raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Real_concurrency_exception_from_postgres_maps_to_409_CONCURRENCY_CONFLICT()
    {
        await MigrateAsync(postgres.ConnectionString);
        var seeded = await AddWebsiteAsync(postgres.ConnectionString);
        await using var db = NewDb(postgres.ConnectionString, seeded.ShopId);
        var draft = await db.PageDrafts.SingleAsync(d => d.Id == seeded.Home.Id);
        await using var other = NewDb(postgres.ConnectionString, seeded.ShopId);
        (await other.PageDrafts.SingleAsync(d => d.Id == seeded.Home.Id)).Replace(TreeWith(RichText("c_other", "<p>x</p>")));
        await other.SaveChangesAsync();
        draft.Replace(TreeWith(RichText("c_mine1", "<p>y</p>")));

        var ex = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
        var (handled, status, body, _) = await RunAsync(_handler, ex);

        Assert.True(handled);
        Assert.Equal(409, status);
        Assert.Equal("CONCURRENCY_CONFLICT", body.GetProperty("error_code").GetString());
    }
}
