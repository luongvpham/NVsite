using Sample.Domain;

namespace Sample.Application.Dtos;

public sealed record SampleSummary(Guid Id, string Name, SampleStatus Status, DateTimeOffset CreatedAtUtc);
