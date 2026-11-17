using System.Text;
using System.Text.Json.Nodes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Common.Persistence;
using Vsite.Application.Media.Interfaces;
using Vsite.Application.Website.Dtos;
using Vsite.Application.Website.Interfaces;
using Vsite.Application.Website.Publishing;
using Vsite.Application.Website.Validation;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Website.Entities;

namespace Vsite.Application.Website.Commands.PublishWebsite;

/// <summary>
/// Flow 1 (WEBSITE-004 T4). Nạp website + theme + mọi trang ĐANG BẬT kèm draft (mọi predicate có <c>ShopId</c> tường minh, #21)
/// → mỗi trang: <see cref="PageTreeValidator"/> → <see cref="RequiredPropsChecker"/> → <see cref="IMediaReferenceValidator"/> (ảnh
/// trong tree + ảnh OG của trang); ảnh OG mặc định + favicon của website kiểm một lần → có lỗi thì 422 <c>PUBLISH_BLOCKED</c>, không
/// ghi gì → chuẩn hoá <c>imageId</c> về GUID chữ thường (R14) → <see cref="SnapshotBuilder"/> → <c>SitePublication</c> (Version = max + 1)
/// → <c>Website.Publish</c> → hạ <c>IsDirty</c> CHỈ cho draft đang bẩn (draft sạch không bị ghi nên <c>xmin</c> giữ nguyên, R9)
/// → MỘT <c>SaveChanges</c>. Race: trùng <c>(website_id, version)</c> (23505) hoặc <c>xmin</c> của draft lệch → 409
/// <c>PUBLISH_CONFLICT</c> (R10). Sau khi lưu: xoá con trỏ cache (#109).
/// </summary>
public sealed class PublishWebsiteHandler(
    IAppDbContext db,
    ITenantContext tenant,
    PageTreeValidator validator,
    RequiredPropsChecker requiredProps,
    IMediaReferenceValidator media,
    IAuditActor actor,
    ISitePointerCache pointerCache,
    ILogger<PublishWebsiteHandler> logger)
    : IRequestHandler<PublishWebsiteCommand, PublicationDto>
{
    private const string MediaInvalidCode = "MEDIA_INVALID_IMAGE_REFERENCE";

    public async Task<PublicationDto> Handle(PublishWebsiteCommand request, CancellationToken cancellationToken)
    {
        var shopId = tenant.RequireShopId();

        var website = await db.Websites.FirstOrDefaultAsync(w => w.ShopId == shopId, cancellationToken)
            ?? throw new NotFoundException("Website", shopId);

        var theme = await db.Themes.AsNoTracking()
            .FirstAsync(t => t.ShopId == shopId && t.Id == website.Id, cancellationToken);

        var pages = await db.Pages.AsNoTracking()
            .Where(p => p.ShopId == shopId && p.WebsiteId == website.Id && p.IsEnabled)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Title)
            .ToListAsync(cancellationToken);

        var pageIds = pages.Select(p => p.Id).ToList();
        var drafts = await db.PageDrafts // tracking: hạ cờ IsDirty, xmin là concurrency token
            .Where(d => d.ShopId == shopId && pageIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        var errors = new List<PublishPageError>();
        var truncated = false;

        // Lỗi cấp website (OG mặc định / favicon) đứng ĐẦU danh sách để trần MaxErrors không cắt mất nó.
        Guid[] websiteImages = [.. new[] { website.DefaultOgImageId, website.FaviconAssetId }.OfType<Guid>()];
        var websiteMediaError = await CheckMediaAsync([], websiteImages, cancellationToken);
        if (websiteMediaError is not null)
        {
            errors.Add(new PublishPageError(null, null, "", MediaInvalidCode, websiteMediaError));
        }

        var snapshotPages = new List<SnapshotPage>(pages.Count);
        var imageIds = new HashSet<Guid>();

        foreach (var page in pages)
        {
            var draft = drafts[page.Id];
            var pageErrors = new List<PublishPageError>();
            void Add(string path, string code, string message) => pageErrors.Add(new(page.Id, page.Title, path, code, message));

            var result = validator.Validate(draft.Tree, page.Kind);
            if (!result.IsValid)
            {
                truncated |= result.ErrorsTruncated; // validator tự dừng ở PageTreeLimits.MaxErrors mỗi trang
                foreach (var e in result.Errors)
                {
                    Add(e.Path, e.Code, e.Message);
                }

                errors.AddRange(pageErrors);
                continue;
            }

            foreach (var e in requiredProps.Check(result.CleanTree!))
            {
                Add(e.Path, e.Code, e.Message);
            }

            var treeIds = ParseImageIds(result.ImageIds);
            if (treeIds is null)
            {
                Add("", MediaInvalidCode, "Một hoặc nhiều ảnh tham chiếu không hợp lệ (id không đúng định dạng).");
            }
            else
            {
                var message = await CheckMediaAsync(treeIds, page.OgImageId is { } og ? [og] : [], cancellationToken);
                if (message is not null)
                {
                    Add("", MediaInvalidCode, message);
                }
            }

            if (pageErrors.Count > 0)
            {
                errors.AddRange(pageErrors);
                continue;
            }

            imageIds.UnionWith(treeIds!);
            if (page.OgImageId is { } ogId)
            {
                imageIds.Add(ogId);
            }

            snapshotPages.Add(new SnapshotPage(page, NormalizeImageIds(result.CleanTree!, result.ImageIds)));
        }

        if (errors.Count > 0)
        {
            throw new PublishBlockedException(
                errors.Take(PublishBlockedException.MaxErrors).ToList(),
                truncated || errors.Count > PublishBlockedException.MaxErrors);
        }

        imageIds.UnionWith(websiteImages);
        var allImageIds = imageIds.ToArray();
        var mediaRows = await db.MediaAssets.AsNoTracking()
            .Where(a => a.ShopId == shopId && allImageIds.Contains(a.Id))
            .Select(a => new SnapshotMedia(a.Id, a.StorageKey, a.Preset))
            .ToListAsync(cancellationToken);
        if (mediaRows.Count != allImageIds.Length)
        {
            // Ảnh bị xoá mềm giữa lúc kiểm và lúc nạp: snapshot bất biến sẽ có imageId không có khoá trong media (R14) → không ghi.
            throw PublishConflict();
        }

        var version = (await db.SitePublications
            .Where(p => p.ShopId == shopId && p.WebsiteId == website.Id)
            .MaxAsync(p => (int?)p.Version, cancellationToken) ?? 0) + 1;

        var snapshot = SnapshotBuilder.Build(website, theme, version, snapshotPages, mediaRows);
        var publication = SitePublication.Create(
            shopId, website.Id, version, snapshot, NormalizeNote(request.Note), actor.UserId, DateTimeOffset.UtcNow);

        db.SitePublications.Add(publication);
        website.Publish(publication);
        foreach (var page in snapshotPages)
        {
            var draft = drafts[page.Page.Id];
            if (draft.IsDirty)
            {
                draft.MarkPublished();
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (UniqueViolation.IsUniqueViolation(ex) || UniqueViolation.IsConcurrencyConflict(ex))
        {
            throw PublishConflict();
        }

        logger.LogInformation(
            "Published website {WebsiteId} of shop {ShopId} as version {Version}: {PageCount} pages, snapshotBytes={SnapshotBytes}",
            website.Id, shopId, version, snapshotPages.Count, Encoding.UTF8.GetByteCount(snapshot));

        try
        {
            await pointerCache.InvalidateAsync(shopId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Bản publish đã commit — không biến nó thành lỗi cho người dùng; con trỏ cũ tự hết hạn theo TTL.
            logger.LogWarning(ex, "Could not invalidate site pointer cache for shop {ShopId}", shopId);
        }

        return Mappings.ToDto(publication, website.CurrentPublicationId);
    }

    private static ConflictException PublishConflict() =>
        new("PUBLISH_CONFLICT", "Website vừa được xuất bản hoặc chỉnh sửa ở nơi khác. Hãy tải lại rồi thử lại.");

    /// <summary>Trả thông điệp lỗi Media (không ném) để gom theo trang; <c>null</c> = hợp lệ.</summary>
    private async Task<string?> CheckMediaAsync(IReadOnlyCollection<Guid> tree, IReadOnlyCollection<Guid> business, CancellationToken ct)
    {
        try
        {
            await media.EnsureValidAsync(tree, business, ct);
            return null;
        }
        catch (UnprocessableException ex) when (ex.ErrorCode == MediaInvalidCode)
        {
            return ex.Message;
        }
    }

    /// <summary>Cùng quy tắc <c>PUT draft</c>: định dạng "D" chặt (không phân biệt hoa thường); hỏng một cái → <c>null</c>.</summary>
    private static List<Guid>? ParseImageIds(IReadOnlyList<string> ids)
    {
        var guids = new List<Guid>(ids.Count);
        foreach (var id in ids)
        {
            if (!Guid.TryParseExact(id, "D", out var guid))
            {
                return null;
            }

            guids.Add(guid);
        }

        return guids;
    }

    /// <summary>
    /// R14: khoá <c>snapshot.media</c> là <c>Guid.ToString()</c> (chữ thường) nên mọi <c>imageId</c> trong tree phải cùng dạng, kẻo
    /// renderer tra trượt. Chỉ thay giá trị nằm trong <paramref name="imageIds"/> (validator đã nhận diện là id ảnh). Walker lặp.
    /// </summary>
    private static string NormalizeImageIds(string cleanTree, IReadOnlyList<string> imageIds)
    {
        if (!imageIds.Any(id => !string.Equals(id, Guid.ParseExact(id, "D").ToString(), StringComparison.Ordinal)))
        {
            return cleanTree; // đã chuẩn — giữ nguyên văn
        }

        var targets = new HashSet<string>(imageIds, StringComparer.Ordinal);
        var root = JsonNode.Parse(cleanTree)!;
        var stack = new Stack<JsonNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            switch (stack.Pop())
            {
                case JsonObject obj:
                    foreach (var (key, value) in obj.ToList())
                    {
                        if (key == "imageId" && value is JsonValue v && v.TryGetValue<string>(out var s) && targets.Contains(s))
                        {
                            obj[key] = Guid.ParseExact(s, "D").ToString();
                        }
                        else if (value is not null)
                        {
                            stack.Push(value);
                        }
                    }

                    break;
                case JsonArray arr:
                    foreach (var item in arr)
                    {
                        if (item is not null)
                        {
                            stack.Push(item);
                        }
                    }

                    break;
            }
        }

        return root.ToJsonString();
    }

    private static string? NormalizeNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}
