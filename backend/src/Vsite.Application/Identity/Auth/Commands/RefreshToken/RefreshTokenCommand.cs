using MediatR;
using Vsite.Application.Identity.Auth.Dtos;

namespace Vsite.Application.Identity.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthTokenResult>;
