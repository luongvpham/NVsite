using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Vsite.Application.Common.Imaging;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Media;
using Vsite.Application.Media.Commands.UploadShopLogo;
using Vsite.Application.Media.Queries.GetUsage;
using Vsite.Application.Shop.Interfaces;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Media.Entities;
using Vsite.Infrastructure.Imaging;
using Vsite.Infrastructure.Persistence;
using Vsite.Infrastructure.Shop;
using ShopEntity = Vsite.Domain.Shop.Entities.Shop;

namespace Vsite.IntegrationTests.Media;

/// <summary>
/// T7, MEDIA-001 (#73, #82) — test handler-level KHÔNG cần Docker, cùng khuôn
/// `UploadHandlerTests`/`LibraryHandlerTests` (EF Core InMemory + <see cref="ImageSharpImageProcessor"/>
/// + <see cref="LocalDiskObjectStorage"/> thật trong thư mục temp). Bù cho `ShopLogoTests` (Docker,
/// Postgres+Redis Testcontainers) không chạy được trên máy không có Docker daemon — xem
/// `Docs/DOCKER-TEST-DEBT.md`.
///
/// Ảnh test `logo-alpha-1000x200.png` (1000×200, trong suốt nửa phải) — committed binary mới, KHÔNG
/// tái dùng `alpha.png` (200×200, không khớp assertion "1000×200 → derivative 320×96,inside = 320×64").
///
/// Chạy: <c>dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~ShopLogoHandlerTests"</c>.
/// </summary>
public sealed class ShopLogoHandlerTests : IDisposable
{
    private readonly string _storageRoot;
    private readonly IObjectStorage _storage;
    private readonly IImageProcessor _processor;
    private readonly FakePresetCatalog _presetCatalog;
    private readonly FakeTenantContext _tenantContext;

    private static readonly ImagePreset InsidePreset = new("320x96,inside", 320, 96, PresetFit.Inside);
    private static readonly ImagePreset CoverPreset = new("96x96,cover", 96, 96, PresetFit.Cover);

    public ShopLogoHandlerTests()
    {
        _storageRoot = Path.Combine(Path.GetTempPath(), "vsite-shoplogo-handler-test-" + Guid.NewGuid());
        Directory.CreateDirectory(_storageRoot);

        var storageOptions = Options.Create(new StorageOptions { LocalDiskRoot = _storageRoot });
        _storage = new LocalDiskObjectStorage(storageOptions, new FakeHostEnvironment());
        _processor = new ImageSharpImageProcessor(Options.Create(new ImageUploadOptions()));
        _presetCatalog = new FakePresetCatalog(InsidePreset, CoverPreset);
        _tenantContext = new FakeTenantContext();
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    // ---- upload -> 1 bản Library + đúng N phái sinh (N = For("Shop").Count), mỗi Preset một lần,
    // cùng SourceAssetId; Shop.LogoId = id bản Library, MỘT SaveChangesAsync duy nhất ----

    [Fact]
    public async Task Upload_creates_library_plus_one_derivative_per_preset_and_sets_LogoId_in_one_SaveChanges()
    {
        var dbName = Guid.NewGuid().ToString("N");
        await using var db = CreateDbContext(dbName);
        var shopId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _tenantContext.ShopId = shopId;
        await SeedShopAsync(db, shopId);

        // Decorator đếm số lần SaveChangesAsync được gọi qua đúng MỘT instance IAppDbContext này —
        // chứng minh THẬT SỰ đúng MỘT SaveChanges cho cả insert MediaAsset lẫn update Shop.LogoId,
        // không chỉ suy luận từ số record cuối cùng (review sau T7: test cũ không đếm call).
        var countingDb = new CountingSaveChangesDbContext(db);
        var handler = CreateHandler(countingDb);

        using var sourceStream = new MemoryStream(ReadTestAsset("logo-alpha-1000x200.png"));
        var command = new UploadShopLogoCommand(shopId, sourceStream, "logo.png");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, countingDb.SaveChangesCallCount);

        Assert.Equal(2, result.Derivatives.Count);
        Assert.Equal(new[] { InsidePreset.Name, CoverPreset.Name }, result.Derivatives.Select(d => d.Preset).OrderBy(p => p));
        Assert.All(result.Derivatives, d => Assert.Equal(result.LibraryAsset.Id, d.SourceAssetId));

        // Đọc lại từ một AppDbContext MỚI (ChangeTracker rỗng, cùng InMemory database name) — chứng
        // minh dữ liệu đã PERSIST thật qua đúng MỘT SaveChanges, không phải chỉ còn trong bộ nhớ của
        // context vừa ghi.
        await using var freshDb = CreateDbContext(dbName);
        var assets = await freshDb.MediaAssets.ToListAsync();
        Assert.Equal(3, assets.Count); // 1 library + 2 derivatives

        var library = Assert.Single(assets, a => a.IsInLibrary);
        Assert.Equal(result.LibraryAsset.Id, library.Id);
        Assert.Equal(2, assets.Count(a => !a.IsInLibrary && a.SourceAssetId == library.Id));

        var shop = await freshDb.Shops.IgnoreQueryFilters().FirstAsync(s => s.Id == shopId);
        Assert.Equal(library.Id, shop.LogoId);
    }

