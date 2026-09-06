using Microsoft.EntityFrameworkCore;
using Sample.Application.Abstractions;
using DomainSample = Sample.Domain.Sample;

namespace Sample.Infrastructure;

public sealed class SampleRepository(SampleDbContext dbContext) : ISampleRepository
{
    public async Task AddAsync(DomainSample sample, CancellationToken cancellationToken)
    {
        await dbContext.Samples.AddAsync(sample, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<DomainSample?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Samples.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<DomainSample?> GetByIdForShopAsync(Guid shopId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Samples.FirstOrDefaultAsync(s => s.ShopId == shopId && s.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<DomainSample> Items, int Total)> ListAsync(
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Samples.OrderBy(s => s.CreatedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
