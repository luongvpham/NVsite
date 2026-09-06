using DomainSample = Sample.Domain.Sample;
using Sample.Domain;

namespace Sample.Application.Dtos;

public sealed record SampleDetail(Guid Id, Guid ShopId, string Name, SampleStatus Status, DateTimeOffset CreatedAtUtc)
{
    public static SampleDetail FromDomain(DomainSample sample) =>
        new(sample.Id, sample.ShopId, sample.Name, sample.Status, sample.CreatedAtUtc);
}
