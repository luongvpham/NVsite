using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Vsite.Api.OpenApi;

namespace Vsite.IntegrationTests;

/// <summary>
/// Unit test thuần cho <see cref="DuplicateNullableSchemaOccurrenceTagger"/> +
/// <see cref="DuplicateNullableSchemaDocumentTransformer"/> — dựng <see cref="OpenApiSchema"/>/
/// <see cref="OpenApiDocument"/> nhỏ trực tiếp trong code cùng <c>JsonTypeInfo</c> thật (qua
/// <c>JsonSerializerOptions.Default.GetTypeInfo</c>), không cần Docker/Postgres, không cần chạy
/// WebApplicationFactory/toàn bộ pipeline ASP.NET Core:
/// <c>dotnet test backend/tests/IntegrationTests --filter "FullyQualifiedName~DuplicateNullableSchemaDocumentTransformerTests"</c>.
/// </summary>
public sealed class DuplicateNullableSchemaDocumentTransformerTests
{
    private sealed record MediaAssetDto(string Id, string StorageKey);

    private sealed record FooDto(string A);

    private sealed record FooDto2(string A, int B);

    private static OpenApiSchema MediaAssetSchema(bool nullable) => new()
    {
        Type = "object",
        Nullable = nullable,
        Required = new HashSet<string> { "id", "storageKey" },
        Properties = new Dictionary<string, OpenApiSchema>
        {
            ["id"] = new OpenApiSchema { Type = "string" },
            ["storageKey"] = new OpenApiSchema { Type = "string" },
        },
    };

