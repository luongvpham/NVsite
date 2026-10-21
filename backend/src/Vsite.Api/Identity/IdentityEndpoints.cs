using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Vsite.Application.Identity.Auth.Commands.ChangePassword;
using Vsite.Application.Identity.Auth.Commands.ForgotPassword;
using Vsite.Application.Identity.Auth.Commands.Login;
using Vsite.Application.Identity.Auth.Commands.Logout;
using Vsite.Application.Identity.Auth.Commands.RefreshToken;
using Vsite.Application.Identity.Auth.Commands.Register;
using Vsite.Application.Identity.Auth.Commands.ResetPassword;
using Vsite.Application.Identity.Auth.Commands.VerifyEmail;
using Vsite.Application.Identity.Auth.Dtos;
using Vsite.Application.Identity.Auth.Queries.GetMe;
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
            .WithName("register")
            .Produces<RegisterResult>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        auth.MapGet("/verify-email", async (string token, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new VerifyEmailCommand(token), ct)))
            .WithName("verifyEmail")
            .Produces<VerifyEmailResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        // #101 — refresh token KHÔNG nằm trong body: endpoint tách nó ra, đặt vào cookie httpOnly và chỉ
        // trả access token. Application vẫn dùng AuthTokenResult (có refresh) nội bộ.
        auth.MapPost("/login", async (LoginRequest body, HttpContext http, IWebHostEnvironment env, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new LoginCommand(body.Email, body.Password), ct);
            return Results.Ok(SetRefreshCookie(http, env, result));
        })
            .WithName("login")
            .WithDescription("Response set cookie httpOnly `vsite_rt` (refresh token, Path=/api/auth) và chỉ trả access token trong body. Client phải nhận cookie (vd. withCredentials). (#101)")
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        auth.MapPost("/refresh-token", async (HttpContext http, IWebHostEnvironment env, ISender sender, CancellationToken ct) =>
        {
            // Thiếu cookie = chưa đăng nhập / đã logout → 401 như token sai (không phải 422: không còn input để validate).
            var refreshToken = RefreshTokenCookie.Read(http.Request);
            if (refreshToken is null)
            {
                return UnauthorizedProblem("Thiếu refresh token.");
            }

            try
            {
                var result = await sender.Send(new RefreshTokenCommand(refreshToken), ct);
                return Results.Ok(SetRefreshCookie(http, env, result));
            }
            catch (UnauthorizedAccessException ex)
            {
                // Cookie sai/hết hạn/đã thu hồi phải bị XOÁ khỏi trình duyệt: giữ lại thì mỗi lần mở trang
                // lại gửi một token đã thu hồi → handler coi là reuse và thu hồi cả nhóm phiên (#3).
                // Trả ProblemDetails tại đây (không ném tiếp) vì UseExceptionHandler xoá header đã set.
                RefreshTokenCookie.Delete(http.Response, env.IsDevelopment());
                return UnauthorizedProblem(ex.Message);
            }
        })
            .WithName("refreshToken")
            .WithSummary("Đổi refresh token (cookie `vsite_rt`) lấy access token mới.")
            .WithDescription("Không có body. Refresh token đi bằng cookie httpOnly `vsite_rt` (client phải gửi kèm cookie, vd. withCredentials); response set cookie mới (xoay vòng) và chỉ trả access token. 401 nếu thiếu/sai/hết hạn/đã thu hồi — cookie sai bị xoá. (#101)")
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // AllowAnonymous có chủ đích: phải đăng xuất được kể cả khi access token đã hết hạn — danh tính
        // ở đây là chính cookie. Luôn 204, kể cả không có cookie/token lạ (không lộ token có tồn tại không).
        auth.MapPost("/logout", async (HttpContext http, IWebHostEnvironment env, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new LogoutCommand(RefreshTokenCookie.Read(http.Request)), ct);
            RefreshTokenCookie.Delete(http.Response, env.IsDevelopment());
            return Results.NoContent();
        })
            .WithName("logout")
            .WithDescription("Thu hồi refresh token trong cookie `vsite_rt` (nếu có) và xoá cookie. Luôn 204, kể cả không có cookie hoặc token đã thu hồi. Không cần access token. (#101)")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent);

        auth.MapPost("/forgot-password", async (ForgotPasswordRequest body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ForgotPasswordCommand(body.Email), ct);
            return Results.NoContent();
        })
            .WithName("forgotPassword")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        auth.MapPost("/reset-password", async (ResetPasswordRequest body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ResetPasswordCommand(body.Token, body.NewPassword), ct);
            return Results.NoContent();
        })
            .WithName("resetPassword")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        // Quyết định #32 — mọi endpoint dưới đây yêu cầu JWT hợp lệ. `RequireAuthorization()`
        // không tham số = bất kỳ audience nào (Main/Portal/Shop); `RequireGlobalScope` = chặn
        // token `shop:{shopId}` (xem AuthenticationSetup + ShopMembershipValidationMiddleware).
        auth.MapGet("/me", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetMeQuery(), ct)))
            .WithName("getMe")
            .RequireAuthorization()
            .Produces<MeDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/me/change-password", async (ChangePasswordRequest body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ChangePasswordCommand(body.CurrentPassword, body.NewPassword), ct);
            return Results.NoContent();
        })
            .WithName("changePassword")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    // Cùng shape với UnauthorizedAccessExceptionHandler (error_code UNAUTHORIZED) để FE xử lý một kiểu.
    private static IResult UnauthorizedProblem(string detail) => Results.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Unauthorized",
        detail: detail,
        extensions: new Dictionary<string, object?> { ["error_code"] = "UNAUTHORIZED" });

    private static AccessTokenResponse SetRefreshCookie(HttpContext http, IWebHostEnvironment env, AuthTokenResult result)
    {
        RefreshTokenCookie.Append(http.Response, result.RefreshToken, result.RefreshTokenExpiresAt, env.IsDevelopment());
        return new AccessTokenResponse(result.AccessToken, result.AccessTokenExpiresAt);
    }
}

public sealed record RegisterRequest(string Email, string Password, string? FullName);
public sealed record LoginRequest(string Email, string Password);

/// <summary>Response công khai của login/refresh (#101) — KHÔNG có refresh token (nằm ở cookie).</summary>
public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
