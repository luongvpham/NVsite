using DomainMarker = Identity.Domain.Entities.User;
using NetArchTest.Rules;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// Enforce chiều phụ thuộc Domain ← Application ← Infrastructure ← Api (backend/CLAUDE.md), viết
/// lại cho module Identity (Bước 3) sau khi Sample bị xoá (docs/tasks/CLEANUP-SAMPLE.md — file này
/// từng rỗng tạm ở Phase 0 của Bước 3, xem git blame).
///
/// TODO(Bước 3 Phase 2+): thêm test `Application_Should_Not_Reference_Infrastructure_Or_Api` và
/// `Infrastructure_Should_Not_Reference_Api` khi `Identity.Application`/`Identity.Api` tồn tại.
/// </summary>
public sealed class LayeringTests
{
    [Fact]
    public void Domain_Should_Not_Reference_EfCore_MediatR_Or_AspNetCore()
    {
        var result = Types.InAssembly(typeof(DomainMarker).Assembly)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "MediatR", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Domain_Should_Not_Reference_Infrastructure()
    {
        var result = Types.InAssembly(typeof(DomainMarker).Assembly)
            .Should()
            .NotHaveDependencyOn("Identity.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Vi phạm ở: " + string.Join(", ", result.FailingTypeNames ?? []);
}
