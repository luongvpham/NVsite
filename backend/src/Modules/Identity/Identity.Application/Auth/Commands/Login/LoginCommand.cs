using Identity.Application.Auth.Dtos;
using MediatR;

namespace Identity.Application.Auth.Commands.Login;

/// <summary>
/// Audience/ShopId đến từ `ITenantContext` (resolve bởi `Api.Tenancy.TenantResolutionMiddleware`
/// theo Host — Quyết định #7 thu hẹp), KHÔNG phải field của command/route param do client cung cấp
/// (Quyết định #21.4). Một endpoint `/auth/login` duy nhất cho mọi context.
/// </summary>
public sealed record LoginCommand(string Email, string Password) : IRequest<AuthTokenResult>;
