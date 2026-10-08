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
using Vsite.Application.Media.Commands.CloneFromLibrary;
using Vsite.Application.Media.Commands.DeleteFromLibrary;
using Vsite.Application.Media.Commands.UploadToLibrary;
using Vsite.Application.Media.Commands.UploadToSlot;
using Vsite.Application.Media.Queries.GetAssetsByIds;
using Vsite.Application.Media.Queries.GetReferences;
using Vsite.Application.Media.Queries.GetUsage;
using Vsite.Application.Media.Queries.ListLibrary;
using Vsite.Application.Shop.Interfaces;
using Vsite.Domain.Abstractions;
using Vsite.Domain.Exceptions;
using Vsite.Infrastructure.Imaging;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests.Media;

/// <summary>
/// T6, MEDIA-001 (#55, #71, #72) — test handler-level KHÔNG cần Docker, cùng khuôn
/// `UploadHandlerTests` (EF Core InMemory + <see cref="ImageSharpImageProcessor"/> +
/// <see cref="LocalDiskObjectStorage"/> thật trong thư mục temp). Bù cho
/// `LibraryEndpointTests` (Docker, Postgres+Redis Testcontainers) không chạy được trên máy không có
/// Docker daemon — xem `Docs/DOCKER-TEST-DEBT.md`.
///
/// Chạy: <c>dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~LibraryHandlerTests"</c>.
/// </summary>
public sealed class LibraryHandlerTests : IDisposable
{
    private readonly string _storageRoot;
    private readonly IObjectStorage _storage;
    private readonly IImageProcessor _processor;
    private readonly FakePresetCatalog _presetCatalog;
    private readonly FakeTenantContext _tenantContext;

    private static readonly ImagePreset CoverPreset = new("test-800x600,cover", 800, 600, PresetFit.Cover);

    public LibraryHandlerTests()
    {
        _storageRoot = Path.Combine(Path.GetTempPath(), "vsite-media-library-test-" + Guid.NewGuid());
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

    // ---- Test bắt buộc 5: clone -> record mới, StorageKey khác bản Library, SourceAssetId đúng,
    // kích thước đúng preset ----

    [Fact]
    public async Task Clone_creates_new_record_with_different_key_correct_source_and_preset_size()
    {
        await using var db = CreateDbContext();
        var writer = CreateWriter(db);
        var shopId = Guid.NewGuid();
        _tenantContext.ShopId = shopId;

        var library = await UploadLibraryAsync(db, writer, shopId, "photo.jpg");

        var cloneHandler = CreateCloneHandler(db, writer);
        var command = new CloneFromLibraryCommand(shopId, library.Id, CoverPreset.Name, FocalX: null, FocalY: null);

        var result = await cloneHandler.Handle(command, CancellationToken.None);

        Assert.NotEqual(library.StorageKey, result.StorageKey);
        Assert.Equal(library.Id, result.SourceAssetId);
        Assert.Equal(CoverPreset.Name, result.Preset);
        Assert.False(result.IsInLibrary);
        Assert.Equal(CoverPreset.Width, result.Width);
        Assert.Equal(CoverPreset.Height, result.Height);

        var stored = await _storage.OpenReadAsync(result.StorageKey, CancellationToken.None);
        Assert.NotNull(stored);
        await using var content = stored!.Content;
        using var image = await Image.LoadAsync(content);
        Assert.Equal(CoverPreset.Width, image.Width);
        Assert.Equal(CoverPreset.Height, image.Height);
    }

    // ---- clone từ id là clone -> 404 (không lộ tồn tại) ----

    [Fact]
    public async Task Clone_of_a_clone_id_returns_NotFound()
    {
        await using var db = CreateDbContext();
        var writer = CreateWriter(db);
        var shopId = Guid.NewGuid();
        _tenantContext.ShopId = shopId;

        var library = await UploadLibraryAsync(db, writer, shopId, "photo.jpg");
        var cloneHandler = CreateCloneHandler(db, writer);
        var clone = await cloneHandler.Handle(
            new CloneFromLibraryCommand(shopId, library.Id, CoverPreset.Name, null, null), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            cloneHandler.Handle(new CloneFromLibraryCommand(shopId, clone.Id, CoverPreset.Name, null, null), CancellationToken.None));

        Assert.Equal("MEDIAASSET_NOT_FOUND", ex.ErrorCode);
    }

    // ---- clone asset của shop B -> 404 (Global Query Filter ràng ShopId trong CÙNG query) ----

    [Fact]
    public async Task Clone_of_another_shops_asset_returns_NotFound()
    {
        await using var db = CreateDbContext();
        var writer = CreateWriter(db);
        var shopAId = Guid.NewGuid();
        var shopBId = Guid.NewGuid();

        _tenantContext.ShopId = shopBId;
        var libraryB = await UploadLibraryAsync(db, writer, shopBId, "photo.jpg");

        _tenantContext.ShopId = shopAId;
        var cloneHandler = CreateCloneHandler(db, writer);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            cloneHandler.Handle(new CloneFromLibraryCommand(shopAId, libraryB.Id, CoverPreset.Name, null, null), CancellationToken.None));

        Assert.Equal("MEDIAASSET_NOT_FOUND", ex.ErrorCode);
    }

