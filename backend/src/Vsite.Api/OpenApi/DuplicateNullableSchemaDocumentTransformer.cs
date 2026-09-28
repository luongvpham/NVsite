using System.Collections.Concurrent;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace Vsite.Api.OpenApi;

// TODO(.NET 10): file này gắn chặt với hành vi nội bộ của Microsoft.AspNetCore.OpenApi 9.0.x /
// OpenAPI.NET v1 (đặc biệt: document transformer chạy TRƯỚC khi components.schemas được populate —
// xem javadoc bên dưới). Khi nâng lên .NET 10 / OpenAPI.NET v2, PHẢI verify lại toàn bộ giả định
// về thứ tự pipeline bằng đúng cách đã làm ở đây (dump document.Components/document.Paths tại thời
// điểm transformer chạy), không chỉ chạy test xanh rồi coi là còn đúng.

/// <summary>
/// Microsoft.AspNetCore.OpenApi tự sinh schema trùng lặp có hậu tố số (<c>XDto2</c>, <c>XDto3</c>,
/// …) khi cùng một CLR type được dùng vừa non-nullable vừa nullable ở hai chỗ khác nhau trong cùng
/// document (ví dụ MEDIA-001: <c>SlotUploadResultDto.LibraryAsset</c> là <c>MediaAssetDto?</c> trong
/// khi các chỗ khác dùng <c>MediaAssetDto</c> không-null). Orval không tự gộp hai schema này — nó
/// sinh ra hai type TS không tương thích cho cùng một khái niệm (xem
/// <c>.superpowers/sdd/plan/task-F1-report.md</c>; Gate 1: sửa ở BE bằng transformer, không vá ở
/// FE).
///
/// <para>
/// ĐÃ THỬ và KHÔNG dùng được: một <see cref="IOpenApiDocumentTransformer"/> đơn lẻ đọc/sửa
/// <c>document.Components.Schemas</c> sau khi mọi transformer chạy xong. Thực nghiệm trực tiếp
/// trên Microsoft.AspNetCore.OpenApi 9.0.9 (cả build-time <c>GenerateOpenApiDocuments</c> lẫn
/// runtime <c>/openapi/{name}.json</c>) cho thấy <c>document.Components</c> vẫn <c>null</c> tại
/// thời điểm document transformer chạy — framework gom các schema đã sinh (từ một schema store nội
/// bộ) và "hoist" chúng vào <c>components.schemas</c> CHỈ SAU KHI mọi document transformer đã chạy
/// xong; không có hook công khai nào chạy sau bước hoist đó. Document transformer chỉ nhìn thấy
/// schema còn dạng INLINE (đầy đủ, chưa $ref) bên trong <c>document.Paths</c>. Sửa schema inline ở
/// đó thôi KHÔNG đủ: bản gốc (trước khi sửa) đã được một schema store nội bộ (chạy trong lúc
/// generate path, TRƯỚC document transformer) ghi nhận để hoist riêng — nếu không đổi luôn NỘI
/// DUNG mà schema transformer trả về (trước khi store ghi nhận), bản nullable gốc vẫn bị hoist
/// thành một component "mồ côi" (không $ref nào trỏ tới, xác nhận bằng thực nghiệm) dù document
/// transformer đã thay chỗ dùng nó bằng thứ khác.
/// </para>
///
/// <para>
/// Giải pháp THỰC SỰ hoạt động (verify bằng project tối giản, xem báo cáo task MEDIA-001-D2): hai
/// transformer phối hợp, dựa đúng vào thứ tự thật của pipeline (schema transformer chạy — và có
/// thể SỬA nội dung schema — TRƯỚC KHI schema đó được schema store ghi nhận để hoist; document
/// transformer chạy sau đó, trên cây <c>document.Paths</c> còn inline):
/// <list type="number">
/// <item><see cref="DuplicateNullableSchemaOccurrenceTagger"/> (schema transformer) — với MỌI
/// schema dạng object có properties VÀ đang nullable, resolve đúng reference id mà document NÀY sẽ
/// dùng (tôn trọng <c>options.CreateSchemaReferenceId</c> nếu có cấu hình riêng, rơi về default
/// của framework nếu không — xem javadoc trên chính tagger) từ <c>context.JsonTypeInfo</c>, rồi
/// NGAY TRONG LẦN GỌI NÀY: tắt
/// <c>Nullable</c> trên chính schema (để nội dung khớp hệt bản non-nullable của cùng type — schema
/// store nhờ vậy hoist gộp về MỘT component duy nhất thay vì tạo bản `…2` riêng) và gắn reference id
/// vào <see cref="OpenApiSchema.Annotations"/> (bag trong bộ nhớ, KHÔNG serialize ra JSON) để bước
/// sau tra lại.</item>
/// <item><see cref="DuplicateNullableSchemaDocumentTransformer"/> (document transformer) — với mỗi
/// node còn tag từ bước 1 (vẫn inline, chưa bị hoist), THAY node đó bằng
/// <c>{ allOf: [ $ref tới reference id đã gắn ], nullable: true }</c>, khôi phục đúng ngữ nghĩa
/// nullable tại ĐÚNG vị trí sử dụng ban đầu, sau khi bước 1 đã đảm bảo có đúng MỘT component gốc
/// (non-nullable) để trỏ tới.</item>
/// </list>
/// Vì bước 1 sửa TRƯỚC khi schema store ghi nhận, không có schema "mồ côi" nào phát sinh — không
/// cần "xoá" gì ở bước 2 cả. Vì việc gộp dựa trên <c>JsonTypeInfo</c> (cùng một CLR type), không
/// bao giờ gộp nhầm hai type khác nhau chỉ vì tên trùng tiền tố (khác hẳn cách match theo regex
/// tên schema — vốn có rủi ro đó).
/// </para>
///
/// Đăng ký CẢ HAI transformer cho MỌI document OpenAPI (mọi module) trong Program.cs, cùng chỗ với
/// <see cref="ProblemDetailsSchemaTransformer"/>.
/// </summary>
public sealed class DuplicateNullableSchemaDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        Apply(document);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Logic thuần, tách riêng khỏi <see cref="TransformAsync"/> để unit test gọi thẳng trên một
    /// <see cref="OpenApiDocument"/> dựng tay (sau khi các schema liên quan đã được
    /// <see cref="DuplicateNullableSchemaOccurrenceTagger"/> gắn tag), không cần dựng
    /// <see cref="OpenApiDocumentTransformerContext"/> hay chạy toàn bộ pipeline ASP.NET Core.
    /// </summary>
    public static void Apply(OpenApiDocument document)
    {
        void FixSlot(OpenApiSchema? schema, Action<OpenApiSchema> replace)
        {
            if (schema is null)
            {
                return;
            }

            if (schema.Annotations is not null
                && schema.Annotations.TryGetValue(DuplicateNullableSchemaOccurrenceTagger.AnnotationKey, out var tag)
                && tag is string refId)
            {
                replace(new OpenApiSchema
                {
                    AllOf = new List<OpenApiSchema>
                    {
                        new() { Reference = new OpenApiReference { Id = refId, Type = ReferenceType.Schema } },
                    },
                    Nullable = true,
                    // Giữ lại description riêng của node gốc (ví dụ do XML doc comment trên property
                    // C# sinh ra) — bọc allOf không nên làm mất thông tin này.
                    Description = schema.Description,
                });
                return;
            }

            FixSchemaChildren(schema);
        }

        void FixSchemaChildren(OpenApiSchema schema)
        {
            if (schema.Properties is not null)
            {
                foreach (var key in schema.Properties.Keys.ToList())
                {
                    var propKey = key;
                    FixSlot(schema.Properties[propKey], s => schema.Properties[propKey] = s);
                }
            }

            FixSlot(schema.Items, s => schema.Items = s);
            FixSlot(schema.AdditionalProperties, s => schema.AdditionalProperties = s);
            FixSlot(schema.Not, s => schema.Not = s);
            FixList(schema.AllOf);
            FixList(schema.OneOf);
            FixList(schema.AnyOf);
        }

        void FixList(IList<OpenApiSchema>? list)
        {
            if (list is null)
            {
                return;
            }

            for (var i = 0; i < list.Count; i++)
            {
                var index = i;
                FixSlot(list[index], s => list[index] = s);
            }
        }

        void FixContent(IDictionary<string, OpenApiMediaType>? content)
        {
            if (content is null)
            {
                return;
            }

            foreach (var mediaType in content.Values)
            {
                FixSlot(mediaType.Schema, s => mediaType.Schema = s);
            }
        }

        void FixResponse(OpenApiResponse response)
        {
            FixContent(response.Content);
            if (response.Headers is null)
            {
                return;
            }

            foreach (var header in response.Headers.Values)
            {
                FixSlot(header.Schema, s => header.Schema = s);
            }
        }

        // components.schemas thường vẫn rỗng ở thời điểm document transformer chạy (xem ghi chú ở
        // đầu file) — đi qua vẫn AN TOÀN và giữ để phòng phiên bản framework sau này populate sớm
        // hơn; phần việc THẬT nằm ở duyệt document.Paths bên dưới, nơi schema còn inline.
        if (document.Components?.Schemas is not null)
        {
            foreach (var schema in document.Components.Schemas.Values)
            {
                FixSchemaChildren(schema);
            }
        }

        if (document.Components?.Parameters is not null)
        {
            foreach (var parameter in document.Components.Parameters.Values)
            {
                FixSlot(parameter.Schema, s => parameter.Schema = s);
            }
        }

        if (document.Components?.RequestBodies is not null)
        {
            foreach (var requestBody in document.Components.RequestBodies.Values)
            {
                FixContent(requestBody.Content);
            }
        }

        if (document.Components?.Responses is not null)
        {
            foreach (var response in document.Components.Responses.Values)
            {
                FixResponse(response);
            }
        }

        if (document.Components?.Headers is not null)
        {
            foreach (var header in document.Components.Headers.Values)
            {
                FixSlot(header.Schema, s => header.Schema = s);
            }
        }

        if (document.Paths is null)
        {
            return;
        }

        foreach (var pathItem in document.Paths.Values)
        {
            foreach (var operation in pathItem.Operations.Values)
            {
                if (operation.Parameters is not null)
                {
                    foreach (var parameter in operation.Parameters)
                    {
                        FixSlot(parameter.Schema, s => parameter.Schema = s);
                    }
                }

                if (operation.RequestBody is not null)
                {
                    FixContent(operation.RequestBody.Content);
                }

                if (operation.Responses is not null)
                {
                    foreach (var response in operation.Responses.Values)
                    {
                        FixResponse(response);
                    }
                }
            }
        }
    }
}

