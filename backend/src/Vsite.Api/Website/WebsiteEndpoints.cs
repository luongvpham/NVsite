using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Vsite.Api.ExceptionHandling;
using Vsite.Api.Tenancy;
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
using Vsite.Domain.Authorization;
using Vsite.Domain.Exceptions;

namespace Vsite.Api.Website;

/// <summary>
/// T10, WEBSITE-001 — 10 endpoint dưới <c>/api/shops/{shopId}/website</c>. MỌI route (đọc lẫn ghi) gắn
/// <c>RequireShopOwner("WEBSITE_OWNER_REQUIRED", …)</c> (#100): một Customer/Manager/Staff có token global không đọc/ghi được
/// website. <c>ShopId</c> chỉ đến từ route (filter set <c>ITenantContext</c>), không bao giờ từ body (#21.4, #99).
///
/// Body của <c>savePageDraft</c>/<c>updateTheme</c> được đọc THÔ trong handler (không để Minimal API bind JsonElement) vì argument
/// binding chạy trước endpoint filter nên không đặt kịp giới hạn kích thước; đọc thủ công cho phép đặt giới hạn ngay trước lần đọc
/// đầu tiên (cùng lý do <c>MediaEndpoints</c> đọc form thủ công) và giữ nguyên văn tree cho validator.
/// </summary>
public static class WebsiteEndpoints
{
    /// <summary>Phần bọc <c>{"tree":…,"version":…}</c> ngoài tree — cộng vào <see cref="PageTreeLimits.MaxBodyBytes"/>.</summary>
    private const int EnvelopeSlackBytes = 4096;

    /// <summary>Body theme nhỏ (validator tự giới hạn tokens 16 KB) — 64 KB là biên rộng cho phần bọc.</summary>
    private const int MaxThemeBodyBytes = 64 * 1024;

