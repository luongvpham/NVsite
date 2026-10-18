using System.Text.Json.Nodes;

namespace Vsite.IntegrationTests.Website;

/// <summary>
/// T4, WEBSITE-001 (R4, R5, #67) — IRichTextSanitizer chạy bộ vector dùng chung với FE (xss-richtext.json). Phân loại
/// reject/clean dựa trên SỰ KIỆN của thư viện, không so chuỗi. Không cần Docker.
/// </summary>
public sealed class RichTextSanitizerTests
{
    public static IEnumerable<object[]> Vectors()
    {
        var file = TreeFixtures.Load("xss-richtext.json");
        foreach (var vector in file["vectors"]!.AsArray())
        {
            yield return [vector!["id"]!.GetValue<string>()];
        }
    }

    private static JsonObject Vector(string id) =>
        TreeFixtures.Load("xss-richtext.json")["vectors"]!.AsArray().Single(v => v!["id"]!.GetValue<string>() == id)!.AsObject();

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Vector_is_classified_by_sanitizer_events(string id)
    {
        var profile = TreeFixtures.Load("xss-richtext.json")["profile"]!.GetValue<string>();
        var vector = Vector(id);
        var input = vector["input"]!.GetValue<string>();
        var expect = vector["expect"]!.GetValue<string>();
        var sanitizer = TreeFixtures.CreateSanitizer();

        var result = sanitizer.Sanitize(input, profile);

        switch (expect)
        {
            case "reject":
                Assert.True(result.Unsafe, $"{id}: phải bị coi là nguy hiểm. clean={result.Clean}");
                break;
            case "clean":
                Assert.False(result.Unsafe, $"{id}: không được báo nguy hiểm. clean={result.Clean}");
                Assert.NotEqual(input, result.Clean);
                if (vector["clean"] is { } exact)
                {
                    Assert.Equal(exact.GetValue<string>(), result.Clean);
                }

                break;
            case "pass":
                Assert.False(result.Unsafe, $"{id}: không được báo nguy hiểm. clean={result.Clean}");
                Assert.Equal(input, result.Clean);
                break;
            default:
                throw new InvalidOperationException($"expect lạ: {expect}");
        }

        foreach (var forbidden in vector["mustNotContain"]?.AsArray().Select(n => n!.GetValue<string>()) ?? [])
        {
            Assert.DoesNotContain(forbidden, result.Clean, StringComparison.OrdinalIgnoreCase);
        }

        // Idempotent (R5): sanitize(sanitize(x)) == sanitize(x), và lần hai không còn gì để cắt.
        var again = sanitizer.Sanitize(result.Clean, profile);
        Assert.Equal(result.Clean, again.Clean);
        Assert.False(again.Unsafe);
    }

    [Fact]
    public void Every_vector_output_never_contains_executable_markup()
    {
        var sanitizer = TreeFixtures.CreateSanitizer();
        foreach (var vector in TreeFixtures.Load("xss-richtext.json")["vectors"]!.AsArray())
        {
            var clean = sanitizer.Sanitize(vector!["input"]!.GetValue<string>(), "basic").Clean;
            Assert.DoesNotContain("<script", clean, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("onerror", clean, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("onclick", clean, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("javascript:", clean, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("vbscript:", clean, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<iframe", clean, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Profile_decides_the_allowed_tags()
    {
        var sanitizer = TreeFixtures.CreateSanitizer();

        Assert.Equal("<p>a</p>", sanitizer.Sanitize("<p>a</p>", "basic").Clean);
        var inline = sanitizer.Sanitize("<p>a</p><strong>b</strong>", "inline");
        Assert.Equal("a<strong>b</strong>", inline.Clean);
        Assert.False(inline.Unsafe);
    }

    [Fact]
    public void Unknown_profile_throws_with_the_profile_name()
    {
        var ex = Assert.Throws<ArgumentException>(() => TreeFixtures.CreateSanitizer().Sanitize("<p>a</p>", "nope"));
        Assert.Contains("nope", ex.Message);
        // `dangerous` nằm chung file nhưng không phải profile.
        Assert.Throws<ArgumentException>(() => TreeFixtures.CreateSanitizer().Sanitize("<p>a</p>", "dangerous"));
    }

    [Fact]
    public void Sanitizer_does_not_share_state_between_calls()
    {
        var sanitizer = TreeFixtures.CreateSanitizer();

        Assert.True(sanitizer.Sanitize("<script>1</script>", "basic").Unsafe);
        Assert.False(sanitizer.Sanitize("<p>ok</p>", "basic").Unsafe);
    }

    [Fact]
    public void Only_http_https_mailto_tel_schemes_survive_and_blank_target_gets_rel()
    {
        var sanitizer = TreeFixtures.CreateSanitizer();

        Assert.Equal("<a href=\"mailto:a@b.vn\">m</a>", sanitizer.Sanitize("<a href=\"mailto:a@b.vn\">m</a>", "basic").Clean);
        Assert.Equal("<a href=\"tel:+84123\">t</a>", sanitizer.Sanitize("<a href=\"tel:+84123\">t</a>", "basic").Clean);
        Assert.Equal("<a href=\"http://x.vn\" target=\"_blank\" rel=\"noopener noreferrer\">h</a>",
            sanitizer.Sanitize("<a href=\"http://x.vn\" target=\"_blank\" rel=\"opener\">h</a>", "basic").Clean);
        // data: là scheme nguy hiểm → reject.
        Assert.True(sanitizer.Sanitize("<a href=\"data:text/plain,hi\">x</a>", "basic").Unsafe);
    }
}