    // ---- Preset lạ -> 422 MEDIA_UNKNOWN_PRESET (cùng khuôn UploadToSlotHandler) ----

    [Fact]
    public async Task Clone_with_unknown_preset_throws_Unprocessable_MEDIA_UNKNOWN_PRESET()
    {
        await using var db = CreateDbContext();
        var writer = CreateWriter(db);
        var shopId = Guid.NewGuid();
        _tenantContext.ShopId = shopId;

        var library = await UploadLibraryAsync(db, writer, shopId, "photo.jpg");
        var cloneHandler = CreateCloneHandler(db, writer);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            cloneHandler.Handle(new CloneFromLibraryCommand(shopId, library.Id, "does-not-exist", null, null), CancellationToken.None));

        Assert.Equal("MEDIA_UNKNOWN_PRESET", ex.ErrorCode);
    }

    // ---- Test bắt buộc 9: xoá bản Library -> list không còn; GetAssetsByIds([cloneId]) vẫn trả
    // clone, file clone vẫn mở được. Review fix (#72): clone KHÔNG được mất SourceAssetId qua cascade
    // fix-up của EF Core (DeleteBehavior.SetNull) — verify bằng cách đọc lại clone từ một
    // AppDbContext MỚI (không tracking chung với context vừa xoá), và usage phải giảm đúng
    // library.SizeBytes (clone chưa bao giờ tính vào usage vì SourceAssetId != null). ----

    [Fact]
    public async Task Delete_library_then_list_excludes_it_but_clone_still_resolves_and_file_still_opens()
    {
        var dbName = Guid.NewGuid().ToString("N");
        await using var db = CreateDbContext(dbName);
        var writer = CreateWriter(db);
        var shopId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _tenantContext.ShopId = shopId;

        var library = await UploadLibraryAsync(db, writer, shopId, "photo.jpg");
        var cloneHandler = CreateCloneHandler(db, writer);
        var clone = await cloneHandler.Handle(
            new CloneFromLibraryCommand(shopId, library.Id, CoverPreset.Name, null, null), CancellationToken.None);

        var usageHandler = new GetUsageHandler(db);
        var usageBefore = await usageHandler.Handle(new GetUsageQuery(shopId), CancellationToken.None);
        Assert.Equal(library.SizeBytes, usageBefore.UsedBytes);

        var deleteHandler = new DeleteFromLibraryHandler(db);
        await deleteHandler.Handle(new DeleteFromLibraryCommand(shopId, library.Id), CancellationToken.None);

        var listHandler = new ListLibraryHandler(db);
        var page = await listHandler.Handle(new ListLibraryQuery(shopId, 1, 24), CancellationToken.None);
        Assert.DoesNotContain(page.Items, a => a.Id == library.Id);

        var lookupHandler = new GetAssetsByIdsHandler(db);
        var found = await lookupHandler.Handle(new GetAssetsByIdsQuery(shopId, [clone.Id]), CancellationToken.None);
        var foundClone = Assert.Single(found);
        Assert.Equal(clone.Id, foundClone.Id);
        Assert.Equal(library.Id, foundClone.SourceAssetId);

        // Đọc lại từ một AppDbContext MỚI, cùng InMemory database name nhưng KHÔNG chia sẻ
        // ChangeTracker/identity map với `db` — chứng minh giá trị đã PERSIST đúng, không phải chỉ
        // "đọc lại instance đang tracked" (điều mà `foundClone.SourceAssetId` ở trên chưa loại trừ
        // hết, vì `GetAssetsByIdsHandler` vẫn dùng CHUNG `db`).
        await using (var freshDb = CreateDbContext(dbName))
        {
            var persistedClone = await freshDb.MediaAssets.IgnoreQueryFilters().FirstAsync(a => a.Id == clone.Id);
            Assert.Equal(library.Id, persistedClone.SourceAssetId);
            Assert.False(persistedClone.IsDeleted);

            var persistedLibrary = await freshDb.MediaAssets.IgnoreQueryFilters().FirstAsync(a => a.Id == library.Id);
            Assert.True(persistedLibrary.IsDeleted);
        }

        var stored = await _storage.OpenReadAsync(foundClone.StorageKey, CancellationToken.None);
        Assert.NotNull(stored);
        await stored!.Content.DisposeAsync();

        // Library row ngừng tính vào usage (soft-deleted); clone chưa bao giờ tính (SourceAssetId !=
        // null) — usage giảm ĐÚNG BẰNG SizeBytes của bản Library, không phải về 0 và không đổi.
        var usageAfter = await usageHandler.Handle(new GetUsageQuery(shopId), CancellationToken.None);
        Assert.Equal(usageBefore.UsedBytes - library.SizeBytes, usageAfter.UsedBytes);
    }

    // ---- delete asset không phải Library (hoặc id lạ) -> 404 ----

    [Fact]
    public async Task Delete_of_non_library_asset_returns_NotFound()
    {
        await using var db = CreateDbContext();
        var writer = CreateWriter(db);
        var slotHandler = new UploadToSlotHandler(_processor, _presetCatalog, writer);
        var shopId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _tenantContext.ShopId = shopId;

        using var sourceStream = new MemoryStream(EncodeJpeg(1600, 1200));
        var direct = await slotHandler.Handle(
            new UploadToSlotCommand(shopId, sourceStream, "photo.jpg", CoverPreset.Name, 0.5f, 0.5f, false, null),
            CancellationToken.None);

        var deleteHandler = new DeleteFromLibraryHandler(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            deleteHandler.Handle(new DeleteFromLibraryCommand(shopId, direct.Asset.Id), CancellationToken.None));
    }

    // ---- Test bắt buộc 10: usage trước/sau clone KHÔNG đổi; sau upload thẳng thì tăng đúng
    // SizeBytes ----

    [Fact]
    public async Task Usage_unchanged_after_clone_but_increases_by_SizeBytes_after_direct_upload()
    {
        await using var db = CreateDbContext();
        var writer = CreateWriter(db);
        var shopId = Guid.NewGuid();
        _tenantContext.ShopId = shopId;

        var usageHandler = new GetUsageHandler(db);
        var before = await usageHandler.Handle(new GetUsageQuery(shopId), CancellationToken.None);
        Assert.Equal(0, before.UsedBytes);

        var library = await UploadLibraryAsync(db, writer, shopId, "photo.jpg");
        var afterLibrary = await usageHandler.Handle(new GetUsageQuery(shopId), CancellationToken.None);
        Assert.Equal(library.SizeBytes, afterLibrary.UsedBytes);

        var cloneHandler = CreateCloneHandler(db, writer);
        await cloneHandler.Handle(new CloneFromLibraryCommand(shopId, library.Id, CoverPreset.Name, null, null), CancellationToken.None);

        var afterClone = await usageHandler.Handle(new GetUsageQuery(shopId), CancellationToken.None);
        Assert.Equal(afterLibrary.UsedBytes, afterClone.UsedBytes);

        var slotHandler = new UploadToSlotHandler(_processor, _presetCatalog, writer);
        using var directStream = new MemoryStream(EncodeJpeg(1600, 1200));
        var direct = await slotHandler.Handle(
            new UploadToSlotCommand(shopId, directStream, "direct.jpg", CoverPreset.Name, 0.5f, 0.5f, false, null),
            CancellationToken.None);

        var afterDirect = await usageHandler.Handle(new GetUsageQuery(shopId), CancellationToken.None);
        Assert.Equal(afterClone.UsedBytes + direct.Asset.SizeBytes, afterDirect.UsedBytes);
    }

    // ---- list phân trang đúng shape; không chứa record IsInLibrary=false ----

    [Fact]
    public async Task List_excludes_non_library_records_and_pages_correctly()
    {
        await using var db = CreateDbContext();
        var writer = CreateWriter(db);
        var shopId = Guid.NewGuid();
        _tenantContext.ShopId = shopId;

        for (var i = 0; i < 3; i++)
        {
            await UploadLibraryAsync(db, writer, shopId, $"photo{i}.jpg");
        }

        var slotHandler = new UploadToSlotHandler(_processor, _presetCatalog, writer);
        using var directStream = new MemoryStream(EncodeJpeg(1600, 1200));
        await slotHandler.Handle(
            new UploadToSlotCommand(shopId, directStream, "direct.jpg", CoverPreset.Name, 0.5f, 0.5f, false, null),
            CancellationToken.None);

        var listHandler = new ListLibraryHandler(db);

        var page1 = await listHandler.Handle(new ListLibraryQuery(shopId, 1, 2), CancellationToken.None);
        Assert.Equal(3, page1.Total);
        Assert.Equal(2, page1.Items.Count);
        Assert.All(page1.Items, a => Assert.True(a.IsInLibrary));

        var page2 = await listHandler.Handle(new ListLibraryQuery(shopId, 2, 2), CancellationToken.None);
        Assert.Single(page2.Items);
        Assert.Equal(2, page2.Page);
        Assert.Equal(2, page2.PageSize);
    }

    // ---- GetAssetsByIds chứa id của shop B -> id đó KHÔNG có trong kết quả; nhưng vẫn trả record
    // soft-deleted CÙNG shop ----

    [Fact]
    public async Task GetAssetsByIds_excludes_other_shops_ids_but_includes_same_shop_soft_deleted()
    {
        await using var db = CreateDbContext();
        var writer = CreateWriter(db);
        var shopAId = Guid.NewGuid();
        var shopBId = Guid.NewGuid();
        var ownerAId = Guid.NewGuid();

        _tenantContext.ShopId = shopAId;
        var libraryA = await UploadLibraryAsync(db, writer, shopAId, "a.jpg");

        _tenantContext.ShopId = shopBId;
        var libraryB = await UploadLibraryAsync(db, writer, shopBId, "b.jpg");

        _tenantContext.ShopId = shopAId;
        var deleteHandler = new DeleteFromLibraryHandler(db);
        await deleteHandler.Handle(new DeleteFromLibraryCommand(shopAId, libraryA.Id), CancellationToken.None);

        var lookupHandler = new GetAssetsByIdsHandler(db);
        var result = await lookupHandler.Handle(
            new GetAssetsByIdsQuery(shopAId, [libraryA.Id, libraryB.Id]), CancellationToken.None);

        var found = Assert.Single(result);
        Assert.Equal(libraryA.Id, found.Id);
    }

    // ---- >200 id -> validator FAIL ----

    [Fact]
    public async Task GetAssetsByIdsValidator_rejects_more_than_200_ids()
    {
        var validator = new GetAssetsByIdsValidator();
        var ids = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToList();
        var query = new GetAssetsByIdsQuery(Guid.NewGuid(), ids);

        var result = await validator.ValidateAsync(query);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task GetAssetsByIdsValidator_accepts_exactly_200_ids()
    {
        var validator = new GetAssetsByIdsValidator();
        var ids = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid()).ToList();
        var query = new GetAssetsByIdsQuery(Guid.NewGuid(), ids);

        var result = await validator.ValidateAsync(query);

        Assert.True(result.IsValid);
    }

    // ---- References: chưa là logo -> rỗng; là Shop.LogoId -> ShopLogo ----

    [Fact]
    public async Task GetReferences_returns_ShopLogo_when_asset_is_the_shops_logo()
    {
        await using var db = CreateDbContext();
        var writer = CreateWriter(db);
        var shopId = Guid.NewGuid();
        _tenantContext.ShopId = shopId;

        var library = await UploadLibraryAsync(db, writer, shopId, "logo.jpg");

        var referencesHandler = new GetReferencesHandler(db);
        var before = await referencesHandler.Handle(new GetReferencesQuery(shopId, library.Id), CancellationToken.None);
        Assert.Empty(before.References);

        var shop = new Vsite.Domain.Shop.Entities.Shop(shopId, "Shop", $"shop-{shopId:N}", Vsite.Domain.Shop.Enums.ShopKind.Hosted);
        shop.SetLogo(library.Id);
        db.Shops.Add(shop);
        await db.SaveChangesAsync(CancellationToken.None);

        var after = await referencesHandler.Handle(new GetReferencesQuery(shopId, library.Id), CancellationToken.None);
        var reference = Assert.Single(after.References);
        Assert.Equal(Vsite.Application.Media.Dtos.MediaReferenceKind.ShopLogo, reference.Kind);
    }

    // ---- helpers ----

    private async Task<Vsite.Application.Media.Dtos.MediaAssetDto> UploadLibraryAsync(
        AppDbContext db, MediaAssetWriter writer, Guid shopId, string fileName)
    {
        var handler = new UploadToLibraryHandler(_processor, writer);
        using var stream = new MemoryStream(EncodeJpeg(1600, 1200));
        return await handler.Handle(new UploadToLibraryCommand(shopId, stream, fileName, null, null), CancellationToken.None);
    }

    private CloneFromLibraryHandler CreateCloneHandler(IAppDbContext db, MediaAssetWriter writer) =>
        new(db, _storage, _processor, _presetCatalog, writer);

    private MediaAssetWriter CreateWriter(IAppDbContext db) =>
        new(db, _storage, TimeProvider.System, Options.Create(new ImageUploadOptions()));

    /// <summary>Truyền <paramref name="databaseName"/> để tạo MỘT AppDbContext MỚI (ChangeTracker
    /// rỗng) trỏ vào CÙNG InMemory database — dùng để đọc lại dữ liệu đã persist mà không bị "che" bởi
    /// identity map của context đã dùng để ghi (xem test bắt buộc 9).</summary>
    private AppDbContext CreateDbContext(string? databaseName = null)
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N"))
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


}
