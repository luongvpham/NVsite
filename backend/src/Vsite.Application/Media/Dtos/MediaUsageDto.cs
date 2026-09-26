namespace Vsite.Application.Media.Dtos;

/// <summary>T6, MEDIA-001 (#55) — `GET /shops/{shopId}/media/usage`. Chỉ tính bản Library/upload gốc
/// (`SourceAssetId IS NULL`), không tính clone/phái sinh — clone không chiếm quota riêng, dùng chung
/// dung lượng với bản Library sinh ra nó.</summary>
public sealed record MediaUsageDto(long UsedBytes);