    public static IEndpointRouteBuilder MapWebsiteEndpoints(this IEndpointRouteBuilder app)
    {
        var website = app.MapGroup("/shops/{shopId:guid}/website").WithTags("Website").WithGroupName("website");

        website.MapPost("", async (Guid shopId, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreateWebsiteCommand(shopId), ct);
            return Results.Created($"/api/shops/{shopId}/website", created);
        })
            .WithName("createWebsite")
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireOwner()
            .Produces<WebsiteDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        website.MapGet("", async (Guid shopId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetWebsiteQuery(), ct)))
            .WithName("getWebsite")
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireOwner()
            .Produces<WebsiteDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        website.MapGet("/pages", async (Guid shopId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListPagesQuery(), ct)))
            .WithName("listPages")
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireOwner()
            .Produces<IReadOnlyList<PageDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        website.MapPost("/pages", async (Guid shopId, CreatePageRequest body, ISender sender, CancellationToken ct) =>
        {
            var page = await sender.Send(
                new CreatePageCommand(body.Slug, body.Title, body.SeoTitle, body.SeoDescription, body.OgImageId, body.NoIndex, body.IsEnabled), ct);
            return Results.Created($"/api/shops/{shopId}/website/pages/{page.Id}", page);
        })
            .WithName("createPage")
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireOwner()
            .Produces<PageDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        // PUT thay toàn bộ metadata (không PATCH — ruling WEBSITE-001); riêng slug null = không đổi.
        website.MapPut("/pages/{pageId:guid}", async (Guid shopId, Guid pageId, UpdatePageRequest body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new UpdatePageCommand(pageId, body.Slug, body.Title, body.SeoTitle, body.SeoDescription, body.OgImageId, body.NoIndex, body.IsEnabled), ct)))
            .WithName("updatePage")
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireOwner()
            .Produces<PageDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        website.MapDelete("/pages/{pageId:guid}", async (Guid shopId, Guid pageId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeletePageCommand(pageId), ct);
            return Results.NoContent();
        })
            .WithName("deletePage")
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireOwner()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        website.MapGet("/pages/{pageId:guid}/draft", async (Guid shopId, Guid pageId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetPageDraftQuery(pageId), ct)))
            .WithName("getPageDraft")
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireOwner()
            .Produces<PageDraftDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        website.MapPut("/pages/{pageId:guid}/draft", async (
            Guid shopId, Guid pageId, HttpRequest request, PageTreeLimits limits, ISender sender, CancellationToken ct) =>
        {
            var bytes = await ReadBoundedBodyAsync(request, (long)limits.MaxBodyBytes + EnvelopeSlackBytes, ct);
            var (treeJson, version) = ParseDraftEnvelope(bytes);
            return Results.Ok(await sender.Send(new SavePageDraftCommand(pageId, treeJson, version), ct));
        })
            .WithName("savePageDraft")
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireOwner()
            .WithMetadata(new RequestBodyTooLargeErrorMetadata("TREE_BODY_TOO_LARGE", "Nội dung trang vượt quá giới hạn dung lượng cho phép."))
            .Accepts<SavePageDraftRequest>("application/json")
            .Produces<SavePageDraftResultDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        website.MapGet("/theme", async (Guid shopId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetThemeQuery(), ct)))
            .WithName("getTheme")
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireOwner()
            .Produces<ThemeDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        website.MapPut("/theme", async (Guid shopId, HttpRequest request, ISender sender, CancellationToken ct) =>
        {
            var bytes = await ReadBoundedBodyAsync(request, MaxThemeBodyBytes, ct);
            var (presetKey, tokensJson) = ParseThemeEnvelope(bytes);
            return Results.Ok(await sender.Send(new UpdateThemeCommand(presetKey, tokensJson), ct));
        })
            .WithName("updateTheme")
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireOwner()
            .WithMetadata(new RequestBodyTooLargeErrorMetadata("THEME_BODY_TOO_LARGE", "Nội dung theme vượt quá giới hạn dung lượng cho phép."))
            .Accepts<UpdateThemeRequest>("application/json")
            .Produces<ThemeDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static RouteHandlerBuilder RequireOwner(this RouteHandlerBuilder builder) =>
        builder.RequireShopOwner("WEBSITE_OWNER_REQUIRED", "Chỉ chủ shop (Owner) mới được xem/sửa website.");

    /// <summary>Đọc body với trần cứng: đặt <c>MaxRequestBodySize</c> của Kestrel (413 sớm theo Content-Length) VÀ tự đếm byte (chunked/
    /// host không áp feature). Vượt → <see cref="BadHttpRequestException"/> 413, được <see cref="BadHttpRequestExceptionHandler"/>
    /// ánh xạ theo <see cref="RequestBodyTooLargeErrorMetadata"/> của route.</summary>
    private static async Task<byte[]> ReadBoundedBodyAsync(HttpRequest request, long limit, CancellationToken ct)
    {
        var feature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
        {
            feature.MaxRequestBodySize = limit;
        }

        if (request.ContentLength > limit)
        {
            throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static JsonDocument ParseObject(byte[] bytes, string errorCode)
    {
        try
        {
            var doc = JsonDocument.Parse(bytes);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                // Khoá trùng ở gốc envelope: JsonDocument chấp nhận (last-wins) nên phải chặn tường minh, kẻo `tree`/`version` mơ hồ.
                // Name ném InvalidOperationException với surrogate lẻ — cũng là body hỏng, không phải 500.
                var names = new HashSet<string>(StringComparer.Ordinal);
                if (doc.RootElement.EnumerateObject().All(p => names.Add(p.Name)))
                {
                    return doc;
                }
            }

            doc.Dispose();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
        }

        throw new UnprocessableException(errorCode, "Body phải là một object JSON hợp lệ (không trùng khoá).");
    }

    private static (string TreeJson, long Version) ParseDraftEnvelope(byte[] bytes)
    {
        using var doc = ParseObject(bytes, "TREE_INVALID");
        if (!doc.RootElement.TryGetProperty("tree", out var tree) || tree.ValueKind != JsonValueKind.Object)
        {
            throw new UnprocessableException("TREE_INVALID", "Thiếu 'tree' (object).");
        }

        if (!doc.RootElement.TryGetProperty("version", out var version) || !version.TryGetInt64(out var versionValue) || versionValue < 0)
        {
            throw new UnprocessableException("VALIDATION_ERROR", "Thiếu hoặc sai 'version' (số nguyên không âm).");
        }

        return (tree.GetRawText(), versionValue);
    }

    private static (string? PresetKey, string TokensJson) ParseThemeEnvelope(byte[] bytes)
    {
        using var doc = ParseObject(bytes, "THEME_INVALID");
        if (!doc.RootElement.TryGetProperty("tokens", out var tokens) || tokens.ValueKind != JsonValueKind.Object)
        {
            throw new UnprocessableException("THEME_INVALID", "Thiếu 'tokens' (object).");
        }

        string? presetKey = null;
        if (doc.RootElement.TryGetProperty("presetKey", out var preset) && preset.ValueKind != JsonValueKind.Null)
        {
            if (preset.ValueKind != JsonValueKind.String)
            {
                throw new UnprocessableException("VALIDATION_ERROR", "'presetKey' phải là chuỗi hoặc null.");
            }

            presetKey = preset.GetString();
        }

        return (presetKey, tokens.GetRawText());
    }
}

/// <summary>Body <c>POST …/website/pages</c>. Không có kind/systemType/shopId (#99, R10).</summary>
public sealed record CreatePageRequest(
    string Slug,
    string Title,
    string? SeoTitle,
    string? SeoDescription,
    Guid? OgImageId,
    bool NoIndex = false,
    bool IsEnabled = true);

/// <summary>Body <c>PUT …/website/pages/{pageId}</c> — thay toàn bộ; riêng <c>Slug = null</c> nghĩa là không đổi slug.</summary>
public sealed record UpdatePageRequest(
    string? Slug,
    string Title,
    string? SeoTitle,
    string? SeoDescription,
    Guid? OgImageId,
    bool NoIndex,
    bool IsEnabled);

/// <summary>Shape OpenAPI của body <c>savePageDraft</c> (<c>Accepts&lt;T&gt;</c>) — KHÔNG dùng để bind thật. <c>Version</c> là token
/// opaque (xmin) client nhận từ <c>getPageDraft</c>/lần lưu trước.</summary>
public sealed class SavePageDraftRequest
{
    public JsonElement Tree { get; set; }
    public long Version { get; set; }
}

/// <summary>Shape OpenAPI của body <c>updateTheme</c> — KHÔNG dùng để bind thật.</summary>
public sealed class UpdateThemeRequest
{
    public string? PresetKey { get; set; }
    public JsonElement Tokens { get; set; }
}
