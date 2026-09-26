using Vsite.Application.Common.Imaging;
using Vsite.Infrastructure.Imaging;

namespace Vsite.IntegrationTests.Imaging;

/// <summary>
/// Test cho <see cref="ImagePresetCatalog"/> và <see cref="DerivativePresetCatalog"/> (T3, MEDIA-001,
/// #78/#86). Đọc file thật ở repo root — không mock, catalog PHẢI khớp đúng file người duyệt đã chốt
/// (<c>config/image-presets.json</c>, <c>packages/builder-components/generated/derivative-presets.json</c>).
/// </summary>
public sealed class ImagePresetCatalogTests
{
    [Fact]
    public void Reads_Real_ImagePresets_File_With_Nine_Presets()
    {
        var catalog = new ImagePresetCatalog(ImagePresetsFilePath());

        Assert.Equal(9, catalog.All.Count);
    }

    [Fact]
    public void TryGet_320x96_inside_Has_Fit_Inside()
    {
        var catalog = new ImagePresetCatalog(ImagePresetsFilePath());

        var found = catalog.TryGet("320x96,inside", out var preset);

        Assert.True(found);
        Assert.Equal(PresetFit.Inside, preset.Fit);
        Assert.Equal(320, preset.Width);
        Assert.Equal(96, preset.Height);
    }

    [Fact]
    public void TryGet_Unknown_Preset_Returns_False()
    {
        var catalog = new ImagePresetCatalog(ImagePresetsFilePath());

        var found = catalog.TryGet("unknown-preset", out _);

        Assert.False(found);
    }

    [Fact]
    public void Missing_ImagePresets_File_Throws_FileNotFoundException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid()}.json");

        Assert.Throws<FileNotFoundException>(() => new ImagePresetCatalog(path));
    }

    [Fact]
    public void Bad_Shape_ImagePresets_File_Throws_InvalidOperationException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bad-image-presets-{Guid.NewGuid()}.json");
        File.WriteAllText(path, "{ \"not_presets\": {} }");

        try
        {
            Assert.Throws<InvalidOperationException>(() => new ImagePresetCatalog(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DerivativePresetCatalog_For_Shop_Matches_Artifact()
    {
        var catalog = new DerivativePresetCatalog(DerivativePresetsFilePath());

        var result = catalog.For("Shop");

        Assert.Equal(new[] { "320x96,inside", "96x96,cover" }, result);
    }

    [Fact]
    public void DerivativePresetCatalog_For_Unknown_Returns_Empty()
    {
        var catalog = new DerivativePresetCatalog(DerivativePresetsFilePath());

        var result = catalog.For("Unknown");

        Assert.Empty(result);
    }

    [Fact]
    public void Missing_DerivativePresets_File_Throws_FileNotFoundException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid()}.json");

        Assert.Throws<FileNotFoundException>(() => new DerivativePresetCatalog(path));
    }

    [Fact]
    public void Bad_Shape_DerivativePresets_File_Throws_InvalidOperationException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bad-derivative-presets-{Guid.NewGuid()}.json");
        File.WriteAllText(path, "[1,2,3]");

        try
        {
            Assert.Throws<InvalidOperationException>(() => new DerivativePresetCatalog(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string ImagePresetsFilePath() => Path.Combine(FindRepoRoot(), "config", "image-presets.json");

    private static string DerivativePresetsFilePath() =>
        Path.Combine(FindRepoRoot(), "packages", "builder-components", "generated", "derivative-presets.json");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "pnpm-workspace.yaml")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Không tìm thấy repo root (pnpm-workspace.yaml).");
    }
}
