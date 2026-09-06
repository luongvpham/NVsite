namespace Sample.Domain;

/// <summary>
/// Entity throwaway, chỉ để chứng minh pipeline Bước 1. Xoá trước khi bắt đầu Identity (Bước 3).
/// </summary>
public sealed class Sample
{
    public Guid Id { get; private set; }
    public Guid ShopId { get; private set; }
    public string Name { get; private set; }
    public SampleStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Sample(Guid id, Guid shopId, string name, SampleStatus status, DateTimeOffset createdAtUtc)
    {
        Id = id;
        ShopId = shopId;
        Name = name;
        Status = status;
        CreatedAtUtc = createdAtUtc;
    }

    public static Sample Create(Guid shopId, string name, SampleStatus status, TimeProvider timeProvider) =>
        new(Guid.NewGuid(), shopId, name, status, timeProvider.GetUtcNow());
}
