using MediatR;
using Sample.Application.Abstractions;
using Sample.Application.Dtos;

namespace Sample.Application.Samples;

public sealed record GetSampleByIdQuery(Guid Id) : IRequest<SampleDetail?>;

public sealed class GetSampleByIdQueryHandler(ISampleRepository repository)
    : IRequestHandler<GetSampleByIdQuery, SampleDetail?>
{
    public async Task<SampleDetail?> Handle(GetSampleByIdQuery request, CancellationToken cancellationToken)
    {
        var sample = await repository.GetByIdAsync(request.Id, cancellationToken);
        return sample is null ? null : SampleDetail.FromDomain(sample);
    }
}
