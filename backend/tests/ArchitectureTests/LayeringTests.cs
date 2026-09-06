using DomainSample = Sample.Domain.Sample;
using NetArchTest.Rules;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// Enforce chiều phụ thuộc Domain ← Application ← Infrastructure ← Api (backend/CLAUDE.md).
/// Test phải fail thật khi vi phạm — xem ghi chú kiểm chứng trong từng test.
/// </summary>
public sealed class LayeringTests
{
    [Fact]
    public void Domain_Should_Not_Reference_EfCore_MediatR_Or_AspNetCore()
    {
        var result = Types.InAssembly(typeof(DomainSample).Assembly)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "MediatR", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Domain_Should_Not_Reference_Application()
    {
        var result = Types.InAssembly(typeof(DomainSample).Assembly)
            .Should()
            .NotHaveDependencyOn("Sample.Application")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_Should_Not_Reference_Infrastructure_Or_Api()
    {
        var result = Types.InAssembly(typeof(Sample.Application.DependencyInjection).Assembly)
            .Should()
            .NotHaveDependencyOnAny("Sample.Infrastructure", "Sample.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_Should_Not_Reference_Api()
    {
        var result = Types.InAssembly(typeof(Sample.Infrastructure.DependencyInjection).Assembly)
            .Should()
            .NotHaveDependencyOn("Sample.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Vi phạm ở: " + string.Join(", ", result.FailingTypeNames ?? []);
}