    // ---- PNG trong suốt 1000×200 -> phái sinh 320x96,inside có kích thước 320×64, giữ alpha ----

    [Fact]
    public async Task Transparent_1000x200_png_produces_320x64_inside_derivative_with_alpha()
    {
        await using var db = CreateDbContext();
        var shopId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _tenantContext.ShopId = shopId;
        await SeedShopAsync(db, shopId);

        var handler = CreateHandler(db);

        using var sourceStream = new MemoryStream(ReadTestAsset("logo-alpha-1000x200.png"));
        var command = new UploadShopLogoCommand(shopId, sourceStream, "logo.png");

        var result = await handler.Handle(command, CancellationToken.None);

        var insideDerivative = Assert.Single(result.Derivatives, d => d.Preset == InsidePreset.Name);
        Assert.Equal(320, insideDerivative.Width);
        Assert.Equal(64, insideDerivative.Height);

        var stored = await _storage.OpenReadAsync(insideDerivative.StorageKey, CancellationToken.None);
        Assert.NotNull(stored);
        await using var content = stored!.Content;
        using var image = await Image.LoadAsync<Rgba32>(content);
        Assert.Equal(320, image.Width);
        Assert.Equal(64, image.Height);

        // Nguồn trong suốt ở nửa phải (900px trở đi trên trục 1000px gốc) -> vùng tương ứng bên phải
        // của derivative (đã scale, không crop vì fit "inside") vẫn phải giữ alpha thấp.
        var transparentPixel = image[image.Width - 1, image.Height / 2];
        Assert.True(transparentPixel.A < 50, $"Kỳ vọng vùng trong suốt còn alpha thấp, thực tế A={transparentPixel.A}.");
    }

    // ---- upload lần hai -> LogoId đổi sang bản mới, bản cũ vẫn còn trong Library (không xoá gì) ----

    [Fact]
    public async Task Second_upload_switches_LogoId_and_keeps_old_library_record()
    {
        await using var db = CreateDbContext();
        var shopId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _tenantContext.ShopId = shopId;
        await SeedShopAsync(db, shopId);

        var handler = CreateHandler(db);

        using var firstStream = new MemoryStream(ReadTestAsset("logo-alpha-1000x200.png"));
        var first = await handler.Handle(new UploadShopLogoCommand(shopId, firstStream, "logo1.png"), CancellationToken.None);

        using var secondStream = new MemoryStream(ReadTestAsset("logo-alpha-1000x200.png"));
        var second = await handler.Handle(new UploadShopLogoCommand(shopId, secondStream, "logo2.png"), CancellationToken.None);

        Assert.NotEqual(first.LibraryAsset.Id, second.LibraryAsset.Id);

        var shop = await db.Shops.IgnoreQueryFilters().FirstAsync(s => s.Id == shopId);
        Assert.Equal(second.LibraryAsset.Id, shop.LogoId);

        // Bản cũ vẫn còn (KHÔNG bị xoá) — vẫn thấy qua query mặc định (không soft-deleted).
        var oldLibrary = await db.MediaAssets.FirstOrDefaultAsync(a => a.Id == first.LibraryAsset.Id);
        Assert.NotNull(oldLibrary);
        Assert.True(oldLibrary!.IsInLibrary);

        // Tổng 6 record: 2 library (2 upload) + 2 derivative mỗi lần.
        var totalAssets = await db.MediaAssets.CountAsync();
        Assert.Equal(6, totalAssets);
    }

