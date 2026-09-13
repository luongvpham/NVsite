using MediatR;

namespace Vsite.Application.Identity.Auth.Queries.GetMe;

/// <summary>Quyết định #32 — "đọc hồ sơ của chính mình ✅" cho MỌI audience, kể cả `shop:{shopId}`.</summary>
public sealed record GetMeQuery : IRequest<MeDto>;

public sealed record MeDto(Guid UserId, string? Email, string? FullName, string? AvatarUrl, string? Phone, string Audience);
