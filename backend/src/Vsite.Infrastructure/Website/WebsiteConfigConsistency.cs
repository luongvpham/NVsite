using Vsite.Application.Website.Interfaces;

namespace Vsite.Infrastructure.Website;

/// <summary>Kiểm nhất quán giữa các artifact Website lúc khởi động (fail-fast, #97).</summary>
public static class WebsiteConfigConsistency
{
    /// <summary>
    /// Mọi type trong tree-rules.json phải có JSON Schema props. Thiếu thì validator âm thầm bỏ qua kiểm shape của type
    /// đó (chỉ còn bản đồ prop) — nên app không được lên.
    /// </summary>
    public static void EnsureEveryTypeHasPropsSchema(ITreeRulesCatalog rules, IWebsiteSchemaProvider schemas)
    {
        var missing = rules.Types.Keys.Where(type => !schemas.HasPropsSchema(type)).Order(StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"tree-rules.json có type không có schema trong props-schemas.json: [{string.Join(", ", missing)}]. Chạy lại `pnpm gen:registry`.");
        }
    }
}
