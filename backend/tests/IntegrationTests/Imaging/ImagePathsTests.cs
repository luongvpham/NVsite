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
}
