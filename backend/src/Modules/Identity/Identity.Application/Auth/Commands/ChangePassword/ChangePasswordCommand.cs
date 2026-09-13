using MediatR;

namespace Identity.Application.Auth.Commands.ChangePassword;

/// <summary>
/// Quyết định #32 — "đổi password của chính shop đó ✅" (không phải password global) khi gọi từ
/// token `shop:{shopId}`; ngược lại đổi `User.PasswordHash` cho `vsite-main`/`vsite-portal`. Scope
/// đến từ `ICurrentUserContext.Audience` (JWT đã xác thực), không phải input của client.
/// </summary>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest;
