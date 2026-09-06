using DomainSample = Sample.Domain.Sample;

namespace Sample.Application.Abstractions;

public interface ISampleRepository
{
    Task AddAsync(DomainSample sample, CancellationToken cancellationToken);

    Task<DomainSample?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<DomainSample?> GetByIdForShopAsync(Guid shopId, Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<DomainSample> Items, int Total)> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}
