using MediatR;
using Sample.Application.Abstractions;
using Sample.Application.Dtos;
using Shared.Pagination;

namespace Sample.Application.Samples;

public sealed record ListSamplesQuery(int Page, int PageSize) : IRequest<PagedResult<SampleSummary>>;

public sealed class ListSamplesQueryHandler(ISampleRepository repository)
    : IRequestHandler<ListSamplesQuery, PagedResult<SampleSummary>>
{
    public async Task<PagedResult<SampleSummary>> Handle(ListSamplesQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await repository.ListAsync(request.Page, request.PageSize, cancellationToken);

        var summaries = items
            .Select(s => new SampleSummary(s.Id, s.Name, s.Status, s.CreatedAtUtc))
            .ToList();

        return new PagedResult<SampleSummary>(summaries, total, request.Page, request.PageSize);
    }
}