    [Fact]
    public async Task Usage_increases_by_exactly_the_library_assets_SizeBytes()
    {
        await using var db = CreateDbContext();
        var shopId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _tenantContext.ShopId = shopId;
        await SeedShopAsync(db, shopId);

        var usageHandler = new GetUsageHandler(db);
        var before = await usageHandler.Handle(new GetUsageQuery(shopId), CancellationToken.None);
        Assert.Equal(0, before.UsedBytes);

        var handler = CreateHandler(db);
        using var sourceStream = new MemoryStream(ReadTestAsset("logo-alpha-1000x200.png"));
        var result = await handler.Handle(new UploadShopLogoCommand(shopId, sourceStream, "logo.png"), CancellationToken.None);

        var after = await usageHandler.Handle(new GetUsageQuery(shopId), CancellationToken.None);
        Assert.Equal(result.LibraryAsset.SizeBytes, after.UsedBytes);
    }

    // ---- Review sau T7: preset name trong derivative catalog KHÔNG có trong IImagePresetCatalog ->
    // InvalidOperationException (lỗi cấu hình) NÉM RA TRƯỚC khi đụng ảnh (LoadAsync/WriteLibraryAsync)
    // -> storage rỗng, KHÔNG mồ côi file Library/derivative trước đó (fix: resolve toàn bộ preset
    // name -> ImagePreset TRƯỚC WriteLibraryAsync, xem UploadShopLogoHandler) ----

