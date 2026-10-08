using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Vsite.Api.Tenancy;
using Vsite.Application.Media.Commands.CloneFromLibrary;
using Vsite.Application.Media.Commands.DeleteFromLibrary;
using Vsite.Application.Media.Commands.UploadShopLogo;
using Vsite.Application.Media.Commands.UploadToLibrary;
using Vsite.Application.Media.Commands.UploadToSlot;
using Vsite.Application.Media.Dtos;
using Vsite.Application.Media.Queries.GetAssetsByIds;
using Vsite.Application.Media.Queries.GetDerivatives;
using Vsite.Application.Media.Queries.GetReferences;
using Vsite.Application.Media.Queries.GetUsage;
using Vsite.Application.Media.Queries.ListLibrary;
using Vsite.Domain.Authorization;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Pagination;

namespace Vsite.Api.Media;

/// <summary>
/// T5, MEDIA-001 (#70) — hai endpoint upload đầu tiên của module Media (bảng endpoint đầy đủ ở
/// `backend/docs/modules/media.md`; T6–T9 thêm phần còn lại lên trên module này). Cả hai endpoint đều
/// multipart, `.DisableAntiforgery()` (API dùng JWT bearer, không cookie) và giới hạn dung lượng
/// request ~11 MB (Kestrel trả 413 nếu vượt).
///
/// T6, MEDIA-001 (#55, #71, #72) thêm: list thư viện (phân trang), clone từ Library, xoá khỏi Library
/// (Owner only — kiểm tra ở handler, cùng khuôn `UpdateShopHandler`, không phải policy riêng ở
/// endpoint), tra tham chiếu, tra theo lô id, và quota đã dùng. Không endpoint nào trong nhóm này
/// phục vụ file ảnh qua HTTP — đó là T9 (`/media/{key}`), một route KHÁC hẳn nhóm `/shops/{shopId}/media`.
///
/// Binding form: đọc <c>HttpRequest.ReadFormAsync</c> THẲNG trong handler thay vì dùng
/// <c>[FromForm]</c> complex-type auto-binding (Giả định tôi đã tự đặt — xem
/// `Docs/tasks/MEDIA-001/changelog.md` mục 8): argument binding của Minimal API chạy TRƯỚC endpoint filter, nên nếu bind
/// qua `[FromForm]` complex type thì `IHttpMaxRequestBodySizeFeature` set trong handler sẽ set QUÁ
/// TRỄ — form đã bị đọc hết trước đó. Đọc form thủ công cho phép set giới hạn NGAY TRƯỚC lần đọc
/// body đầu tiên trong cùng request.
/// </summary>
public static class MediaEndpoints
{
    private const long MaxUploadBytes = 11 * 1024 * 1024;

    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        var media = app.MapGroup("/shops/{shopId:guid}/media").WithTags("Media").WithGroupName("media");

