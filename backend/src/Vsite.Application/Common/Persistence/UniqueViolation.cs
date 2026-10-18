using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Vsite.Application.Common.Persistence;

/// <summary>
/// Nhận diện lỗi lúc <c>SaveChanges</c> mà không phụ thuộc Npgsql (Application không tham chiếu provider): Npgsql
/// <c>PostgresException</c> kế thừa <see cref="DbException"/> và ghi đè <see cref="DbException.SqlState"/> (.NET 8+).
/// Dùng chung cho handler (đổi 23505 race thành 409 nghiệp vụ) và <c>ConcurrencyAndUniqueExceptionHandler</c> (409 chung).
/// </summary>
public static class UniqueViolation
{
    /// <summary>SQLSTATE của <c>unique_violation</c> trong PostgreSQL.</summary>
    public const string SqlState = "23505";

    /// <summary><c>true</c> khi <paramref name="exception"/> hoặc một <c>InnerException</c> của nó là DbException có SQLSTATE 23505.</summary>
    public static bool IsUniqueViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException { SqlState: SqlState })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Lỗi concurrency token (<c>xmin</c> lệch) — kể cả bị bọc.</summary>
    public static bool IsConcurrencyConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException)
            {
                return true;
            }
        }

        return false;
    }
}
