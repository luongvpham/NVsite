using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vsite.Application.Common.Imaging;
using Vsite.Application.Media.Interfaces;
using Vsite.Application.Media.Jobs;
using Vsite.Application.Media.Options;
using Vsite.Domain.Media.Entities;
using Vsite.Domain.Website.Entities;
using Vsite.Infrastructure.Website;
using Vsite.IntegrationTests.Website;
using static Vsite.IntegrationTests.Website.WebsiteHandlerKit;

namespace Vsite.IntegrationTests.Media;

/// <summary>
/// WEBSITE-008 T7 (#117, #116) — <see cref="MediaOrphanCleanupJob"/> trên Postgres thật. Nguồn "ảnh đang dùng" là ba cài
/// đặt thật của Website (draft, snapshot, FK SEO); storage giả ghi lại key đã xoá và có thể bị bảo ném.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class MediaOrphanCleanupJobTests(PostgresFixture postgres) : IAsyncLifetime
{
    // CreatedAt do AppDbContext đóng dấu bằng đồng hồ thật ⇒ neo "now" vào đồng hồ thật rồi lùi created_at bằng SQL.
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static TimeSpan Days(double d) => TimeSpan.FromDays(d);

    private string Cs => postgres.ConnectionString;

    public Task InitializeAsync() => MigrateAsync(Cs);

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------- seeding ----------

    private async Task<MediaAsset> AddAsync(Guid shopId, Func<MediaAsset> create, TimeSpan createdAgo, bool softDelete = false)
    {
        await using var db = NewDb(Cs, shopId);
        var asset = create();
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();
        if (softDelete)
        {
            asset.SoftDeleteFromLibrary();
            await db.SaveChangesAsync();
        }

        var at = Now - createdAgo;
        await db.MediaAssets.IgnoreQueryFilters().Where(m => m.Id == asset.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.CreatedAt, at));
        return asset;
    }

    private Task<MediaAsset> AddLibraryAsync(Guid shopId, TimeSpan createdAgo, bool deleted = false) =>
        AddAsync(shopId, () => MediaAsset.NewLibrary(shopId, Key(shopId, "library"), 100, 100, 10, 0.5f, 0.5f, null, null), createdAgo, deleted);

    private Task<MediaAsset> AddCloneAsync(Guid shopId, MediaAsset library, TimeSpan createdAgo) =>
        AddAsync(shopId, () => MediaAsset.NewClone(library, Key(shopId, "clone"), 100, 100, 10, "p,cover", 0.5f, 0.5f), createdAgo);

    private Task<MediaAsset> AddDirectAsync(Guid shopId, TimeSpan createdAgo) =>
        AddAsync(shopId, () => MediaAsset.NewDirect(shopId, Key(shopId, "direct"), 100, 100, 10, "p,cover", 0.5f, 0.5f, null, null), createdAgo);

    private Task<MediaAsset> AddDerivativeAsync(Guid shopId, MediaAsset library, string preset, TimeSpan createdAgo) =>
        AddAsync(shopId, () => MediaAsset.NewDerivative(library, Key(shopId, "derivative"), 50, 50, 5, preset, 0.5f, 0.5f), createdAgo);

    private static string Key(Guid shopId, string kind) => $"shops/{shopId}/{kind}/{Guid.NewGuid():N}.webp";

    // ---------- running the job ----------

    private async Task RunAsync(Guid shopId, DateTimeOffset at, RecordingStorage storage, params IMediaUsageSource[] extraSources)
    {
        await using var request = new Request(Cs, shopId);
        IMediaUsageSource[] sources =
        [
            new PageDraftUsageSource(request.Db, request.Tenant, S.Validator),
            new SitePublicationUsageSource(request.Db, request.Tenant, S.Versioning),
            new SeoImageUsageSource(request.Db, request.Tenant),
            .. extraSources,
        ];
        var job = new MediaOrphanCleanupJob(
            request.Db, request.Tenant, sources, storage, Options.Create(new MediaCleanupOptions()), new FixedTime(at),
            NullLogger<MediaOrphanCleanupJob>.Instance);
        await job.ExecuteAsync(default);
    }

    /// <summary>null = record không còn.</summary>
    private async Task<(bool Exists, DateTimeOffset? OrphanedAt)> StateAsync(Guid assetId)
    {
        await using var db = NewDb(Cs, null);
        var row = await db.MediaAssets.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.Id == assetId).Select(m => new { m.OrphanedAt }).SingleOrDefaultAsync();
        return row is null ? (false, null) : (true, row.OrphanedAt);
    }

    private static void AssertMarkedAt(DateTimeOffset expected, (bool Exists, DateTimeOffset? OrphanedAt) state)
    {
        Assert.True(state.Exists);
        Assert.NotNull(state.OrphanedAt);
        Assert.True((state.OrphanedAt!.Value - expected).Duration() < TimeSpan.FromMilliseconds(1), $"OrphanedAt={state.OrphanedAt:O}, mong {expected:O}");
    }

    private static void AssertUntouched((bool Exists, DateTimeOffset? OrphanedAt) state)
    {
        Assert.True(state.Exists);
        Assert.Null(state.OrphanedAt);
    }

    // ---------- tests ----------

    [Fact]
    public async Task Unused_clone_and_direct_8_days_old_are_marked_then_deleted_with_file_after_grace()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var library = await AddLibraryAsync(seeded.ShopId, Days(30));
        var clone = await AddCloneAsync(seeded.ShopId, library, Days(8));
        var direct = await AddDirectAsync(seeded.ShopId, Days(8));
        var storage = new RecordingStorage();

        await RunAsync(seeded.ShopId, Now, storage);

        AssertMarkedAt(Now, await StateAsync(clone.Id));
        AssertMarkedAt(Now, await StateAsync(direct.Id));
        Assert.Empty(storage.Deleted);

        await RunAsync(seeded.ShopId, Now + Days(8), storage);

        Assert.False((await StateAsync(clone.Id)).Exists);
        Assert.False((await StateAsync(direct.Id)).Exists);
        Assert.Equal(new[] { clone.StorageKey, direct.StorageKey }.Order(), storage.Deleted.Order());
        AssertUntouched(await StateAsync(library.Id));
    }

    [Fact]
    public async Task Marked_clone_is_not_deleted_before_grace_has_passed_since_the_mark()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var library = await AddLibraryAsync(seeded.ShopId, Days(30));
        var clone = await AddCloneAsync(seeded.ShopId, library, Days(30));
        var storage = new RecordingStorage();

        await RunAsync(seeded.ShopId, Now, storage);
        await RunAsync(seeded.ShopId, Now + Days(6), storage);

        AssertMarkedAt(Now, await StateAsync(clone.Id));
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task Clone_created_6_days_ago_is_not_marked_R8()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var library = await AddLibraryAsync(seeded.ShopId, Days(30));
        var clone = await AddCloneAsync(seeded.ShopId, library, Days(6));
        var storage = new RecordingStorage();

        await RunAsync(seeded.ShopId, Now, storage);

        AssertUntouched(await StateAsync(clone.Id));
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task Clone_in_draft_or_only_in_a_snapshot_is_not_marked()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var library = await AddLibraryAsync(seeded.ShopId, Days(30));
        var inSnapshotOnly = await AddCloneAsync(seeded.ShopId, library, Days(8));
        var inDraft = await AddCloneAsync(seeded.ShopId, library, Days(8));
        await SaveTreeAsync(request, seeded.Home.Id, TreeWith(Hero("c_hero1", inSnapshotOnly.Id.ToString("D"))));
        await request.Publish();
        await SaveTreeAsync(request, seeded.Home.Id, TreeWith(Hero("c_hero1", inDraft.Id.ToString("D"))));
        var storage = new RecordingStorage();

        await RunAsync(seeded.ShopId, Now, storage);
        await RunAsync(seeded.ShopId, Now + Days(8), storage);

        AssertUntouched(await StateAsync(inSnapshotOnly.Id));
        AssertUntouched(await StateAsync(inDraft.Id));
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task Marked_clone_put_back_into_draft_saves_fine_and_is_unmarked_on_next_run_R4()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var library = await AddLibraryAsync(seeded.ShopId, Days(30));
        var clone = await AddCloneAsync(seeded.ShopId, library, Days(8));
        var storage = new RecordingStorage();

        await RunAsync(seeded.ShopId, Now, storage);
        AssertMarkedAt(Now, await StateAsync(clone.Id));

        // Autosave (handler thật, qua MediaReferenceValidator) KHÔNG bị chặn bởi dấu mồ côi.
        await SaveTreeAsync(request, seeded.Home.Id, TreeWith(Hero("c_hero1", clone.Id.ToString("D"))));

        await RunAsync(seeded.ShopId, Now + Days(8), storage);

        AssertUntouched(await StateAsync(clone.Id));
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task Live_library_and_its_derivatives_are_never_touched()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var library = await AddLibraryAsync(seeded.ShopId, Days(60));
        var derivative = await AddDerivativeAsync(seeded.ShopId, library, "logo-sm", Days(60));
        var storage = new RecordingStorage();

        await RunAsync(seeded.ShopId, Now, storage);
        await RunAsync(seeded.ShopId, Now + Days(8), storage);

        AssertUntouched(await StateAsync(library.Id));
        AssertUntouched(await StateAsync(derivative.Id));
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task Deleted_library_that_is_the_shop_logo_keeps_it_and_its_derivatives_R7()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var library = await AddLibraryAsync(seeded.ShopId, Days(60));
        var derivative = await AddDerivativeAsync(seeded.ShopId, library, "logo-sm", Days(60));
        await using (var db = NewDb(Cs, null))
        {
            var shop = await db.Shops.SingleAsync(s => s.Id == seeded.ShopId);
            shop.SetLogo(library.Id);
            await db.SaveChangesAsync();
        }

        await using (var db = NewDb(Cs, seeded.ShopId))
        {
            var tracked = await db.MediaAssets.SingleAsync(m => m.Id == library.Id);
            tracked.SoftDeleteFromLibrary();
            await db.SaveChangesAsync();
        }

        var storage = new RecordingStorage();

        await RunAsync(seeded.ShopId, Now, storage);
        await RunAsync(seeded.ShopId, Now + Days(8), storage);

        AssertUntouched(await StateAsync(library.Id));
        AssertUntouched(await StateAsync(derivative.Id));
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task Deleted_unused_library_is_marked_then_deleted_with_its_derivatives_but_used_clone_stays()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var library = await AddLibraryAsync(seeded.ShopId, Days(60));
        var derivative = await AddDerivativeAsync(seeded.ShopId, library, "logo-sm", Days(60));
        var usedClone = await AddCloneAsync(seeded.ShopId, library, Days(60));
        await SaveTreeAsync(request, seeded.Home.Id, TreeWith(Hero("c_hero1", usedClone.Id.ToString("D"))));
        await using (var db = NewDb(Cs, seeded.ShopId))
        {
            var tracked = await db.MediaAssets.SingleAsync(m => m.Id == library.Id);
            tracked.SoftDeleteFromLibrary();
            await db.SaveChangesAsync();
        }

        var storage = new RecordingStorage();

        await RunAsync(seeded.ShopId, Now, storage);

        AssertMarkedAt(Now, await StateAsync(library.Id));
        AssertMarkedAt(Now, await StateAsync(derivative.Id));
        AssertUntouched(await StateAsync(usedClone.Id));
        Assert.Empty(storage.Deleted);

        await RunAsync(seeded.ShopId, Now + Days(8), storage);

        Assert.False((await StateAsync(library.Id)).Exists);
        Assert.False((await StateAsync(derivative.Id)).Exists);
        AssertUntouched(await StateAsync(usedClone.Id));
        Assert.Equal(new[] { library.StorageKey, derivative.StorageKey }.Order(), storage.Deleted.Order());
    }

    [Fact]
    public async Task Asset_still_held_by_a_foreign_key_keeps_record_file_and_mark_and_the_next_asset_is_still_deleted_R6()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var held = await AddDirectAsync(seeded.ShopId, Days(30));
        var free = await AddDirectAsync(seeded.ShopId, Days(30));
        // Mọi FK tới media_asset của schema hiện tại đều SET NULL, trừ Shop.LogoId (đã nằm trong "used"). Dựng một bảng có FK
        // RESTRICT để giả lập "FK còn giữ" (R6) — vd. một FK tương lai hoặc race gán logo — và kiểm đường rollback thật.
        var table = $"t7_holder_{Guid.NewGuid():N}"[..24];
        await using var admin = NewDb(Cs, null);
        await admin.Database.ExecuteSqlRawAsync(
            $"CREATE TABLE {table} (asset_id uuid NOT NULL REFERENCES media_asset (id) ON DELETE RESTRICT)");
        try
        {
            await admin.Database.ExecuteSqlRawAsync($"INSERT INTO {table} (asset_id) VALUES ('{held.Id}')");
            var storage = new RecordingStorage();

            await RunAsync(seeded.ShopId, Now, storage);
            AssertMarkedAt(Now, await StateAsync(held.Id));

            await RunAsync(seeded.ShopId, Now + Days(8), storage);

            AssertMarkedAt(Now, await StateAsync(held.Id));
            Assert.DoesNotContain(held.StorageKey, storage.Deleted);
            Assert.False((await StateAsync(free.Id)).Exists);
            Assert.Equal(free.StorageKey, Assert.Single(storage.Deleted));
        }
        finally
        {
            await admin.Database.ExecuteSqlRawAsync($"DROP TABLE {table}");
        }
    }

    [Fact]
    public async Task Og_image_of_a_soft_deleted_page_is_not_held_db_sets_page_og_image_id_null()
    {
        // Ghim hành vi thật: fk_page_media_asset_og_image là ON DELETE SET NULL (og_image_id) (migration AddWebsiteModule sửa tay),
        // không Restrict như plan giả định. Trang đã xoá mềm không giữ được ảnh OG; DB tự gỡ con trỏ (T3 cũng gỡ sau 7 ngày).
        var seeded = await AddWebsiteAsync(Cs);
        var og = await AddDirectAsync(seeded.ShopId, Days(30));
        Guid pageId;
        await using (var db = NewDb(Cs, seeded.ShopId))
        {
            var page = Page.NewComposable(seeded.ShopId, seeded.Website.Id, $"p-{Guid.NewGuid():N}"[..12], "Trang", 99);
            page.SetOgImage(og.Id);
            db.Pages.Add(page);
            await db.SaveChangesAsync();
            page.SoftDelete();
            await db.SaveChangesAsync();
            pageId = page.Id;
        }

        var storage = new RecordingStorage();

        await RunAsync(seeded.ShopId, Now, storage);
        await RunAsync(seeded.ShopId, Now + Days(8), storage);

        Assert.False((await StateAsync(og.Id)).Exists);
        Assert.Equal(og.StorageKey, Assert.Single(storage.Deleted));
        await using var check = NewDb(Cs, null);
        Assert.Null(await check.Pages.IgnoreQueryFilters().Where(p => p.Id == pageId).Select(p => p.OgImageId).SingleAsync());
    }

    [Fact]
    public async Task Og_image_of_a_live_page_is_used_and_not_marked()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var og = await AddDirectAsync(seeded.ShopId, Days(30));
        await using (var db = NewDb(Cs, seeded.ShopId))
        {
            var page = Page.NewComposable(seeded.ShopId, seeded.Website.Id, $"p-{Guid.NewGuid():N}"[..12], "Trang", 99);
            page.SetOgImage(og.Id);
            db.Pages.Add(page);
            await db.SaveChangesAsync();
        }

        var storage = new RecordingStorage();

        await RunAsync(seeded.ShopId, Now, storage);
        await RunAsync(seeded.ShopId, Now + Days(8), storage);

        AssertUntouched(await StateAsync(og.Id));
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task Storage_failure_rolls_back_the_record_delete_and_a_later_run_retries()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var failing = await AddDirectAsync(seeded.ShopId, Days(30));
        var other = await AddDirectAsync(seeded.ShopId, Days(30));
        var storage = new RecordingStorage();

        await RunAsync(seeded.ShopId, Now, storage);
        storage.ThrowOn.Add(failing.StorageKey);
        await RunAsync(seeded.ShopId, Now + Days(8), storage);

        AssertMarkedAt(Now, await StateAsync(failing.Id));
        Assert.False((await StateAsync(other.Id)).Exists);
        Assert.Equal(other.StorageKey, Assert.Single(storage.Deleted));

        storage.ThrowOn.Clear();
        await RunAsync(seeded.ShopId, Now + Days(9), storage);

        Assert.False((await StateAsync(failing.Id)).Exists);
        Assert.Contains(failing.StorageKey, storage.Deleted);
    }

    [Fact]
    public async Task A_throwing_usage_source_aborts_the_run_without_marking_or_deleting_R5()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var markedEarlier = await AddDirectAsync(seeded.ShopId, Days(30));
        var storage = new RecordingStorage();
        await RunAsync(seeded.ShopId, Now, storage);
        AssertMarkedAt(Now, await StateAsync(markedEarlier.Id));
        var fresh = await AddDirectAsync(seeded.ShopId, Days(30));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunAsync(seeded.ShopId, Now + Days(8), storage, new ThrowingSource()));

        AssertMarkedAt(Now, await StateAsync(markedEarlier.Id));
        AssertUntouched(await StateAsync(fresh.Id));
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task Asset_reused_during_the_run_is_not_deleted_second_collection_before_delete_phase()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var asset = await AddDirectAsync(seeded.ShopId, Days(30));
        var storage = new RecordingStorage();
        await RunAsync(seeded.ShopId, Now, storage);
        AssertMarkedAt(Now, await StateAsync(asset.Id));

        // Lần thu 1 (đầu job) chưa thấy; lần thu 2 (trước pha xoá) thấy — mô phỏng chủ shop đưa ảnh lại vào draft giữa chừng.
        var source = new UsedFromCall(2, asset.Id);
        await RunAsync(seeded.ShopId, Now + Days(8), storage, source);

        Assert.Equal(2, source.Calls);
        Assert.True((await StateAsync(asset.Id)).Exists);
        Assert.Empty(storage.Deleted);

        // Lần sau nguồn vẫn thấy ảnh ⇒ bỏ dấu.
        await RunAsync(seeded.ShopId, Now + Days(9), storage, new UsedFromCall(1, asset.Id));
        AssertUntouched(await StateAsync(asset.Id));
    }

    [Fact]
    public async Task Second_collection_throwing_aborts_without_deleting_R5()
    {
        var seeded = await AddWebsiteAsync(Cs);
        var asset = await AddDirectAsync(seeded.ShopId, Days(30));
        var storage = new RecordingStorage();
        await RunAsync(seeded.ShopId, Now, storage);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunAsync(seeded.ShopId, Now + Days(8), storage, new ThrowingFromCall(2)));

        AssertMarkedAt(Now, await StateAsync(asset.Id));
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task Soft_deleted_clone_and_direct_still_in_a_snapshot_are_protected()
    {
        var seeded = await AddWebsiteAsync(Cs);
        await using var request = new Request(Cs, seeded.ShopId);
        var library = await AddLibraryAsync(seeded.ShopId, Days(30));
        var clone = await AddCloneAsync(seeded.ShopId, library, Days(30));
        var direct = await AddDirectAsync(seeded.ShopId, Days(30));
        await SaveTreeAsync(request, seeded.Home.Id, TreeWith(
            Hero("c_hero1", clone.Id.ToString("D")), Gallery("c_gall1", direct.Id.ToString("D"))));
        await request.Publish();
        await SaveTreeAsync(request, seeded.Home.Id, TreeWith());
        await using (var db = NewDb(Cs, seeded.ShopId))
        {
            foreach (var id in new[] { clone.Id, direct.Id })
            {
                (await db.MediaAssets.SingleAsync(m => m.Id == id)).SoftDeleteFromLibrary();
            }

            await db.SaveChangesAsync();
        }

        var storage = new RecordingStorage();

        await RunAsync(seeded.ShopId, Now, storage);
        await RunAsync(seeded.ShopId, Now + Days(8), storage);

        AssertUntouched(await StateAsync(clone.Id));
        AssertUntouched(await StateAsync(direct.Id));
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task Other_shop_is_unchanged_116()
    {
        var a = await AddWebsiteAsync(Cs);
        var b = await AddWebsiteAsync(Cs);
        var cloneA = await AddDirectAsync(a.ShopId, Days(30));
        var cloneB = await AddDirectAsync(b.ShopId, Days(30));
        var storage = new RecordingStorage();

        await RunAsync(a.ShopId, Now, storage);
        await RunAsync(a.ShopId, Now + Days(8), storage);

        Assert.False((await StateAsync(cloneA.Id)).Exists);
        AssertUntouched(await StateAsync(cloneB.Id));
        Assert.Equal(cloneA.StorageKey, Assert.Single(storage.Deleted));
    }

    // ---------- fakes ----------

    private sealed class RecordingStorage : IObjectStorage
    {
        public List<string> Deleted { get; } = [];

        public HashSet<string> ThrowOn { get; } = [];

        public Task DeleteAsync(string key, CancellationToken ct)
        {
            if (ThrowOn.Contains(key))
            {
                throw new IOException($"Simulated storage failure for {key}.");
            }

            Deleted.Add(key);
            return Task.CompletedTask;
        }

        public Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken ct) => throw new NotSupportedException();

        public Task<bool> ExistsAsync(string key, CancellationToken ct) => throw new NotSupportedException();

        public Task<StoredObject?> OpenReadAsync(string key, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class ThrowingSource : IMediaUsageSource
    {
        public Task<IReadOnlySet<Guid>> CollectUsedAssetIdsAsync(CancellationToken ct) =>
            throw new InvalidOperationException("Simulated unreadable snapshot (R5).");
    }

    /// <summary>Trả <paramref name="assetId"/> từ lần gọi thứ <paramref name="fromCall"/> trở đi (trước đó: rỗng).</summary>
    private sealed class UsedFromCall(int fromCall, Guid assetId) : IMediaUsageSource
    {
        public int Calls { get; private set; }

        public Task<IReadOnlySet<Guid>> CollectUsedAssetIdsAsync(CancellationToken ct)
        {
            Calls++;
            IReadOnlySet<Guid> result = Calls >= fromCall ? new HashSet<Guid> { assetId } : new HashSet<Guid>();
            return Task.FromResult(result);
        }
    }

    /// <summary>Ném từ lần gọi thứ <paramref name="fromCall"/> (lần thu trước pha xoá).</summary>
    private sealed class ThrowingFromCall(int fromCall) : IMediaUsageSource
    {
        private int _calls;

        public Task<IReadOnlySet<Guid>> CollectUsedAssetIdsAsync(CancellationToken ct) =>
            ++_calls >= fromCall
                ? throw new InvalidOperationException("Simulated unreadable source on second collection (R5).")
                : Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
