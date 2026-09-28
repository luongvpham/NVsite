using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
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

    private static async Task RunTaggerAsync(OpenApiSchema schema, Type clrType, string documentName = "test")
    {
        var tagger = new DuplicateNullableSchemaOccurrenceTagger();
        var context = new OpenApiSchemaTransformerContext
        {
            DocumentName = documentName,
            JsonTypeInfo = JsonSerializerOptions.Default.GetTypeInfo(clrType),
            JsonPropertyInfo = null,
            ParameterDescription = null,
            ApplicationServices = new ServiceCollection().BuildServiceProvider(),
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
        Assert.Equal("MediaAssetDto", schema.Annotations[DuplicateNullableSchemaOccurrenceTaggerTestAccessor.AnnotationKey]);
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
}

/// <summary>Chỉ để test đọc đúng annotation key nội bộ (internal, cùng assembly Vsite.Api).</summary>
internal static class DuplicateNullableSchemaOccurrenceTaggerTestAccessor
{
    public const string AnnotationKey = "x-vsite-duplicate-nullable-ref-id";
}
