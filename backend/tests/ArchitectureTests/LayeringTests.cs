using NetArchTest.Rules;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// Enforce chiều phụ thuộc Domain ← Application ← Infrastructure ← Api
/// (DesignIdeal/architecture-guide.md §1). Từ 2026-09-13 mỗi tầng là MỘT assembly cho toàn hệ
/// (không còn 4 project mỗi module) — ranh giới giữa các module do
/// <see cref="ModuleBoundaryTests"/> lo, theo namespace.
/// </summary>
public sealed class LayeringTests
{
    private static readonly System.Reflection.Assembly Domain = typeof(Vsite.Domain.Common.BaseEntity).Assembly;
    private static readonly System.Reflection.Assembly Application = typeof(Vsite.Application.Common.Interfaces.IAppDbContext).Assembly;
    private static readonly System.Reflection.Assembly Infrastructure = typeof(Vsite.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_Should_Not_Reference_EfCore_Or_AspNetCore()
    {
        // MediatR KHÔNG nằm trong danh sách cấm: BaseEvent : INotification chỉ dùng marker
        // interface (architecture-guide.md §2). Mọi thứ còn lại thì cấm.
        var result = Types.InAssembly(Domain)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Domain chạm hạ tầng", result));
    }

    [Fact]
    public void Domain_Should_Not_Reference_Outer_Layers()
    {
        var result = Types.InAssembly(Domain)
            .Should()
            .NotHaveDependencyOnAny("Vsite.Application", "Vsite.Infrastructure", "Vsite.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Domain phụ thuộc tầng ngoài", result));
    }

    [Fact]
    public void Application_Should_Not_Reference_Infrastructure_Or_Api()
    {
        var result = Types.InAssembly(Application)
            .Should()
            .NotHaveDependencyOnAny("Vsite.Infrastructure", "Vsite.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Application phụ thuộc tầng ngoài", result));
    }

    [Fact]
    public void Application_Should_Not_Reference_Http_Or_DbProvider()
    {
        // Hai ngoại lệ CÓ CHỦ ĐÍCH, không phải nới lỏng vì test đỏ:
        //  - `Microsoft.EntityFrameworkCore`: IAppDbContext dùng DbSet<T> — data shape, đúng pattern
        //    Clean Architecture (architecture-guide.md §11 bước 2).
        //  - `Microsoft.AspNetCore.Identity`: chỉ để lấy IPasswordHasher<User> (Quyết định #3).
        //    Tên namespace gây hiểu nhầm — package thật là `Microsoft.Extensions.Identity.Core`,
        //    KHÔNG kéo theo HTTP. Vì vậy cấm đích danh các namespace HTTP thật bên dưới thay vì
        //    cấm cả cây `Microsoft.AspNetCore`.
        var result = Types.InAssembly(Application)
            .Should()
            .NotHaveDependencyOnAny(
                "Npgsql",
                "Microsoft.AspNetCore.Http",
                "Microsoft.AspNetCore.Mvc",
                "Microsoft.AspNetCore.Builder",
                "Microsoft.AspNetCore.Routing",
                "Microsoft.AspNetCore.Authentication",
                "Microsoft.AspNetCore.Authorization")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Application chạm provider/HTTP", result));
    }

    [Fact]
    public void Infrastructure_Should_Not_Reference_Api()
    {
        var result = Types.InAssembly(Infrastructure)
            .Should()
            .NotHaveDependencyOn("Vsite.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Infrastructure phụ thuộc Api", result));
    }

    private static string Describe(string what, TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : $"{what} — vi phạm ở: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
