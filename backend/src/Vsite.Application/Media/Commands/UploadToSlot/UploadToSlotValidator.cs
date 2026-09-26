using FluentValidation;
using Vsite.Application.Common.Imaging;

namespace Vsite.Application.Media.Commands.UploadToSlot;

/// <summary>T5, MEDIA-001 — brief `task-T5-brief.md`. Preset lạ / focal ngoài [0, 1] → 422
/// (`ValidationBehavior` ném <c>Vsite.Application.Common.Exceptions.ValidationException</c>, cùng
/// envelope <c>VALIDATION_ERROR</c> dùng chung mọi module — xem
/// <c>UpdateShopValidator.IsReserved</c> cho tiền lệ "422 nhưng không có error_code riêng per-rule").
/// `FileName` cắt còn 200 ký tự KHÔNG phải rule reject — xử lý ở <see cref="MediaAssetWriter"/>
/// (khớp `MaxLength(200)` của cột DB, không phải validation error).</summary>
public sealed class UploadToSlotValidator : AbstractValidator<UploadToSlotCommand>
{
    public UploadToSlotValidator(IImagePresetCatalog presetCatalog)
    {
        RuleFor(x => x.ShopId).NotEmpty();

        RuleFor(x => x.Preset)
            .NotEmpty()
            .Must(preset => presetCatalog.TryGet(preset, out _))
                .WithMessage("MEDIA_UNKNOWN_PRESET: preset không tồn tại trong catalog.");

        RuleFor(x => x.FocalX).InclusiveBetween(0f, 1f);
        RuleFor(x => x.FocalY).InclusiveBetween(0f, 1f);

        RuleFor(x => x.AltText).MaximumLength(200);
    }
}
