using Identity.Application.Auth.Dtos;
using MediatR;

namespace Identity.Application.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthTokenResult>;
