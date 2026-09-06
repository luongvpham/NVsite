using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Sample.Api.Errors;
using Sample.Application.Dtos;
using Sample.Application.Samples;
using Shared.Pagination;

namespace Sample.Api;

public static class SampleEndpoints
{
    public static IEndpointRouteBuilder MapSampleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/samples")
            .WithGroupName("sample")
            .WithTags("Sample");

        group.MapGet("/", async (int? page, int? pageSize, ISender sender, CancellationToken ct) =>
            {
                var effectivePage = page is > 0 ? page.Value : 1;
                var effectivePageSize = Math.Min(pageSize is > 0 ? pageSize.Value : 20, 100);
                var result = await sender.Send(new ListSamplesQuery(effectivePage, effectivePageSize), ct);
                return Results.Ok(result);
            })
            .WithName("ListSamples")
            .Produces<PagedResult<SampleSummary>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var sample = await sender.Send(new GetSampleByIdQuery(id), ct);
                return sample is null
                    ? ApiError.NotFound("sample_not_found", $"Sample '{id}' không tồn tại.")
                    : Results.Ok(sample);
            })
            .WithName("GetSampleById")
            .Produces<SampleDetail>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async ([FromBody] CreateSampleCommand command, ISender sender, CancellationToken ct) =>
            {
                var created = await sender.Send(command, ct);
                return Results.Created($"/samples/{created.Id}", created);
            })
            .WithName("CreateSample")
            .Produces<SampleDetail>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        app.MapGroup("/shops/{shopId:guid}/samples")
            .WithGroupName("sample")
            .WithTags("Sample")
            .MapGet("/{id:guid}", async (Guid shopId, Guid id, ISender sender, CancellationToken ct) =>
            {
                var sample = await sender.Send(new GetSampleByIdForShopQuery(shopId, id), ct);
                return sample is null
                    ? ApiError.NotFound("sample_not_found", $"Sample '{id}' không tồn tại trong shop '{shopId}'.")
                    : Results.Ok(sample);
            })
            .WithName("GetSampleByIdForShop")
            .Produces<SampleDetail>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