        media.MapPost("/slot-uploads", async (Guid shopId, HttpRequest request, ISender sender, CancellationToken ct) =>
        {
            RequireMultipart(request);
            SetMaxRequestBodySize(request, MaxUploadBytes);
            var form = await request.ReadFormAsync(ct);
            var file = RequireFile(form);

            await using var stream = file.OpenReadStream();
            var command = new UploadToSlotCommand(
                shopId,
                stream,
                file.FileName,
                form["preset"].ToString(),
                ParseFloat(form["focalX"], 0.5f),
                ParseFloat(form["focalY"], 0.5f),
                ParseBool(form["saveToLibrary"]),
                NullIfEmpty(form["altText"].ToString()));

            var result = await sender.Send(command, ct);
            return Results.Ok(result);
        })
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireShopMembership()
            .DisableAntiforgery()
            .Accepts<UploadToSlotForm>("multipart/form-data")
            .Produces<SlotUploadResultDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        media.MapPost("/library", async (Guid shopId, HttpRequest request, ISender sender, CancellationToken ct) =>
        {
            RequireMultipart(request);
            SetMaxRequestBodySize(request, MaxUploadBytes);
            var form = await request.ReadFormAsync(ct);
            var file = RequireFile(form);

            await using var stream = file.OpenReadStream();
            var command = new UploadToLibraryCommand(
                shopId,
                stream,
                file.FileName,
                NullIfEmpty(form["altText"].ToString()),
                NullIfEmpty(form["folder"].ToString()));

            var result = await sender.Send(command, ct);
            return Results.Ok(result);
        })
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireShopMembership()
            .DisableAntiforgery()
            .Accepts<UploadToLibraryForm>("multipart/form-data")
            .Produces<MediaAssetDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        media.MapGet("/library", async (Guid shopId, ISender sender, CancellationToken ct, int page = 1, int pageSize = 24) =>
            Results.Ok(await sender.Send(new ListLibraryQuery(shopId, page, pageSize), ct)))
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireShopMembership()
            .Produces<PagedResult<MediaAssetDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        media.MapPost("/library/{assetId:guid}/clones", async (
            Guid shopId, Guid assetId, CloneRequest body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new CloneFromLibraryCommand(shopId, assetId, body.Preset, body.FocalX, body.FocalY), ct)))
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireShopMembership()
            .Produces<MediaAssetDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        media.MapGet("/library/{assetId:guid}/references", async (Guid shopId, Guid assetId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetReferencesQuery(shopId, assetId), ct)))
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireShopMembership()
            .Produces<MediaReferencesDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // MEDIA-001 D3 (#73) — tra (bản Library gốc, preset) → phái sinh cho Portal (vd. logo shop
        // `320x96,inside` từ Shop.LogoId). `preset` chứa dấu phẩy nên client phải URL-encode; binding
        // string? từ query không tách theo dấu phẩy. Id lạ/của shop khác/không có phái sinh -> 200 [].
        media.MapGet("/library/{assetId:guid}/derivatives", async (
            Guid shopId, Guid assetId, string? preset, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetDerivativesQuery(shopId, assetId, preset), ct)))
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireShopMembership()
            .Produces<IReadOnlyList<MediaAssetDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        // Owner-only — kiểm tra RoleId ở handler (cùng khuôn UpdateShopHandler), không phải policy
        // ASP.NET Core riêng ở tầng endpoint. RequireShopMembership() chỉ xác nhận membership bất kỳ.
        media.MapDelete("/library/{assetId:guid}", async (Guid shopId, Guid assetId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteFromLibraryCommand(shopId, assetId), ct);
            return Results.NoContent();
        })
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireShopMembership()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // ids: query param lặp lại (?ids=a&ids=b) — Minimal API bind thẳng Guid[] theo tên tham số,
        // không cần [FromQuery]; OpenAPI/Orval diễn đạt `ids` như `array` param tự nhiên hơn so với
        // một chuỗi comma-separated phải tự parse.
        media.MapGet("/assets", async (Guid shopId, Guid[]? ids, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetAssetsByIdsQuery(shopId, ids ?? []), ct)))
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireShopMembership()
            .Produces<IReadOnlyList<MediaAssetDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        media.MapGet("/usage", async (Guid shopId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetUsageQuery(shopId), ct)))
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireShopMembership()
            .Produces<MediaUsageDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // T7, MEDIA-001 (#73, #82) — `PUT /shops/{shopId}/logo`, KHÔNG dưới prefix `/media` (route
        // riêng `/shops/{shopId}/logo`, không phải `/shops/{shopId}/media/logo`). Vẫn module Media
        // (viết Shop.LogoId qua Public Contract `IShopLogoWriter` — Shop không reference Media).
        app.MapPut("/shops/{shopId:guid}/logo", async (Guid shopId, HttpRequest request, ISender sender, CancellationToken ct) =>
        {
            RequireMultipart(request);
            SetMaxRequestBodySize(request, MaxUploadBytes);
            var form = await request.ReadFormAsync(ct);
            var file = RequireFile(form);

            await using var stream = file.OpenReadStream();
            var command = new UploadShopLogoCommand(shopId, stream, file.FileName);

            var result = await sender.Send(command, ct);
            return Results.Ok(result);
        })
            .WithTags("Media").WithGroupName("media")
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .RequireShopMembership()
            .DisableAntiforgery()
            .Accepts<UploadShopLogoForm>("multipart/form-data")
            .Produces<ShopLogoDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    /// <summary>Review sau T5 — trước đây thiếu check này: request không phải multipart khiến
    /// <c>ReadFormAsync</c> ném <c>InvalidOperationException</c>, không có handler nào bắt (rơi
    /// xuống mặc định 500, không phải ProblemDetails/error_code — vi phạm #19).</summary>
    private static void RequireMultipart(HttpRequest request)
    {
        if (!request.HasFormContentType)
        {
            throw new UnsupportedMediaTypeException(
                "MEDIA_MULTIPART_REQUIRED", "Endpoint chỉ nhận Content-Type multipart/form-data.");
        }
    }

    private static void SetMaxRequestBodySize(HttpRequest request, long bytes)
    {
        var feature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
        {
            feature.MaxRequestBodySize = bytes;
        }
    }

    private static IFormFile RequireFile(IFormCollection form)
    {
        var file = form.Files["file"];
        if (file is null || file.Length == 0)
        {
            throw new UnprocessableException("MEDIA_FILE_MISSING", "Thiếu file ảnh trong multipart form (field 'file').");
        }

        return file;
    }

    private static float ParseFloat(string? raw, float fallback) =>
        float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private static bool ParseBool(string? raw) =>
        bool.TryParse(raw, out var value) && value;

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>Shape multipart cho OpenAPI (<c>.Accepts&lt;T&gt;</c>) — KHÔNG dùng để bind tham số
/// thật (xem doc trên <see cref="MediaEndpoints"/> vì sao đọc form thủ công).</summary>
public sealed class UploadToSlotForm
{
    public IFormFile File { get; set; } = null!;
    public string Preset { get; set; } = string.Empty;
    public float FocalX { get; set; } = 0.5f;
    public float FocalY { get; set; } = 0.5f;
    public bool SaveToLibrary { get; set; }
    public string? AltText { get; set; }
}

public sealed class UploadToLibraryForm
{
    public IFormFile File { get; set; } = null!;
    public string? AltText { get; set; }
    public string? Folder { get; set; }
}

/// <summary>Shape multipart cho OpenAPI của `PUT /shops/{shopId}/logo` (T7) — chỉ nhận file, không
/// nhận focal/altText (derivative luôn focal Center, xem doc trên <see cref="UploadShopLogoCommand"/>).</summary>
public sealed class UploadShopLogoForm
{
    public IFormFile File { get; set; } = null!;
}

/// <summary>Body JSON của `POST /shops/{shopId}/media/library/{assetId}/clones` (T6). Không multipart
/// — clone render lại từ file Library đã lưu, không nhận file mới.</summary>
public sealed record CloneRequest(string Preset, float? FocalX, float? FocalY);
