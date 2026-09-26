using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Vsite.Application.Common.Imaging;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media;
using Vsite.Application.Media.Commands.UploadToLibrary;
using Vsite.Application.Media.Commands.UploadToSlot;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Media.Entities;
using Vsite.Infrastructure.Imaging;
using Vsite.Infrastructure.Persistence;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;

namespace Vsite.IntegrationTests.Media;

/// <summary>
/// T5, MEDIA-001 — test handler-level KHÔNG cần Docker: EF Core InMemory (thay Postgres) +
/// <see cref="ImageSharpImageProcessor"/> + <see cref="LocalDiskObjectStorage"/> thật trong thư mục
/// temp. Bù cho <c>UploadEndpointTests</c> (`MediaApiFactory`, Postgres+Redis Testcontainers) không
/// chạy được trên máy không có Docker daemon — xem `Docs/DOCKER-TEST-DEBT.md`.
///
/// Chạy: <c>dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~UploadHandlerTests"</c>.
/// </summary>
public sealed class UploadHandlerTests : IDisposable
{
    private readonly string _storageRoot;
    private readonly IObjectStorage _storage;
    private readonly IImageProcessor _processor;
    private readonly FakePresetCatalog _presetCatalog;
    private readonly FakeTenantContext _tenantContext;

    private static readonly ImagePreset CoverPreset = new("test-800x600,cover", 800, 600, PresetFit.Cover);

