using FluentValidation;

namespace Vsite.Application.Media.Commands.UploadToSlot;

/// <summary>T5, MEDIA-001 — brief T5. Focal ngoài [0, 1] → 422
/// (`ValidationBehavior` ném <c>Vsite.Application.Common.Exceptions.ValidationException</c>, cùng
/// envelope <c>VALIDATION_ERROR</c> dùng chung mọi module — xem
/// <c>UpdateShopValidator.IsReserved</c> cho tiền lệ "422 nhưng không có error_code riêng per-rule").
///
/// Preset KHÔNG check tồn tại ở đây (review sau T5) — brief đòi `error_code` cụ thể
/// <c>MEDIA_UNKNOWN_PRESET</c> ở top-level ProblemDetails, mà envelope <c>ValidationException</c>
/// LUÔN trả <c>VALIDATION_ERROR</c> chung bất kể rule nào fail. Việc check preset tồn tại + ném
/// <see cref="Vsite.Domain.Exceptions.UnprocessableException"/> đúng mã đã chuyển hẳn sang
/// <c>UploadToSlotHandler</c> — validator chỉ còn giữ NotEmpty (tránh gọi preset catalog với chuỗi
/// rỗng).
///
/// `FileName` cắt còn 200 ký tự KHÔNG phải rule reject — xử lý ở <see cref="MediaAssetWriter"/>
/// (khớp `MaxLength(200)` của cột DB, không phải validation error).</summary>
public sealed class UploadToSlotValidator : AbstractValidator<UploadToSlotCommand>
{
    public UploadToSlotValidator()
    {
        RuleFor(x => x.ShopId).NotEmpty();

        RuleFor(x => x.Preset).NotEmpty();

        RuleFor(x => x.FocalX).InclusiveBetween(0f, 1f);
        RuleFor(x => x.FocalY).InclusiveBetween(0f, 1f);

        RuleFor(x => x.AltText).MaximumLength(200);
    }
}
