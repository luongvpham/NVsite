using AppValidationException = Vsite.Application.Common.Exceptions.ValidationException;
using FluentValidation;
using MediatR;

namespace Vsite.Application.Common.Behaviors;

/// <summary>Chạy mọi `IValidator&lt;TRequest&gt;` đã đăng ký trước khi handler chạy — handler
/// không bao giờ tự validate input tay (architecture-guide.md §3).</summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
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
