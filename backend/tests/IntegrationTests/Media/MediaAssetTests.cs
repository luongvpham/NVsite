using Vsite.Domain.Exceptions;
using Vsite.Domain.Media.Entities;

namespace Vsite.IntegrationTests;

/// <summary>
/// Unit test thuần cho factory của <see cref="MediaAsset"/> — không cần Postgres, chạy được không
/// Docker: <c>dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~MediaAssetTests"</c>.
/// </summary>
public sealed class MediaAssetTests
{
    private static readonly Guid ShopId = Guid.NewGuid();

    [Fact]
    public void NewLibrary_sets_IsInLibrary_true_and_Preset_null()
    {
        var asset = MediaAsset.NewLibrary(
            ShopId, "shops/x/library/a.webp", 1200, 800, 12345, 0.5f, 0.5f, "photo.jpg", "alt");

        Assert.True(asset.IsInLibrary);
        Assert.Null(asset.Preset);
        Assert.Null(asset.SourceAssetId);
        Assert.Equal("image/webp", asset.MimeType);
        Assert.Equal(ShopId, asset.ShopId);
    }

    [Fact]
    public void NewDirect_sets_IsInLibrary_false_and_requires_preset()
    {
        var asset = MediaAsset.NewDirect(
            ShopId, "shops/x/slot/a.webp", 320, 96, 500, "320x96,inside", 0.5f, 0.5f, "photo.jpg", null);

        Assert.False(asset.IsInLibrary);
        Assert.Equal("320x96,inside", asset.Preset);
        Assert.Null(asset.SourceAssetId);
    }

    [Fact]
    public void NewDerived_from_library_source_succeeds()
    {
        var library = MediaAsset.NewLibrary(
            ShopId, "shops/x/library/a.webp", 1200, 800, 12345, 0.5f, 0.5f, null, null);

        var clone = MediaAsset.NewDerived(library, "shops/x/clone/a.webp", 320, 96, 500, "320x96,inside", 0.5f, 0.5f);

        Assert.False(clone.IsInLibrary);
        Assert.Equal("320x96,inside", clone.Preset);
        Assert.Equal(library.Id, clone.SourceAssetId);
        Assert.Equal(ShopId, clone.ShopId);
    }

    [Fact]
    public void NewDerived_from_non_library_source_throws_MEDIA_CLONE_FROM_CLONE()
    {
        var direct = MediaAsset.NewDirect(
            ShopId, "shops/x/slot/a.webp", 320, 96, 500, "320x96,inside", 0.5f, 0.5f, null, null);

        var ex = Assert.Throws<DomainException>(() =>
            MediaAsset.NewDerived(direct, "shops/x/clone/a.webp", 96, 96, 200, "96x96,cover", 0.5f, 0.5f));

        Assert.Equal("MEDIA_CLONE_FROM_CLONE", ex.ErrorCode);
    }

    [Theory]
    [InlineData(-0.1f, 0.5f)]
    [InlineData(1.1f, 0.5f)]
    [InlineData(0.5f, -0.1f)]
    [InlineData(0.5f, 1.1f)]
    public void NewLibrary_rejects_focal_point_outside_0_1(float x, float y)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MediaAsset.NewLibrary(ShopId, "shops/x/library/a.webp", 100, 100, 10, x, y, null, null));
    }
}