    [Fact]
    public async Task Upload_with_derivative_preset_name_missing_from_catalog_throws_before_writing_any_file()
    {
        await using var db = CreateDbContext();
        var shopId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _tenantContext.ShopId = shopId;
        await SeedShopAsync(db, shopId);

        // _presetCatalog chỉ có InsidePreset/CoverPreset — "does-not-exist,inside" không tồn tại.
        var handler = new UploadShopLogoHandler(
            _processor,
            _presetCatalog,
            new FakeDerivativePresetCatalog(("Shop", ["does-not-exist,inside"])),
            CreateWriter(db),
            new ShopLogoWriter(db));

        using var sourceStream = new MemoryStream(ReadTestAsset("logo-alpha-1000x200.png"));
        var command = new UploadShopLogoCommand(shopId, sourceStream, "logo.png");

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));

        Assert.Empty(Directory.GetFiles(_storageRoot, "*", SearchOption.AllDirectories));
        Assert.Empty(await db.MediaAssets.ToListAsync());

        var shop = await db.Shops.IgnoreQueryFilters().FirstAsync(s => s.Id == shopId);
        Assert.Null(shop.LogoId);
    }

    // ---- R4: SaveChangesAsync ném lỗi -> best-effort xoá mọi file đã ghi (library + derivatives),
    // Shop.LogoId KHÔNG bị đổi ----

    [Fact]
    public async Task When_SaveChanges_throws_deletes_all_written_files_and_does_not_change_LogoId()
    {
        var dbName = Guid.NewGuid().ToString("N");
        await using var db = CreateDbContext(dbName);
        var throwingDb = new ThrowingDbContext(db);
        var shopId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _tenantContext.ShopId = shopId;
        await SeedShopAsync(db, shopId);

        var handler = CreateHandler(throwingDb);

        using var sourceStream = new MemoryStream(ReadTestAsset("logo-alpha-1000x200.png"));
        var command = new UploadShopLogoCommand(shopId, sourceStream, "logo.png");

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));

        Assert.Empty(Directory.GetFiles(_storageRoot, "*", SearchOption.AllDirectories));

        // Đọc lại từ một AppDbContext MỚI (ChangeTracker rỗng, cùng InMemory database name) —
        // `db`/`throwingDb` share CÙNG ChangeTracker nên entity Shop đang track vẫn giữ LogoId đã
        // set trong bộ nhớ (SaveChangesAsync ném lỗi KHÔNG tự revert property đã gán); chỉ context
        // mới đọc đúng trạng thái đã PERSIST (chưa đổi gì).
        await using var freshDb = CreateDbContext(dbName);
        var persistedShop = await freshDb.Shops.IgnoreQueryFilters().FirstAsync(s => s.Id == shopId);
        Assert.Null(persistedShop.LogoId);
    }

    // ---- helpers ----

    private UploadShopLogoHandler CreateHandler(IAppDbContext db) =>
        new(
            _processor,
            _presetCatalog,
            new FakeDerivativePresetCatalog(("Shop", [InsidePreset.Name, CoverPreset.Name])),
            CreateWriter(db),
            new ShopLogoWriter(db));

    private MediaAssetWriter CreateWriter(IAppDbContext db) =>
        new(db, _storage, TimeProvider.System, Options.Create(new ImageUploadOptions()));

    private static async Task SeedShopAsync(AppDbContext db, Guid shopId)
    {
        db.Shops.Add(new ShopEntity(shopId, "Shop", $"shop-{shopId:N}", Vsite.Domain.Shop.Enums.ShopKind.Hosted));
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private AppDbContext CreateDbContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options, _tenantContext);
    }

    private static byte[] ReadTestAsset(string fileName) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestAssets", fileName));

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

    private sealed class FakeDerivativePresetCatalog(params (string Source, IReadOnlyList<string> Presets)[] entries) : IDerivativePresetCatalog
    {
        private readonly Dictionary<string, IReadOnlyList<string>> _bySource =
            entries.ToDictionary(e => e.Source, e => e.Presets);

        public IReadOnlyList<string> For(string source) =>
            _bySource.TryGetValue(source, out var names) ? names : Array.Empty<string>();
    }



    /// <summary>R4: forward mọi DbSet cho instance InMemory thật, nhưng <see cref="SaveChangesAsync"/>
    /// luôn ném lỗi TRƯỚC khi chạm DB thật (cùng khuôn `UploadHandlerTests.ThrowingDbContext`).</summary>
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

    /// <summary>Đếm số lần <see cref="SaveChangesAsync"/> được gọi qua instance này, forward thật
    /// xuống <paramref name="inner"/> mỗi lần — dùng để CHỨNG MINH "đúng MỘT SaveChangesAsync cho cả
    /// insert MediaAsset lẫn update Shop.LogoId" thay vì chỉ suy luận từ record cuối cùng trong DB
    /// (review sau T7).</summary>
    private sealed class CountingSaveChangesDbContext(IAppDbContext inner) : IAppDbContext
    {
        public int SaveChangesCallCount { get; private set; }

        public DbSet<User> Users => inner.Users;
        public DbSet<ExternalLogin> ExternalLogins => inner.ExternalLogins;
        public DbSet<Role> Roles => inner.Roles;
        public DbSet<UserShop> UserShops => inner.UserShops;
        public DbSet<PendingRegistration> PendingRegistrations => inner.PendingRegistrations;
        public DbSet<RefreshToken> RefreshTokens => inner.RefreshTokens;
        public DbSet<PasswordResetToken> PasswordResetTokens => inner.PasswordResetTokens;
        public DbSet<ShopEntity> Shops => inner.Shops;
        public DbSet<MediaAsset> MediaAssets => inner.MediaAssets;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCallCount++;
            return inner.SaveChangesAsync(cancellationToken);
        }
    }
}
