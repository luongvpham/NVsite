using MediatR;

namespace Vsite.Application.Identity.Auth.Commands.Logout;

/// <summary>`RefreshToken` null/rỗng (không có cookie) là hợp lệ — logout luôn idempotent (#101).</summary>
public sealed record LogoutCommand(string? RefreshToken) : IRequest;
