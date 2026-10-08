using Vsite.Application.Common.Imaging;

namespace Vsite.IntegrationTests.Imaging;

/// <summary>MEDIA-001 D4 (#88) — nơi DUY NHẤT biết scheme URL ảnh. `Program.cs` mount `/media` bằng đúng hằng
/// <see cref="ImagePaths.MediaPathPrefix"/> (test end-to-end `ShopLogoTests` GET đúng `logoUrl` và nhận 200).</summary>
public sealed class ImagePathsMediaUrlTests
{
    [Fact]
    public void MediaUrl_is_prefix_slash_key_and_prefix_is_slash_media()
    {
        Assert.Equal("/media", ImagePaths.MediaPathPrefix);
        Assert.Equal("/media/shops/abc/x.webp", ImagePaths.MediaUrl("shops/abc/x.webp"));
        Assert.Equal($"{ImagePaths.MediaPathPrefix}/shops/a/b.webp", ImagePaths.MediaUrl("shops/a/b.webp"));
    }

    [Fact]
    public void Program_mounts_media_with_the_same_constant()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "pnpm-workspace.yaml")))
        {
            root = root.Parent;
        }

        var program = File.ReadAllText(Path.Combine(root!.FullName, "backend", "src", "Vsite.Api", "Program.cs"));

        Assert.Contains("app.Map(Vsite.Application.Common.Imaging.ImagePaths.MediaPathPrefix,", program);
        Assert.DoesNotContain("app.Map(\"/media\"", program);
    }
}
