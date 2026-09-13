using MediatR;

namespace Identity.Application.Auth.Commands.VerifyEmail;

public sealed record VerifyEmailCommand(string Token) : IRequest<VerifyEmailResult>;

public sealed record VerifyEmailResult(Guid UserId, bool ShopMembershipCreated);