    public UploadHandlerTests()
    {
        _storageRoot = Path.Combine(Path.GetTempPath(), "vsite-media-handler-test-" + Guid.NewGuid());
        Directory.CreateDirectory(_storageRoot);

        var storageOptions = Options.Create(new StorageOptions { LocalDiskRoot = _storageRoot });
        _storage = new LocalDiskObjectStorage(storageOptions, new FakeHostEnvironment());
        _processor = new ImageSharpImageProcessor(Options.Create(new ImageUploadOptions()));
        _presetCatalog = new FakePresetCatalog(CoverPreset);
        _tenantContext = new FakeTenantContext();
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    // ---- Test bắt buộc 3: không tick -> đúng 1 record, Preset = preset slot, SourceAssetId null,
    // IsInLibrary=false, file tồn tại đúng kích thước preset ----

    [Fact]
    public async Task UploadToSlot_without_SaveToLibrary_creates_exactly_one_Direct_record()
    {
        await using var db = CreateDbContext();
        var writer = CreateWriter(db);
        var handler = new UploadToSlotHandler(_processor, _presetCatalog, writer);
        var shopId = Guid.NewGuid();
        _tenantContext.ShopId = shopId;

        using var sourceStream = new MemoryStream(EncodeJpeg(1600, 1200));
        var command = new UploadToSlotCommand(
            shopId, sourceStream, "photo.jpg", CoverPreset.Name, 0.5f, 0.5f, SaveToLibrary: false, AltText: "alt");

        var result = await handler.Handle(command, CancellationToken.None);

        var assets = await db.MediaAssets.ToListAsync();
        var asset = Assert.Single(assets);

        Assert.Equal(CoverPreset.Name, asset.Preset);
        Assert.Null(asset.SourceAssetId);
        Assert.False(asset.IsInLibrary);
        Assert.Equal(asset.Id, result.Asset.Id);
        Assert.Null(result.LibraryAsset);

        var stored = await _storage.OpenReadAsync(asset.StorageKey, CancellationToken.None);
        Assert.NotNull(stored);
        await using var storedContent = stored!.Content;
        using var image = await Image.LoadAsync(storedContent);
        Assert.Equal(CoverPreset.Width, image.Width);
        Assert.Equal(CoverPreset.Height, image.Height);
        Assert.Equal(CoverPreset.Width, asset.Width);
        Assert.Equal(CoverPreset.Height, asset.Height);
    }

    // ---- Test bắt buộc 4: có tick -> 2 record; clone có SourceAssetId = id bản Library; response
    // trả id CLONE làm asset đặt vào tree ----

    [Fact]
    public async Task UploadToSlot_with_SaveToLibrary_creates_library_plus_derived_clone()
    {
        await using var db = CreateDbContext();
        var writer = CreateWriter(db);
        var handler = new UploadToSlotHandler(_processor, _presetCatalog, writer);
        var shopId = Guid.NewGuid();
        _tenantContext.ShopId = shopId;

        using var sourceStream = new MemoryStream(EncodeJpeg(1600, 1200));
        var command = new UploadToSlotCommand(
            shopId, sourceStream, "photo.jpg", CoverPreset.Name, 0.5f, 0.5f, SaveToLibrary: true, AltText: null);

        var result = await handler.Handle(command, CancellationToken.None);

        var assets = await db.MediaAssets.OrderBy(a => a.IsInLibrary).ToListAsync();
        Assert.Equal(2, assets.Count);

        var library = Assert.Single(assets, a => a.IsInLibrary);
        var clone = Assert.Single(assets, a => !a.IsInLibrary);

        Assert.Null(library.Preset);
        Assert.Null(library.SourceAssetId);

        Assert.Equal(CoverPreset.Name, clone.Preset);
        Assert.Equal(library.Id, clone.SourceAssetId);

        // Response.Asset phải là CLONE (id đặt vào tree), LibraryAsset đi kèm là bản Library.
        Assert.Equal(clone.Id, result.Asset.Id);
        Assert.NotNull(result.LibraryAsset);
        Assert.Equal(library.Id, result.LibraryAsset!.Id);
    }

    // ---- Preset lạ -> validator FAIL ----

    [Fact]
    public async Task UploadToSlotValidator_rejects_unknown_preset()
    {
        var validator = new UploadToSlotValidator(_presetCatalog);
        var command = new UploadToSlotCommand(
            Guid.NewGuid(), new MemoryStream(), "photo.jpg", "does-not-exist", 0.5f, 0.5f, false, null);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UploadToSlotCommand.Preset));
    }

    // ---- Focal ngoài [0,1] -> validator FAIL ----

    [Theory]
    [InlineData(1.5f, 0.5f)]
    [InlineData(0.5f, -0.1f)]
    public async Task UploadToSlotValidator_rejects_focal_point_outside_0_1(float focalX, float focalY)
    {
        var validator = new UploadToSlotValidator(_presetCatalog);
        var command = new UploadToSlotCommand(
            Guid.NewGuid(), new MemoryStream(), "photo.jpg", CoverPreset.Name, focalX, focalY, false, null);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    // ---- R4: SaveChangesAsync ném lỗi -> storage rỗng sau request (best-effort rollback) ----

    [Fact]
    public async Task UploadToSlot_when_SaveChanges_throws_deletes_written_keys()
    {
        await using var db = CreateDbContext();
        var throwingDb = new ThrowingDbContext(db);
        var writer = CreateWriter(throwingDb);
        var handler = new UploadToSlotHandler(_processor, _presetCatalog, writer);
        var shopId = Guid.NewGuid();
        _tenantContext.ShopId = shopId;

        using var sourceStream = new MemoryStream(EncodeJpeg(1600, 1200));
        var command = new UploadToSlotCommand(
            shopId, sourceStream, "photo.jpg", CoverPreset.Name, 0.5f, 0.5f, SaveToLibrary: true, AltText: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));

        Assert.Empty(Directory.GetFiles(_storageRoot, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task UploadToLibrary_when_SaveChanges_throws_deletes_written_key()
    {
        await using var db = CreateDbContext();
        var throwingDb = new ThrowingDbContext(db);
        var writer = CreateWriter(throwingDb);
        var handler = new UploadToLibraryHandler(_processor, writer);
        var shopId = Guid.NewGuid();
        _tenantContext.ShopId = shopId;

        using var sourceStream = new MemoryStream(EncodeJpeg(1600, 1200));
        var command = new UploadToLibraryCommand(shopId, sourceStream, "photo.jpg", null, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));

        Assert.Empty(Directory.GetFiles(_storageRoot, "*", SearchOption.AllDirectories));
    }

    // ---- helpers ----

    private MediaAssetWriter CreateWriter(IAppDbContext db) =>
        new(db, _storage, TimeProvider.System, Options.Create(new ImageUploadOptions()));

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options, _tenantContext);
    }

    private static byte[] EncodeJpeg(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(x => x.Fill(SixLabors.ImageSharp.Color.SeaGreen));
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Vsite.IntegrationTests";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; } = "Test";
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        public TenantAudienceKind AudienceKind => TenantAudienceKind.Portal;
        public Guid? ShopId { get; set; }
    }

    private sealed class FakePresetCatalog(params ImagePreset[] presets) : IImagePresetCatalog
    {
        private readonly Dictionary<string, ImagePreset> _presets = presets.ToDictionary(p => p.Name);

        public bool TryGet(string name, out ImagePreset preset) => _presets.TryGetValue(name, out preset!);

        public IReadOnlyCollection<ImagePreset> All => _presets.Values;
    }

    /// <summary>Decorator R4: forward mọi DbSet cho instance InMemory thật, nhưng
    /// <see cref="SaveChangesAsync"/> luôn ném lỗi TRƯỚC khi chạm DB thật — mô phỏng "ghi file
    /// thành công, DB fail" (CHECK violation / mất kết nối).</summary>
    private sealed class ThrowingDbContext(IAppDbContext inner) : IAppDbContext
    {
        public DbSet<User> Users => inner.Users;
        public DbSet<ExternalLogin> ExternalLogins => inner.ExternalLogins;
        public DbSet<Role> Roles => inner.Roles;
        public DbSet<UserShop> UserShops => inner.UserShops;
        public DbSet<PendingRegistration> PendingRegistrations => inner.PendingRegistrations;
        public DbSet<RefreshToken> RefreshTokens => inner.RefreshTokens;
        public DbSet<PasswordResetToken> PasswordResetTokens => inner.PasswordResetTokens;
        public DbSet<ShopEntity> Shops => inner.Shops;
        public DbSet<MediaAsset> MediaAssets => inner.MediaAssets;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated DB failure (R4 test).");
    }
}
