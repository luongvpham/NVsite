using Microsoft.Extensions.Options;
using Vsite.Application.Common.Imaging;
using Vsite.Application.Common.Interfaces;
using Vsite.Domain.Media.Entities;
using Vsite.Domain.Media.Enums;

namespace Vsite.Application.Media;

/// <summary>
/// T5, MEDIA-001 — helper dùng chung cho mọi command ghi <see cref="MediaAsset"/> (upload slot,
/// upload library, sau này clone T7). Scoped: một instance theo dõi TOÀN BỘ storage key đã
/// <see cref="IObjectStorage.PutAsync"/> trong request hiện tại, để rollback best-effort khi thất
/// bại (R4) — hoặc do <c>SaveChangesAsync</c> ném lỗi, hoặc do một render/put SAU đó trong CÙNG
/// request thất bại (vd. mode "có tick": ghi xong bản Library rồi mới render clone, clone lỗi thì
/// bản Library vừa ghi cũng phải bị xoá).
///
/// KHÔNG tự gọi <c>db.SaveChangesAsync</c> trực tiếp — handler phải gọi
/// <see cref="SaveChangesAsync"/> của lớp này để rollback hoạt động.
/// </summary>
public sealed class MediaAssetWriter(
    IAppDbContext db,
    IObjectStorage storage,
    TimeProvider timeProvider,
    IOptions<ImageUploadOptions> uploadOptions)
{
    private readonly List<string> _writtenKeys = [];

    /// <summary>Bản Library — chưa crop, LongEdge 1600, giữ tỉ lệ gốc (R3: không upscale).</summary>
    public async Task<MediaAsset> WriteLibraryAsync(
        Guid shopId,
        ISourceImage source,
        float focalX,
        float focalY,
        string? originalFileName,
        string? altText,
        string? folder,
        CancellationToken ct)
    {
        var rendered = await RenderAndPutAsync(shopId, source, new ImageTransform.LongEdge(uploadOptions.Value.MaxLongEdge), ct);

        var asset = MediaAsset.NewLibrary(
            shopId, rendered.Key, rendered.Width, rendered.Height, rendered.SizeBytes,
            focalX, focalY, TruncateFileName(originalFileName), altText);
        asset.Folder = folder;

        db.MediaAssets.Add(asset);
        return asset;
    }

    /// <summary>Ảnh upload thẳng vào slot (không tick "Lưu vào thư viện") — đã crop theo
    /// <paramref name="preset"/>.</summary>
    public async Task<MediaAsset> WriteDirectAsync(
        Guid shopId,
        ISourceImage source,
        ImagePreset preset,
        float focalX,
        float focalY,
        string? originalFileName,
        string? altText,
        CancellationToken ct)
    {
        var transform = preset.ToTransform(new FocalPoint(focalX, focalY));
        var rendered = await RenderAndPutAsync(shopId, source, transform, ct);

        var asset = MediaAsset.NewDirect(
            shopId, rendered.Key, rendered.Width, rendered.Height, rendered.SizeBytes,
            preset.Name, focalX, focalY, TruncateFileName(originalFileName), altText);

        db.MediaAssets.Add(asset);
        return asset;
    }

    /// <summary>Clone cho một slot của Component Tree (<see cref="MediaAssetKind.Clone"/>) từ một bản
    /// Library đã tồn tại trong CÙNG request (chưa cần SaveChanges trước — <c>source.Id</c> đã sinh
    /// sẵn lúc construct, xem <c>BaseEntity</c>).</summary>
    public Task<MediaAsset> WriteCloneAsync(
        MediaAsset source,
        ISourceImage sourceImage,
        ImagePreset preset,
        float focalX,
        float focalY,
        CancellationToken ct) =>
        WriteFromLibraryAsync(MediaAsset.NewClone, source, sourceImage, preset, focalX, focalY, ct);

    /// <summary>Phái sinh của ảnh nghiệp vụ (<see cref="MediaAssetKind.Derivative"/>, vd. logo) — xem
    /// <see cref="WriteCloneAsync"/>.</summary>
    public Task<MediaAsset> WriteDerivativeAsync(
        MediaAsset source,
        ISourceImage sourceImage,
        ImagePreset preset,
        float focalX,
        float focalY,
        CancellationToken ct) =>
        WriteFromLibraryAsync(MediaAsset.NewDerivative, source, sourceImage, preset, focalX, focalY, ct);

    private async Task<MediaAsset> WriteFromLibraryAsync(
        Func<MediaAsset, string, int, int, long, string, float, float, MediaAsset> factory,
        MediaAsset source,
        ISourceImage sourceImage,
        ImagePreset preset,
        float focalX,
        float focalY,
        CancellationToken ct)
    {
        var transform = preset.ToTransform(new FocalPoint(focalX, focalY));
        var rendered = await RenderAndPutAsync(source.ShopId, sourceImage, transform, ct);

        var asset = factory(
            source, rendered.Key, rendered.Width, rendered.Height, rendered.SizeBytes,
            preset.Name, focalX, focalY);

        db.MediaAssets.Add(asset);
        return asset;
    }

    /// <summary>R4 — nếu <c>SaveChangesAsync</c> ném lỗi, xoá best-effort mọi key đã ghi trong
    /// request này rồi NÉM LẠI exception gốc (không nuốt, không bọc lại). Thành công thì XOÁ danh
    /// sách key đã theo dõi — writer là scoped, một scope có thể gọi nhiều lần (vd. nhiều thao tác
    /// tuần tự trong cùng request/test); không clear thì một lần gọi SAU thất bại sẽ xoá NHẦM file
    /// của thao tác TRƯỚC đã commit thành công (review sau T5).</summary>
    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            var result = await db.SaveChangesAsync(ct);
            _writtenKeys.Clear();
            return result;
        }
        catch
        {
            await CleanupWrittenKeysAsync();
            throw;
        }
    }

    private async Task<RenderedFile> RenderAndPutAsync(Guid shopId, ISourceImage source, ImageTransform transform, CancellationToken ct)
    {
        try
        {
            var encoded = await source.RenderAsync(transform, ct);
            var key = ImagePaths.NewWebsiteKey(shopId, timeProvider.GetUtcNow());
            await storage.PutAsync(key, encoded.Bytes, encoded.MimeType, ct);
            _writtenKeys.Add(key);
            return new RenderedFile(key, encoded.Width, encoded.Height, encoded.Bytes.LongLength);
        }
        catch
        {
            // R4 mở rộng: một render/put SAU trong CÙNG request lỗi thì các key đã ghi TRƯỚC đó
            // (vd. bản Library ở mode "có tick") cũng phải bị dọn — không đợi tới SaveChangesAsync.
            await CleanupWrittenKeysAsync();
            throw;
        }
    }

    private async Task CleanupWrittenKeysAsync()
    {
        foreach (var key in _writtenKeys)
        {
            try
            {
                // Idempotent theo thiết kế IObjectStorage — an toàn gọi lại dù caller khác đã xoá.
                await storage.DeleteAsync(key, CancellationToken.None);
            }
            catch
            {
                // Best-effort (R4): không để lỗi dọn dẹp che mất exception gốc đang được rethrow.
            }
        }

        // Xoá khỏi danh sách theo dõi SAU khi đã dọn — cùng lý do clear ở nhánh thành công của
        // SaveChangesAsync: writer scoped có thể còn được dùng tiếp trong cùng scope.
        _writtenKeys.Clear();
    }

    private static string? TruncateFileName(string? fileName) =>
        fileName is { Length: > 200 } ? fileName[..200] : fileName;

    private sealed record RenderedFile(string Key, int Width, int Height, long SizeBytes);
}
