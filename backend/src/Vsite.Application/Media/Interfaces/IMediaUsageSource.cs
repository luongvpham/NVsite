namespace Vsite.Application.Media.Interfaces;

/// <summary>
/// WEBSITE-008 T6 (#117) — một nguồn "id ảnh đang được dùng bởi shop hiện tại", gom lại bởi job dọn ảnh mồ côi (T7).
/// Public Contract: Media KHÔNG tham chiếu module Website; Website (Infrastructure) cài interface này. Shop lấy từ
/// <c>ITenantContext</c>. Kết quả dùng để QUYẾT ĐỊNH XOÁ, nên cài đặt phải NÉM khi không đọc được nguồn của mình (R5) —
/// không bao giờ trả tập thiếu/rỗng thay cho lỗi.
/// </summary>
public interface IMediaUsageSource
{
    Task<IReadOnlySet<Guid>> CollectUsedAssetIdsAsync(CancellationToken ct);
}
