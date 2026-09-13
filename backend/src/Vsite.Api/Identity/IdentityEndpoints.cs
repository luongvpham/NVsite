using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Vsite.Application.Identity.Auth.Commands.ChangePassword;
using Vsite.Application.Identity.Auth.Commands.ForgotPassword;
using Vsite.Application.Identity.Auth.Commands.Login;
using Vsite.Application.Identity.Auth.Commands.RefreshToken;
using Vsite.Application.Identity.Auth.Commands.Register;
using Vsite.Application.Identity.Auth.Commands.ResetPassword;
using Vsite.Application.Identity.Auth.Commands.VerifyEmail;
using Vsite.Application.Identity.Auth.Dtos;
using Vsite.Application.Identity.Auth.Queries.GetMe;
using Vsite.Application.Identity.Auth.Queries.ListMyShops;
using Vsite.Domain.Authorization;

namespace Vsite.Api.Identity;

/// <summary>
/// MỘT bộ route duy nhất cho mọi context (vsite.vn / shop / portal) — `Vsite.Api.Tenancy.
/// TenantResolutionMiddleware` đã resolve `ITenantContext` (audience + ShopId) từ Host TRƯỚC KHI
/// request tới đây (Quyết định #7 thu hẹp, #21.4). Endpoint/command KHÔNG nhận ShopId từ route hay
/// body nữa — FE chỉ cần gọi đúng MỘT path tương đối (`/auth/login`...) từ domain nào đang phục vụ
/// trang, không cần biết ShopId dạng GUID.
///
/// Mọi endpoint khai đủ status code có thể trả (Quyết định #19) — 422 luôn có (ValidationBehavior),
/// còn lại tuỳ handler (401 sai credential, 400/404/409 theo AppException cụ thể).
/// </summary>
public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth").WithTags("Identity").WithGroupName("identity");

        auth.MapPost("/register", async (RegisterRequest body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new RegisterCommand(body.Email, body.Password, body.FullName), ct)))
            .Produces<RegisterResult>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        auth.MapGet("/verify-email", async (string token, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new VerifyEmailCommand(token), ct)))
            .Produces<VerifyEmailResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        auth.MapPost("/login", async (LoginRequest body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new LoginCommand(body.Email, body.Password), ct)))
            .Produces<AuthTokenResult>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        auth.MapPost("/refresh-token", async (RefreshTokenRequest body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new RefreshTokenCommand(body.RefreshToken), ct)))
            .Produces<AuthTokenResult>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        auth.MapPost("/forgot-password", async (ForgotPasswordRequest body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ForgotPasswordCommand(body.Email), ct);
            return Results.NoContent();
        })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        auth.MapPost("/reset-password", async (ResetPasswordRequest body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ResetPasswordCommand(body.Token, body.NewPassword), ct);
            return Results.NoContent();
        })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        // Quyết định #32 — mọi endpoint dưới đây yêu cầu JWT hợp lệ. `RequireAuthorization()`
        // không tham số = bất kỳ audience nào (Main/Portal/Shop); `RequireGlobalScope` = chặn
        // token `shop:{shopId}` (xem AuthenticationSetup + ShopMembershipValidationMiddleware).
        auth.MapGet("/me", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetMeQuery(), ct)))
            .RequireAuthorization()
            .Produces<MeDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapGet("/me/shops", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListMyShopsQuery(), ct)))
            .RequireAuthorization(AuthPolicies.RequireGlobalScope)
            .Produces<IReadOnlyList<MyShopDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        auth.MapPost("/me/change-password", async (ChangePasswordRequest body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ChangePasswordCommand(body.CurrentPassword, body.NewPassword), ct);
            return Results.NoContent();
        })
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}

public sealed record RegisterRequest(string Email, string Password, string? FullName);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
