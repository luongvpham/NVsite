using System.Text.RegularExpressions;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// REFACTOR-AUTHZ-001 — `IgnoreQueryFilters()` tắt CẢ filter tenant LẪN filter soft-delete của
/// toàn câu query. Trước task này 7 handler Identity gọi nó trên `UserShops` và quên
/// `!IsDeleted`/`Status == Active`: membership đã xoá/đình chỉ vẫn đăng nhập, đổi mật khẩu được.
///
/// Test quét mã nguồn `backend/src`: mọi lời gọi `IgnoreQueryFilters(` chỉ được nằm trong file có
/// mặt ở <see cref="Allowlist"/>, kèm lý do. Cần đọc `UserShop` xuyên shop thì dùng
/// `Vsite.Application.Identity.UserShopQueries`, KHÔNG thêm file vào đây. Thêm một dòng mới vào
/// allowlist là quyết định bảo mật — phải ghi rõ vì sao filter tenant không áp dụng và điều kiện
/// nào được viết tay thay thế.
/// </summary>
public sealed class IgnoreQueryFiltersAllowlistTests
{
    /// <summary>File → (số lời gọi cho phép, lý do). Khoá cả SỐ LƯỢNG: file đã được phép vẫn không thể
    /// lặng lẽ thêm một lời gọi thứ hai trên entity khác (vd. `UserShops`).</summary>
    private static readonly Dictionary<string, (int Calls, string Reason)> Allowlist = new()
    {
        ["Vsite.Application/Identity/UserShopQueries.cs"] =
            (2, "Điểm duy nhất đọc UserShop xuyên shop cho luồng auth; tự thêm !IsDeleted / Status == Active."),
        ["Vsite.Application/Media/Queries/GetAssetsByIds/GetAssetsByIdsHandler.cs"] =
            (1, "Cố ý trả cả asset đã xoá mềm (#72); ShopId == route viết tay trong cùng query."),
        ["Vsite.Application/Media/Queries/GetDerivatives/GetDerivativesHandler.cs"] =
            (1, "Bản Library nguồn có thể đã xoá mềm (A11); ShopId + !IsDeleted viết tay trên dòng phái sinh."),
        ["Vsite.Infrastructure/Media/ShopLogoReader.cs"] =
            (2, "Cùng lý do GetDerivativesHandler (bản đơn + bản theo lô); ShopId + !IsDeleted + Kind viết tay."),
        ["Vsite.Application/Shop/Commands/CreateShop/CreateShopHandler.cs"] =
            (1, "Kiểm trùng slug kể cả shop đã xoá mềm (unique index không lọc soft delete); Shop không tenant-scoped."),
        ["Vsite.Application/Shop/Commands/UpdateShop/UpdateShopHandler.cs"] =
            (1, "Như CreateShopHandler."),
    };

    // Bắt cả `.IgnoreQueryFilters (` xuống dòng lẫn lời gọi trần (`using static`) — so khớp trên toàn
    // nội dung file SAU khi bỏ comment, không theo từng dòng.
    private static readonly Regex Call = new(@"\bIgnoreQueryFilters\s*\(", RegexOptions.Compiled);
    private static readonly Regex Comments = new(@"//[^\n]*|/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

    [Fact]
    public void IgnoreQueryFilters_is_only_called_from_allowlisted_files_with_the_allowed_count()
    {
        var src = Path.Combine(FindRepoRoot(), "backend", "src");
        var actual = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Select(f => (Rel: Path.GetRelativePath(src, f).Replace(Path.DirectorySeparatorChar, '/'), Full: f))
            .Where(f => !IsGeneratedOrBuildOutput(f.Rel))
            .Select(f => (f.Rel, Calls: Call.Matches(Comments.Replace(File.ReadAllText(f.Full), string.Empty)).Count))
            .Where(f => f.Calls > 0)
            .ToDictionary(f => f.Rel, f => f.Calls);

        var violations = actual
            .Where(kv => !Allowlist.TryGetValue(kv.Key, out var allowed) || kv.Value > allowed.Calls)
            .Select(kv => $"{kv.Key} ({kv.Value} lời gọi)")
            .Order()
            .ToList();
        Assert.True(
            violations.Count == 0,
            "IgnoreQueryFilters() ngoài allowlist hoặc vượt số lời gọi cho phép — đọc UserShop xuyên shop thì dùng " +
            "UserShopQueries; trường hợp khác phải sửa Allowlist kèm lý do (quyết định bảo mật): " + string.Join(", ", violations));

        var stale = Allowlist
            .Where(kv => actual.GetValueOrDefault(kv.Key) < kv.Value.Calls)
            .Select(kv => $"{kv.Key} (cho phép {kv.Value.Calls}, thực tế {actual.GetValueOrDefault(kv.Key)})")
            .Order()
            .ToList();
        Assert.True(stale.Count == 0, "Allowlist cao hơn thực tế — hạ số hoặc xoá dòng: " + string.Join(", ", stale));
    }

    private static bool IsGeneratedOrBuildOutput(string relativePath)
    {
        var segments = relativePath.Split('/');
        return segments.Contains("obj") || segments.Contains("bin") || segments.Contains("Migrations");
    }

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