/// <summary>
/// Nửa còn lại của <see cref="DuplicateNullableSchemaDocumentTransformer"/> — xem javadoc ở đó.
/// Chạy cho TỪNG schema trong lúc framework sinh document, TRƯỚC khi schema đó được schema store
/// nội bộ ghi nhận để hoist vào components.schemas. Với schema dạng object có properties và đang
/// nullable: tắt <c>Nullable</c> (để khớp nội dung với bản non-nullable của cùng type — tránh sinh
/// component thứ hai) và gắn reference id vào <see cref="OpenApiSchema.Annotations"/> để document
/// transformer khôi phục lại ngữ nghĩa nullable đúng chỗ.
///
/// Việc này áp dụng cho MỌI schema object nullable (không chỉ những type có bản non-nullable song
/// song) — đã verify KHÔNG gây tác dụng phụ cho type "chỉ dùng nullable" (không có sibling
/// non-null): kết quả vẫn là một component sạch + $ref bọc allOf/nullable tại đúng chỗ dùng, không
/// sinh schema mồ côi nào. Không cần theo dõi "đã thấy cả hai dạng chưa" giữa các lần gọi (tránh
/// phụ thuộc thứ tự sinh path, vốn không đảm bảo cùng lúc biết đủ mọi occurrence của một type).
///
/// <para>
/// Reference id KHÔNG được tính bằng <c>OpenApiOptions.CreateDefaultSchemaReferenceId</c> trực
/// tiếp — đó chỉ là hàm mặc định, không phải id mà schema store THỰC SỰ dùng nếu document đã cấu
/// hình <c>options.CreateSchemaReferenceId</c> riêng. Thay vào đó, resolve đúng
/// <see cref="OpenApiOptions"/> đã đăng ký cho document hiện tại qua
/// <see cref="IOptionsMonitor{TOptions}"/> (named options, key = <c>context.DocumentName</c> — xem
/// <c>OpenApiServiceCollectionExtensions.AddOpenApi</c>) rồi gọi đúng hàm đó (rơi về default nếu
/// document không custom). Nếu không làm vậy: hai CLR type khác nhau nhưng cùng tên đơn giản (vd
/// <c>Media.FooDto</c> và <c>Shared.FooDto</c>) có thể bị tính ra CÙNG MỘT id mặc định — schema
/// store sẽ tự thêm hậu tố cho một trong hai (đúng cơ chế gây ra bug này ngay từ đầu!) — và tagger
/// sẽ trỏ nhầm bản nullable của type này sang schema của type KIA, âm thầm gộp hai type không liên
/// quan. <see cref="TaggedRefIdOwners"/> canh gác đúng tình huống đó: nếu một reference id (đã
/// gắn tag) từng thuộc về một <see cref="Type"/> khác, ném exception ngay lúc generate thay vì âm
/// thầm phát ra $ref sai.
/// </para>
///
/// Đăng ký cùng <see cref="DuplicateNullableSchemaDocumentTransformer"/> cho MỌI document trong
/// Program.cs.
/// </summary>
public sealed class DuplicateNullableSchemaOccurrenceTagger : IOpenApiSchemaTransformer
{
    public const string AnnotationKey = "x-vsite-duplicate-nullable-ref-id";

