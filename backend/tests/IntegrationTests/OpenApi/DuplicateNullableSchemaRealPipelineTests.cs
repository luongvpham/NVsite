using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vsite.Api.OpenApi;

namespace Vsite.IntegrationTests;

/// <summary>
/// Test đi qua ĐÚNG pipeline sinh document thật của Microsoft.AspNetCore.OpenApi (không hand-build
/// <see cref="Microsoft.OpenApi.Models.OpenApiDocument"/> như các test khác trong file
/// <c>DuplicateNullableSchemaDocumentTransformerTests.cs</c>) — dựng một <c>WebApplication</c> tối
/// giản riêng (KHÔNG dùng <c>Vsite.Api.Program</c>, vốn kéo theo Redis/Postgres qua
/// <c>TenantResolutionMiddleware</c> ngay cả cho request tới <c>/openapi</c> — xác nhận bằng thực
/// nghiệm lúc điều tra bug này, xem task-D2-report.md) chạy trên
/// <see cref="Microsoft.AspNetCore.TestHost.TestServer"/> (in-memory, không cần Docker/socket
/// thật), map một endpoint trả về DTO có shape giống hệt <c>SlotUploadResultDto</c> (một property
/// non-null + một property nullable CÙNG type), rồi gọi thật <c>GET /openapi/{name}.json</c> và
/// parse JSON trả về.
///
/// Lý do cần test này ngoài các test hand-build: các test hand-build giả định đúng thứ tự
/// pipeline thật (schema transformer chạy xong hết TRƯỚC document transformer, document transformer
/// thấy schema còn inline) — một bản vá .NET 9.x sau này đổi thứ tự đó sẽ không được test hand-build
/// phát hiện (chúng tự set up đúng cái thứ tự mà code giả định, nên luôn xanh). Test này chạy code
/// THẬT của framework nên sẽ đỏ nếu giả định đó không còn đúng.
/// </summary>
public sealed class DuplicateNullableSchemaRealPipelineTests
{
    private sealed record RealPipelineAssetDto(string Id, string Name);

    private sealed record RealPipelineSlotDto(RealPipelineAssetDto Asset, RealPipelineAssetDto? LibraryAsset);

    [Fact]
    public async Task Real_OpenApi_pipeline_collapses_duplicate_and_produces_allOf_ref_nullable()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddOpenApi("real-pipeline-test", options => options
                        .AddSchemaTransformer<DuplicateNullableSchemaOccurrenceTagger>()
                        .AddDocumentTransformer<DuplicateNullableSchemaDocumentTransformer>());
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/slot", () => new RealPipelineSlotDto(new RealPipelineAssetDto("id", "name"), null));
                        endpoints.MapOpenApi();
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("/openapi/real-pipeline-test.json");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"GET /openapi/real-pipeline-test.json trả về {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        // Không có schema nào là "bản song sinh hậu tố số" của một schema khác cùng property set —
        // chính hình dạng bug MediaAssetDto2 gốc.
        var byName = schemas.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        foreach (var name in byName.Keys)
        {
            var match = System.Text.RegularExpressions.Regex.Match(name, @"^(?<base>.+?)(?<suffix>[0-9]+)$");
            if (!match.Success || !byName.TryGetValue(match.Groups["base"].Value, out var baseSchema))
            {
                continue;
            }

            var baseProps = PropertyNames(baseSchema);
            var candidateProps = PropertyNames(byName[name]);
            Assert.False(
                baseProps.Count > 0 && baseProps.SetEquals(candidateProps),
                $"'{name}' là bản trùng lặp hậu tố số của '{match.Groups["base"].Value}' — pipeline thật " +
                "vẫn sinh ra đúng bug MediaAssetDto2, transformer không còn hoạt động như mong đợi.");
        }

        var slotSchema = byName["RealPipelineSlotDto"].GetProperty("properties");

        // libraryAsset (nullable) -> allOf: [ $ref RealPipelineAssetDto ] + nullable: true.
        var libraryAsset = slotSchema.GetProperty("libraryAsset");
        Assert.True(libraryAsset.TryGetProperty("nullable", out var nullableFlag) && nullableFlag.GetBoolean());
        Assert.True(libraryAsset.TryGetProperty("allOf", out var allOf));
        Assert.Equal(1, allOf.GetArrayLength());
        Assert.Equal("#/components/schemas/RealPipelineAssetDto", allOf[0].GetProperty("$ref").GetString());
        Assert.False(libraryAsset.TryGetProperty("$ref", out _), "libraryAsset không được là $ref trần (đó là hình dạng bug cũ MediaAssetDto2).");

        // asset (non-null) -> $ref trần tới CÙNG schema, không allOf/nullable.
        var asset = slotSchema.GetProperty("asset");
        Assert.Equal("#/components/schemas/RealPipelineAssetDto", asset.GetProperty("$ref").GetString());
        Assert.False(asset.TryGetProperty("nullable", out _));
        Assert.False(asset.TryGetProperty("allOf", out _));
    }

    private static HashSet<string> PropertyNames(JsonElement schema) =>
        schema.TryGetProperty("properties", out var props)
            ? props.EnumerateObject().Select(p => p.Name).ToHashSet()
            : new HashSet<string>();
}
