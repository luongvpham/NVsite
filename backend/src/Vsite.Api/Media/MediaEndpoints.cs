using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Vsite.Api.Tenancy;
using Vsite.Application.Media.Commands.UploadToLibrary;
using Vsite.Application.Media.Commands.UploadToSlot;
using Vsite.Application.Media.Dtos;
using Vsite.Domain.Authorization;
using Vsite.Domain.Exceptions;

namespace Vsite.Api.Media;

/// <summary>
/// T5, MEDIA-001 (#70) — hai endpoint upload đầu tiên của module Media (`task-T5-brief.md` cuối
/// file có bảng endpoint đầy đủ; T6–T9 thêm phần còn lại lên trên module này). Cả hai endpoint đều
/// multipart, `.DisableAntiforgery()` (API dùng JWT bearer, không cookie) và giới hạn dung lượng
/// request ~11 MB (Kestrel trả 413 nếu vượt).
///
/// Binding form: đọc <c>HttpRequest.ReadFormAsync</c> THẲNG trong handler thay vì dùng
/// <c>[FromForm]</c> complex-type auto-binding (Giả định tôi đã tự đặt — xem
/// `task-T5-report.md`): argument binding của Minimal API chạy TRƯỚC endpoint filter, nên nếu bind
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
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        media.MapPost("/library", async (Guid shopId, HttpRequest request, ISender sender, CancellationToken ct) =>
        {
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
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
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