    /// <summary>
    /// (documentName, referenceId) -> CLR type đã "chiếm" id đó. <c>static</c> có chủ đích — xem
    /// ghi chú tương tự trong <see cref="DuplicateNullableSchemaDocumentTransformer"/> (schema
    /// transformer và document transformer của một lần generate có thể là hai instance khác nhau).
    /// Chỉ dùng để PHÁT HIỆN xung đột id giữa các type ta thực sự có TAG (nullable) — không cần
    /// theo dõi type non-nullable (chúng không đi qua tagger nên không được ta rewrite).
    /// </summary>
    private static readonly ConcurrentDictionary<(string DocumentName, string RefId), Type> TaggedRefIdOwners = new();

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (!schema.Nullable || schema.Type != "object" || schema.Properties is not { Count: > 0 } || context.JsonTypeInfo is null)
        {
            return Task.CompletedTask;
        }

        var refId = ResolveReferenceId(context);
        if (string.IsNullOrEmpty(refId))
        {
            return Task.CompletedTask;
        }

        var clrType = context.JsonTypeInfo.Type;
        var owner = TaggedRefIdOwners.GetOrAdd((context.DocumentName, refId), clrType);
        if (owner != clrType)
        {
            throw new InvalidOperationException(
                $"DuplicateNullableSchemaOccurrenceTagger: reference id '{refId}' trong document " +
                $"'{context.DocumentName}' được tính ra bởi CẢ '{owner.FullName}' lẫn '{clrType.FullName}'. " +
                "Không thể dedupe an toàn khi hai CLR type khác nhau trùng reference id — sẽ trỏ " +
                "nhầm $ref sang type kia. Đổi tên một trong hai type, hoặc cấu hình " +
                "options.CreateSchemaReferenceId để tách id, rồi generate lại.");
        }

        schema.Nullable = false;
        schema.Annotations ??= new Dictionary<string, object>();
        schema.Annotations[AnnotationKey] = refId;

        return Task.CompletedTask;
    }

    private static string? ResolveReferenceId(OpenApiSchemaTransformerContext context)
    {
        var configured = context.ApplicationServices
            .GetService<IOptionsMonitor<OpenApiOptions>>()
            ?.Get(context.DocumentName)
            .CreateSchemaReferenceId;

        return configured?.Invoke(context.JsonTypeInfo) ?? OpenApiOptions.CreateDefaultSchemaReferenceId(context.JsonTypeInfo);
    }
}
