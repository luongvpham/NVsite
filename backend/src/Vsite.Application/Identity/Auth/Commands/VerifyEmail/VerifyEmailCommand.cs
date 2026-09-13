using MediatR;

namespace Vsite.Application.Identity.Auth.Commands.VerifyEmail;

public sealed record VerifyEmailCommand(string Token) : IRequest<VerifyEmailResult>;

public sealed record VerifyEmailResult(Guid UserId, bool ShopMembershipCreated);
