using MediatR;
using Sample.Application.Abstractions;
using Sample.Application.Dtos;

namespace Sample.Application.Samples;

/// <summary>
/// Nested REST — chứng minh hình dạng route ownership (Quyết định #19).
/// Ownership validate trong câu query (WHERE ShopId = ... AND Id = ...), không load rồi check ở memory (#21.3).
/// </summary>
public sealed record GetSampleByIdForShopQuery(Guid ShopId, Guid Id) : IRequest<SampleDetail?>;

public sealed class GetSampleByIdForShopQueryHandler(ISampleRepository repository)
    : IRequestHandler<GetSampleByIdForShopQuery, SampleDetail?>
{
    public async Task<SampleDetail?> Handle(GetSampleByIdForShopQuery request, CancellationToken cancellationToken)
    {
        var sample = await repository.GetByIdForShopAsync(request.ShopId, request.Id, cancellationToken);
        return sample is null ? null : SampleDetail.FromDomain(sample);
    }
}
