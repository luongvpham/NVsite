using AppValidationException = Vsite.Application.Common.Exceptions.ValidationException;
using FluentValidation;
using MediatR;

namespace Vsite.Application.Common.Behaviors;

/// <summary>Chạy mọi `IValidator&lt;TRequest&gt;` đã đăng ký trước khi handler chạy — handler
/// không bao giờ tự validate input tay (architecture-guide.md §3).
///
/// ⚠️ Ràng buộc là `notnull`, KHÔNG phải `IRequest&lt;TResponse&gt;`: với MediatR 12, command trả về
/// void (`: IRequest`) chạy pipeline dạng `IPipelineBehavior&lt;TRequest, Unit&gt;` nhưng KHÔNG phải
/// `IRequest&lt;Unit&gt;` — ràng buộc cũ làm DI âm thầm bỏ qua behavior này, validator của
/// ChangePassword/ResetPassword/ForgotPassword không bao giờ chạy (REFACTOR-BE-001).
/// `ValidationPipelineCoverageTests` khoá lại điều này.</summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count > 0)
        {
            throw new AppValidationException(failures);
        }

        return await next();
    }
}
