using FluentValidation.Results;
using Vsite.Domain.Exceptions;

namespace Vsite.Application.Common.Exceptions;

/// <summary>422 — bọc FluentValidation failures (architecture-guide.md §2). ValidationBehavior
/// ném exception này, KHÔNG handler nào tự validate input tay.</summary>
public sealed class ValidationException : AppException
{
    public ValidationException()
        : base("VALIDATION_ERROR", "Một hoặc nhiều field không hợp lệ.", statusCode: 422)
    {
    }

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base(
            "VALIDATION_ERROR",
            "Một hoặc nhiều field không hợp lệ.",
            statusCode: 422,
            errors: failures
                .GroupBy(f => f.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray()))
    {
    }
}
