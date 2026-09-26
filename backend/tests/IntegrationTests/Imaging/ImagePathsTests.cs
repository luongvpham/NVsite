using Vsite.Application.Common.Imaging;

namespace Vsite.IntegrationTests.Imaging;

/// <summary>
/// Test cho <see cref="ImagePaths.ValidateKey"/> (T2, MEDIA-001). Chỉ kiểm phần validate chung — các
/// member khác của <c>ImagePaths</c> (chuẩn hoá key theo entity, v.v.) do T3 thêm sau.
/// </summary>
public sealed class ImagePathsTests
{
    [Theory]
    [InlineData("shops/abc/def.webp")]
    [InlineData("shops/a/b/c/d.webp")]
    [InlineData("shops/a")]
    public void Valid_key_does_not_throw(string key)
    {
        var exception = Record.Exception(() => ImagePaths.ValidateKey(key));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("shops/../x")]
    [InlineData("shops/a/../../etc/passwd")]
    [InlineData("/etc/passwd")]
    [InlineData("shops\\a")]
    [InlineData("other/a")]
    [InlineData("shops//a")]
    [InlineData("")]
    [InlineData("shops/a\u0000b")]
    public void Invalid_key_throws_ArgumentException(string key)
    {
        Assert.Throws<ArgumentException>(() => ImagePaths.ValidateKey(key));
    }

    [Fact]
    public void Null_key_throws_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ImagePaths.ValidateKey(null!));
    }

    [Fact]
    public void NewWebsiteKey_Has_Expected_Format()
    {
        var shopId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var now = new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

        var key = ImagePaths.NewWebsiteKey(shopId, now);

        Assert.Matches(
            @"^shops/11111111-1111-1111-1111-111111111111/website/2026/09/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\.webp$",
            key);
    }

    [Fact]
    public void ListingFolder_Has_Expected_Format()
    {
        var shopId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var listingId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        Assert.Equal(
            "shops/11111111-1111-1111-1111-111111111111/listings/22222222-2222-2222-2222-222222222222/",
            ImagePaths.ListingFolder(shopId, listingId));
    }

    [Fact]
    public void ProductFolder_Has_Expected_Format()
    {
        var shopId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var productId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        Assert.Equal(
            "shops/11111111-1111-1111-1111-111111111111/products/22222222-2222-2222-2222-222222222222/",
            ImagePaths.ProductFolder(shopId, productId));
    }

    [Fact]
    public void AttributeFolder_Has_Expected_Format()
    {
        var shopId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var attributeId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        Assert.Equal(
            "shops/11111111-1111-1111-1111-111111111111/attributes/22222222-2222-2222-2222-222222222222/",
            ImagePaths.AttributeFolder(shopId, attributeId));
    }

    [Fact]
    public void NewFullKey_Appends_Uuid_Webp_To_Folder()
    {
        var key = ImagePaths.NewFullKey("shops/a/products/b/");

        Assert.Matches(
            @"^shops/a/products/b/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\.webp$",
            key);
    }

    [Fact]
    public void Thumb_Inserts_Prefix_Before_FileName()
    {
        Assert.Equal("shops/a/products/b/thumb_c.webp", ImagePaths.Thumb("shops/a/products/b/c.webp"));
    }

    [Fact]
    public void FeaturedThumb_Inserts_Prefix_Before_FileName()
    {
        Assert.Equal("shops/a/products/b/fthumb_c.webp", ImagePaths.FeaturedThumb("shops/a/products/b/c.webp"));
    }

    [Fact]
    public void IsUnder_Accepts_Real_Subfolder()
    {
        const string folder = "shops/a/products/b/";

        Assert.True(ImagePaths.IsUnder("shops/a/products/b/x.webp", folder));
        Assert.True(ImagePaths.IsUnder("shops/a/products/b/sub/x.webp", folder));
    }

    [Fact]
    public void IsUnder_Rejects_Prefix_That_Is_Not_A_Real_Subfolder()
    {
        const string folder = "shops/a/products/b/";

        Assert.False(ImagePaths.IsUnder("shops/a/products/b-evil/x.webp", folder));
    }
}
