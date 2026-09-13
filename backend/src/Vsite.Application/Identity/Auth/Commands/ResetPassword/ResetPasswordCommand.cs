using MediatR;

namespace Vsite.Application.Identity.Auth.Commands.ResetPassword;

public sealed record ResetPasswordCommand(string Token, string NewPassword) : IRequest;
