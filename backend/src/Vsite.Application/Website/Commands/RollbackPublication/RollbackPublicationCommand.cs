using MediatR;
using Vsite.Application.Website.Dtos;

namespace Vsite.Application.Website.Commands.RollbackPublication;

/// <summary>Đưa con trỏ "bản đang chạy" của shop hiện tại (<c>ITenantContext</c>) về một bản cũ. Không mang <c>ShopId</c> (#21.4).</summary>
public sealed record RollbackPublicationCommand(Guid PublicationId) : IRequest<PublicationDto>;