    private static async Task RunTaggerAsync(
        OpenApiSchema schema,
        Type clrType,
        string documentName = "test",
        Action<OpenApiOptions>? configureOptions = null)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<OpenApiOptions>(documentName, configureOptions ?? (_ => { }));
        var tagger = new DuplicateNullableSchemaOccurrenceTagger();
        var context = new OpenApiSchemaTransformerContext
        {
            DocumentName = documentName,
            JsonTypeInfo = JsonSerializerOptions.Default.GetTypeInfo(clrType),
            JsonPropertyInfo = null,
            ParameterDescription = null,
            ApplicationServices = services.BuildServiceProvider(),
        };
        await tagger.TransformAsync(schema, context, CancellationToken.None);
    }

    [Fact]
    public async Task Tagger_turns_off_Nullable_and_tags_object_schema_with_properties()
    {
        var schema = MediaAssetSchema(nullable: true);

        await RunTaggerAsync(schema, typeof(MediaAssetDto));

        Assert.False(schema.Nullable);
        Assert.NotNull(schema.Annotations);
        Assert.Equal("MediaAssetDto", schema.Annotations[DuplicateNullableSchemaOccurrenceTagger.AnnotationKey]);
    }

    [Fact]
    public async Task Tagger_ignores_non_nullable_schema()
    {
        var schema = MediaAssetSchema(nullable: false);

        await RunTaggerAsync(schema, typeof(MediaAssetDto));

        Assert.False(schema.Nullable);
        Assert.Null(schema.Annotations);
    }

    [Fact]
    public async Task Tagger_ignores_non_object_and_empty_property_schemas()
    {
        var stringSchema = new OpenApiSchema { Type = "string", Nullable = true };
        await RunTaggerAsync(stringSchema, typeof(string));
        Assert.Null(stringSchema.Annotations);
        Assert.True(stringSchema.Nullable); // không phải đối tượng bug này — giữ nguyên.

        var emptyObject = new OpenApiSchema { Type = "object", Nullable = true, Properties = new Dictionary<string, OpenApiSchema>() };
        await RunTaggerAsync(emptyObject, typeof(MediaAssetDto));
        Assert.Null(emptyObject.Annotations);
        Assert.True(emptyObject.Nullable);
    }

    [Fact]
    public async Task Collapses_duplicate_nullable_schema_and_rewrites_ref_as_nullable_allOf()
    {
        // Mô phỏng đúng repro MEDIA-001: MediaAssetDto (non-null, SlotUploadResultDto.Asset) +
        // MediaAssetDto? (SlotUploadResultDto.LibraryAsset) — cả hai đi qua tagger TRƯỚC, đúng thứ
        // tự thật của pipeline (schema transformer chạy trước document transformer).
        var assetSchema = MediaAssetSchema(nullable: false);
        await RunTaggerAsync(assetSchema, typeof(MediaAssetDto));

        var libraryAssetSchema = MediaAssetSchema(nullable: true);
        await RunTaggerAsync(libraryAssetSchema, typeof(MediaAssetDto));

        var slotUploadResultDto = new OpenApiSchema
        {
            Type = "object",
            Required = new HashSet<string> { "asset", "libraryAsset" },
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["asset"] = assetSchema,
                ["libraryAsset"] = libraryAssetSchema,
            },
        };

        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                ["/slot"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<OperationType, OpenApiOperation>
                    {
                        [OperationType.Get] = new OpenApiOperation
                        {
                            Responses = new OpenApiResponses
                            {
                                ["200"] = new OpenApiResponse
                                {
                                    Content = new Dictionary<string, OpenApiMediaType>
                                    {
                                        ["application/json"] = new OpenApiMediaType { Schema = slotUploadResultDto },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };

        DuplicateNullableSchemaDocumentTransformer.Apply(document);

        var libraryAsset = slotUploadResultDto.Properties["libraryAsset"];
        Assert.Null(libraryAsset.Type);
        Assert.True(libraryAsset.Nullable);
        Assert.Single(libraryAsset.AllOf);
        Assert.Equal("MediaAssetDto", libraryAsset.AllOf[0].Reference.Id);

        // asset (non-null, chưa từng bị tag) không bị đổi hình dạng.
        var asset = slotUploadResultDto.Properties["asset"];
        Assert.Equal("object", asset.Type);
        Assert.False(asset.Nullable);
        Assert.Same(assetSchema, asset);
    }

    [Fact]
    public async Task Rewrites_refs_in_array_items_and_leaves_untagged_schema_elsewhere_untouched()
    {
        var nonNull = MediaAssetSchema(nullable: false);
        await RunTaggerAsync(nonNull, typeof(MediaAssetDto));

        var nullableItem = MediaAssetSchema(nullable: true);
        await RunTaggerAsync(nullableItem, typeof(MediaAssetDto));

        var listContainer = new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["items"] = new OpenApiSchema { Type = "array", Items = nullableItem },
            },
        };

        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                ["/foo"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<OperationType, OpenApiOperation>
                    {
                        [OperationType.Get] = new OpenApiOperation
                        {
                            Responses = new OpenApiResponses
                            {
                                ["200"] = new OpenApiResponse
                                {
                                    Content = new Dictionary<string, OpenApiMediaType>
                                    {
                                        ["application/json"] = new OpenApiMediaType { Schema = listContainer },
                                    },
                                },
                            },
                        },
                        [OperationType.Post] = new OpenApiOperation
                        {
                            Responses = new OpenApiResponses
                            {
                                ["200"] = new OpenApiResponse
                                {
                                    Content = new Dictionary<string, OpenApiMediaType>
                                    {
                                        ["application/json"] = new OpenApiMediaType { Schema = nonNull },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };

        DuplicateNullableSchemaDocumentTransformer.Apply(document);

        var itemsSchema = listContainer.Properties["items"].Items;
        Assert.Null(itemsSchema.Type);
        Assert.True(itemsSchema.Nullable);
        Assert.Equal("MediaAssetDto", itemsSchema.AllOf[0].Reference.Id);

        // Bản non-null (chưa từng tag) vẫn nguyên vẹn ở response khác.
        var nonNullResponse = document.Paths["/foo"].Operations[OperationType.Post].Responses["200"]
            .Content["application/json"].Schema;
        Assert.Same(nonNull, nonNullResponse);
        Assert.False(nonNullResponse.Nullable);
    }

    [Fact]
    public async Task Solo_nullable_schema_with_no_non_nullable_sibling_still_collapses_cleanly()
    {
        // Chỉ MỘT occurrence, luôn nullable — không có bản non-null song song. Tagger vẫn tắt
        // Nullable + gắn tag (không cần biết trước có sibling hay không — xem javadoc trong file
        // transformer về lý do không theo dõi "đã thấy đủ hai dạng"); document transformer vẫn bọc
        // đúng vị trí dùng, không sinh schema mồ côi, không đổi refId sang tên khác.
        var onlyNullable = MediaAssetSchema(nullable: true);
        await RunTaggerAsync(onlyNullable, typeof(MediaAssetDto));

        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                ["/solo"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<OperationType, OpenApiOperation>
                    {
                        [OperationType.Get] = new OpenApiOperation
                        {
                            Responses = new OpenApiResponses
                            {
                                ["200"] = new OpenApiResponse
                                {
                                    Content = new Dictionary<string, OpenApiMediaType>
                                    {
                                        ["application/json"] = new OpenApiMediaType { Schema = onlyNullable },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };

        DuplicateNullableSchemaDocumentTransformer.Apply(document);

        var resultSchema = document.Paths["/solo"].Operations[OperationType.Get].Responses["200"]
            .Content["application/json"].Schema;
        Assert.Null(resultSchema.Type);
        Assert.True(resultSchema.Nullable);
        Assert.Equal("MediaAssetDto", resultSchema.AllOf[0].Reference.Id);
    }

    [Fact]
    public async Task Never_merges_two_distinct_CLR_types_that_share_a_name_prefix()
    {
        // FooDto và FooDto2 là hai CLR type KHÁC NHAU, cấu trúc khác nhau, cả hai nullable ở đâu
        // đó — vì việc gắn tag dựa trên JsonTypeInfo (từng type tính refId riêng: "FooDto" vs
        // "FooDto2"), không bao giờ nhầm lẫn dù tên schema trùng tiền tố.
        var foo = new OpenApiSchema
        {
            Type = "object",
            Nullable = true,
            Properties = new Dictionary<string, OpenApiSchema> { ["a"] = new OpenApiSchema { Type = "string" } },
        };
        await RunTaggerAsync(foo, typeof(FooDto));

        var foo2 = new OpenApiSchema
        {
            Type = "object",
            Nullable = true,
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["a"] = new OpenApiSchema { Type = "string" },
                ["b"] = new OpenApiSchema { Type = "integer" },
            },
        };
        await RunTaggerAsync(foo2, typeof(FooDto2));

        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                ["/foo"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<OperationType, OpenApiOperation>
                    {
                        [OperationType.Get] = new OpenApiOperation
                        {
                            Responses = new OpenApiResponses
                            {
                                ["200"] = new OpenApiResponse
                                {
                                    Content = new Dictionary<string, OpenApiMediaType>
                                    {
                                        ["application/json"] = new OpenApiMediaType { Schema = foo },
                                    },
                                },
                            },
                        },
                        [OperationType.Post] = new OpenApiOperation
                        {
                            Responses = new OpenApiResponses
                            {
                                ["200"] = new OpenApiResponse
                                {
                                    Content = new Dictionary<string, OpenApiMediaType>
                                    {
                                        ["application/json"] = new OpenApiMediaType { Schema = foo2 },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };

        DuplicateNullableSchemaDocumentTransformer.Apply(document);

        var fooResult = document.Paths["/foo"].Operations[OperationType.Get].Responses["200"].Content["application/json"].Schema;
        var foo2Result = document.Paths["/foo"].Operations[OperationType.Post].Responses["200"].Content["application/json"].Schema;

        Assert.Equal("FooDto", fooResult.AllOf[0].Reference.Id);
        Assert.Equal("FooDto2", foo2Result.AllOf[0].Reference.Id);
    }

    [Fact]
    public async Task TransformAsync_document_delegates_to_Apply()
    {
        var nullable = MediaAssetSchema(nullable: true);
        await RunTaggerAsync(nullable, typeof(MediaAssetDto));

        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                ["/x"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<OperationType, OpenApiOperation>
                    {
                        [OperationType.Get] = new OpenApiOperation
                        {
                            Responses = new OpenApiResponses
                            {
                                ["200"] = new OpenApiResponse
                                {
                                    Content = new Dictionary<string, OpenApiMediaType>
                                    {
                                        ["application/json"] = new OpenApiMediaType { Schema = nullable },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };

        var transformer = new DuplicateNullableSchemaDocumentTransformer();
        var context = new OpenApiDocumentTransformerContext
        {
            DocumentName = "test",
            DescriptionGroups = Array.Empty<Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescriptionGroup>(),
            ApplicationServices = new ServiceCollection().BuildServiceProvider(),
        };

        await transformer.TransformAsync(document, context, CancellationToken.None);

        var resultSchema = document.Paths["/x"].Operations[OperationType.Get].Responses["200"].Content["application/json"].Schema;
        Assert.True(resultSchema.Nullable);
        Assert.Equal("MediaAssetDto", resultSchema.AllOf[0].Reference.Id);
    }

    [Fact]
    public async Task Tagger_resolves_document_configured_CreateSchemaReferenceId_instead_of_the_static_default()
    {
        const string documentName = nameof(Tagger_resolves_document_configured_CreateSchemaReferenceId_instead_of_the_static_default);

        var schema = MediaAssetSchema(nullable: true);
        await RunTaggerAsync(
            schema,
            typeof(MediaAssetDto),
            documentName,
            options => options.CreateSchemaReferenceId = _ => "CustomConfiguredId");

        Assert.Equal("CustomConfiguredId", schema.Annotations[DuplicateNullableSchemaOccurrenceTagger.AnnotationKey]);
    }

    [Fact]
    public async Task Tagger_throws_when_two_distinct_CLR_types_resolve_to_the_same_reference_id()
    {
        // Mô phỏng đúng rủi ro reviewer nêu: một options.CreateSchemaReferenceId tuỳ biến (hoặc
        // default bị trùng do hai type khác namespace cùng simple name) khiến hai CLR type KHÁC
        // NHAU tính ra CÙNG một reference id — tagger phải NÉM exception ngay lúc generate thay vì
        // âm thầm trỏ $ref của type này sang schema của type kia.
        const string documentName = nameof(Tagger_throws_when_two_distinct_CLR_types_resolve_to_the_same_reference_id);
        void ForceCollision(OpenApiOptions options) => options.CreateSchemaReferenceId = _ => "CollidingId";

        var first = MediaAssetSchema(nullable: true);
        await RunTaggerAsync(first, typeof(FooDto), documentName, ForceCollision);
        Assert.Equal("CollidingId", first.Annotations[DuplicateNullableSchemaOccurrenceTagger.AnnotationKey]);

        var second = MediaAssetSchema(nullable: true);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunTaggerAsync(second, typeof(FooDto2), documentName, ForceCollision));

        Assert.Contains("CollidingId", ex.Message);
        Assert.Contains(documentName, ex.Message);
        // Schema thứ hai không bị nửa-vời sửa (không tắt Nullable/không gắn tag) khi guard chặn nó.
        Assert.True(second.Nullable);
        Assert.Null(second.Annotations);
    }

    [Fact]
    public async Task Tagger_does_not_throw_for_the_same_CLR_type_seen_multiple_times_with_the_same_document()
    {
        const string documentName = nameof(Tagger_does_not_throw_for_the_same_CLR_type_seen_multiple_times_with_the_same_document);

        var first = MediaAssetSchema(nullable: true);
        await RunTaggerAsync(first, typeof(MediaAssetDto), documentName);

        var second = MediaAssetSchema(nullable: true);
        await RunTaggerAsync(second, typeof(MediaAssetDto), documentName);

        Assert.Equal("MediaAssetDto", first.Annotations[DuplicateNullableSchemaOccurrenceTagger.AnnotationKey]);
        Assert.Equal("MediaAssetDto", second.Annotations[DuplicateNullableSchemaOccurrenceTagger.AnnotationKey]);
    }

    [Fact]
    public void Staged_media_document_has_no_digit_suffixed_twin_schema_with_identical_properties()
    {
        // Guard rẻ tiền chạy trực tiếp trên contracts/openapi/.staging/media.v1.json (file task
        // MEDIA-001-D2 thực sự sửa) — không đọc contracts/openapi/media.v1.json (bản đã promote,
        // CHƯA được fix này chạm tới, đợi Gate 1 duyệt lại). Không hard-code path tuyệt đối: tìm
        // repo root qua pnpm-workspace.yaml, giống pattern FindRepoRoot() ở các test khác trong
        // repo (vd Imaging/ImagePresetCatalogTests.cs).
        var stagingPath = Path.Combine(FindRepoRoot(), "contracts", "openapi", ".staging", "media.v1.json");
        using var stream = File.OpenRead(stagingPath);
        using var doc = JsonDocument.Parse(stream);

        if (!doc.RootElement.TryGetProperty("components", out var components)
            || !components.TryGetProperty("schemas", out var schemas))
        {
            return;
        }

        var byName = schemas.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        foreach (var name in byName.Keys)
        {
            var match = System.Text.RegularExpressions.Regex.Match(name, @"^(?<base>.+?)(?<suffix>[0-9]+)$");
            if (!match.Success || !byName.TryGetValue(match.Groups["base"].Value, out var baseSchema))
            {
                continue;
            }

            var candidate = byName[name];
            var baseProps = PropertyNamesOf(baseSchema);
            var candidateProps = PropertyNamesOf(candidate);

            Assert.False(
                baseProps.SetEquals(candidateProps) && baseProps.Count > 0,
                $"'{name}' trông như bản trùng lặp của '{match.Groups["base"].Value}' (cùng tập " +
                "property) — đúng dạng bug MediaAssetDto2 mà DuplicateNullableSchemaOccurrenceTagger " +
                "/ DuplicateNullableSchemaDocumentTransformer phải ngăn. Chạy lại `pnpm contract:export media shop identity`?");
        }
    }

    private static HashSet<string> PropertyNamesOf(JsonElement schema) =>
        schema.TryGetProperty("properties", out var props)
            ? props.EnumerateObject().Select(p => p.Name).ToHashSet()
            : new HashSet<string>();

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
