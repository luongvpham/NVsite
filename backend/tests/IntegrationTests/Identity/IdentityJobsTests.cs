using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Vsite.Application.Identity.Jobs;
using Vsite.Domain.Identity;
using Vsite.Domain.Identity.Entities;
using Vsite.Domain.Identity.Enums;
using Vsite.Infrastructure.Persistence;

namespace Vsite.IntegrationTests;

/// <summary>
/// JOBS-001 T5 / #116 ngoại lệ (a) — 3 job Identity trên bảng KHÔNG tenant, set-based. Postgres thật.
/// Dọn theo <c>ExpiresAt &lt; now - grace</c>, KHÔNG theo <c>RevokedAt</c>; điều kiện nghiêm ngặt (R7).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class IdentityJobsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 3, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync();
        // Mỗi test bắt đầu từ bảng sạch (chia sẻ DB trong collection).
        await db.RefreshTokens.ExecuteDeleteAsync();
        await db.PasswordResetTokens.ExecuteDeleteAsync();
        await db.PendingRegistrations.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AppDbContext CreateDb() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.ConnectionString).Options,
        new TestTenantContext());

    private static FakeTime Clock() => new(Now);

    [Fact]
    public async Task Expired_token_job_deletes_expired_and_keeps_live_incl_boundary_both_tables()
    {
        var userId = await SeedUserAsync();
        await using var db = CreateDb();
        db.RefreshTokens.AddRange(
            Refresh(userId, Now.AddSeconds(-1)),
            Refresh(userId, Now), // ExpiresAt == now - grace(0) => giữ (điều kiện `<` nghiêm ngặt)
            Refresh(userId, Now.AddDays(1)));
        db.PasswordResetTokens.AddRange(
            Reset(userId, Now.AddSeconds(-1)),
            Reset(userId, Now),
            Reset(userId, Now.AddDays(1)));
        await db.SaveChangesAsync();

        await new ExpiredTokenCleanupJob(db, Clock(), NullLogger<ExpiredTokenCleanupJob>.Instance)
            .RunAsync(TimeSpan.Zero, CancellationToken.None);

        Assert.Equal([Now, Now.AddDays(1)], await ExpiryAsync(db.RefreshTokens.Select(t => t.ExpiresAt)));
        Assert.Equal([Now, Now.AddDays(1)], await ExpiryAsync(db.PasswordResetTokens.Select(t => t.ExpiresAt)));
    }

    [Fact]
    public async Task Revoked_but_unexpired_token_is_kept()
    {
        var userId = await SeedUserAsync();
        await using var db = CreateDb();
        var revoked = Refresh(userId, Now.AddDays(5));
        revoked.RevokedAt = Now.AddDays(-3);
        db.RefreshTokens.Add(revoked);
        var consumed = Reset(userId, Now.AddDays(5));
        consumed.ConsumedAt = Now.AddDays(-3);
        db.PasswordResetTokens.Add(consumed);
        await db.SaveChangesAsync();

        await new ExpiredTokenCleanupJob(db, Clock(), NullLogger<ExpiredTokenCleanupJob>.Instance)
            .RunAsync(TimeSpan.Zero, CancellationToken.None);

        Assert.Equal(1, await db.RefreshTokens.CountAsync());
        Assert.Equal(1, await db.PasswordResetTokens.CountAsync());
    }

    [Fact]
    public async Task Grace_of_one_day_keeps_rows_expired_less_than_a_day_ago()
    {
        var userId = await SeedUserAsync();
        await using var db = CreateDb();
        db.RefreshTokens.AddRange(
            Refresh(userId, Now.AddHours(-23)), // < 1 ngày => giữ
            Refresh(userId, Now.AddDays(-1)),   // == biên => giữ
            Refresh(userId, Now.AddDays(-1).AddSeconds(-1))); // xoá
        db.PendingRegistrations.AddRange(
            Pending(Now.AddHours(-23)),
            Pending(Now.AddDays(-1).AddSeconds(-1)));
        await db.SaveChangesAsync();

        var grace = TimeSpan.FromDays(1);
        await new ExpiredTokenCleanupJob(db, Clock(), NullLogger<ExpiredTokenCleanupJob>.Instance)
            .RunAsync(grace, CancellationToken.None);
        await new PendingRegistrationCleanupJob(db, Clock(), NullLogger<PendingRegistrationCleanupJob>.Instance)
            .RunAsync(grace, CancellationToken.None);

        Assert.Equal([Now.AddDays(-1), Now.AddHours(-23)], await ExpiryAsync(db.RefreshTokens.Select(t => t.ExpiresAt)));
        Assert.Equal([Now.AddHours(-23)], await ExpiryAsync(db.PendingRegistrations.Select(t => t.ExpiresAt)));
    }

    [Fact]
    public async Task Pending_registration_job_deletes_expired_keeps_live()
    {
        await using var db = CreateDb();
        db.PendingRegistrations.AddRange(Pending(Now.AddMinutes(-1)), Pending(Now), Pending(Now.AddHours(1)));
        await db.SaveChangesAsync();

        await new PendingRegistrationCleanupJob(db, Clock(), NullLogger<PendingRegistrationCleanupJob>.Instance)
            .RunAsync(TimeSpan.Zero, CancellationToken.None);

        Assert.Equal([Now, Now.AddHours(1)], await ExpiryAsync(db.PendingRegistrations.Select(t => t.ExpiresAt)));
    }

    [Fact]
    public async Task Batch_larger_than_5000_is_fully_deleted_in_batches()
    {
        await using var db = CreateDb();
        // Insert set-based: 12.000 dòng hết hạn (> 2 lô) + 1 dòng còn hạn.
        var expired = Now.AddHours(-1);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO pending_registration (id, email, email_normalized, password_hash, token_hash, expires_at, created_at)
            SELECT gen_random_uuid(), 'e' || g, 'e' || g, 'x', 'th' || g, {expired}, {Now}
            FROM generate_series(1, 12000) g
            """);
        db.PendingRegistrations.Add(Pending(Now.AddHours(1)));
        await db.SaveChangesAsync();

        var log = new ListLogger<PendingRegistrationCleanupJob>();
        await new PendingRegistrationCleanupJob(db, Clock(), log)
            .RunAsync(TimeSpan.Zero, CancellationToken.None);

        Assert.Equal(1, await db.PendingRegistrations.CountAsync());
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("12000"));
    }

    [Fact]
    public async Task Zalo_user_without_external_login_logs_warning_and_changes_nothing()
    {
        await using var db = CreateDb();
        var orphan = NewUser(PrimaryIdentityKind.Zalo);
        db.Users.Add(orphan);
        await db.SaveChangesAsync();

        var log = new ListLogger<ZaloIdentityIntegrityJob>();
        await new ZaloIdentityIntegrityJob(db, log).RunAsync(CancellationToken.None);

        var warning = Assert.Single(log.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(orphan.Id.ToString(), warning.Message);
        Assert.True(await db.Users.AnyAsync(u => u.Id == orphan.Id)); // không sửa dữ liệu
    }

    [Fact]
    public async Task Zalo_user_with_external_login_does_not_warn()
    {
        await using var db = CreateDb();
        // Dọn orphan do ca khác để kiểm "không còn" xác định.
        await db.Users.Where(u => u.PrimaryIdentityKind == PrimaryIdentityKind.Zalo
            && !db.ExternalLogins.Any(e => e.UserId == u.Id)).ExecuteDeleteAsync();
        var ok = NewUser(PrimaryIdentityKind.Zalo);
        db.Users.Add(ok);
        db.ExternalLogins.Add(new ExternalLogin
        {
            UserId = ok.Id,
            Provider = ExternalLoginProviders.Zalo,
            ProviderUserId = $"z-{ok.Id:N}",
        });
        await db.SaveChangesAsync();

        var log = new ListLogger<ZaloIdentityIntegrityJob>();
        await new ZaloIdentityIntegrityJob(db, log).RunAsync(CancellationToken.None);

        Assert.DoesNotContain(log.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void Ho_Chi_Minh_time_zone_resolves() // R6
    {
        // InvariantGlobalization=true: Windows không resolve IANA, helper có fallback Windows id (cùng UTC+7).
        var tz = Vsite.Infrastructure.Jobs.RecurringJobs.ResolveTimeZone("Asia/Ho_Chi_Minh");
        Assert.Contains(tz.Id, new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" });
        Assert.Equal(TimeSpan.FromHours(7), tz.BaseUtcOffset);
    }

    private static async Task<List<DateTimeOffset>> ExpiryAsync(IQueryable<DateTimeOffset> q) =>
        (await q.ToListAsync()).Order().ToList();

    private async Task<Guid> SeedUserAsync()
    {
        await using var db = CreateDb();
        var user = NewUser(PrimaryIdentityKind.Email);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static User NewUser(PrimaryIdentityKind kind)
    {
        var id = Guid.NewGuid();
        var email = kind == PrimaryIdentityKind.Email;
        return new User(id)
        {
            PrimaryIdentityKind = kind,
            RoleId = WellKnownRoles.PlatformUserId,
            Email = email ? $"{id:N}@t.local" : null,
            EmailNormalized = email ? $"{id:N}@T.LOCAL" : null,
            EmailVerifiedAt = email ? Now : null,
        };
    }

    private static RefreshToken Refresh(Guid userId, DateTimeOffset expires) => new()
    {
        UserId = userId, Audience = "vsite-main", TokenHash = Guid.NewGuid().ToString("N"), ExpiresAt = expires,
    };

    private static PasswordResetToken Reset(Guid userId, DateTimeOffset expires) => new()
    {
        UserId = userId, Audience = "vsite-main", TokenHash = Guid.NewGuid().ToString("N"), ExpiresAt = expires,
    };

    private static PendingRegistration Pending(DateTimeOffset expires)
    {
        var n = Guid.NewGuid().ToString("N");
        return new PendingRegistration
        {
            Email = $"{n}@t.local", EmailNormalized = $"{n}@T.LOCAL", PasswordHash = "x", TokenHash = n, ExpiresAt = expires,
        };
    }

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
