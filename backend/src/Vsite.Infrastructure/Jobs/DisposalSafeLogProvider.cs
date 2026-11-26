using Hangfire.Logging;

namespace Vsite.Infrastructure.Jobs;

/// <summary>
/// WEBSITE-008 — bọc log provider của Hangfire để chịu được <c>LoggerFactory</c> đã dispose.
/// Hangfire giữ log provider ở biến STATIC toàn process và <c>AddHangfire</c> đặt lại nó mỗi khi một host cấu hình Hangfire. Trong process
/// có nhiều host (test: nhiều <c>WebApplicationFactory</c>, có host khởi động lỗi bị dispose ngay), static có thể trỏ vào factory của host
/// đã chết ⇒ <c>GetLogger</c> ném <c>ObjectDisposedException</c>; nếu đó là lúc Hangfire khởi tạo static (<c>GlobalJobFilters</c>) thì kiểu đó
/// hỏng VĨNH VIỄN cho cả process. Production chỉ có một host nên hành vi không đổi: log vẫn đi vào logging của host.
/// </summary>
internal sealed class DisposalSafeLogProvider(ILogProvider inner) : ILogProvider
{
    public ILog GetLogger(string name)
    {
        try
        {
            return inner.GetLogger(name);
        }
        catch (ObjectDisposedException)
        {
            return NullLog.Instance;
        }
    }

    private sealed class NullLog : ILog
    {
        public static readonly NullLog Instance = new();

        public bool Log(LogLevel logLevel, Func<string>? messageFunc, Exception? exception = null) => false;
    }
}
