using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Vsite.IntegrationTests.Jobs;

/// <summary>
/// Hangfire giữ log provider ở biến STATIC toàn process (<c>LogProvider</c>), đặt lại mỗi khi một host cấu hình Hangfire. Trong process
/// test có nhiều host; host khởi động lỗi (vd. <c>Invalid_job_options_fail_host_startup</c>) bị dispose ngay nên static trỏ vào
/// <c>LoggerFactory</c> đã dispose ⇒ lần đầu Hangfire khởi tạo static (<c>GlobalJobFilters</c>) ném <c>ObjectDisposedException</c> và hỏng
/// VĨNH VIỄN cho cả process (từng làm đỏ chập chờn <c>JobsDashboardTests</c>/<c>ShopFanOutWiringTests</c>, WEBSITE-008).
/// </summary>
[Collection(IdentityApiCollection.Name)]
public sealed class HangfireLogProviderTests(IdentityApiFactory factory)
{
    [Fact]
    public void Static_log_provider_survives_a_disposed_host()
    {
        _ = factory.Server;
        var bad = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jobs:MediaCleanup:OrphanGrace"] = "-1.00:00:00" })));
        Assert.NotNull(Record.Exception(() => bad.Server)); // host lỗi khởi động, đã cấu hình Hangfire rồi bị dispose

        var ex = Record.Exception(() => Hangfire.Logging.LogProvider.GetLogger("hangfire-logprovider-test").Log(Hangfire.Logging.LogLevel.Info, () => "x"));

        Assert.Null(ex);
    }
}
