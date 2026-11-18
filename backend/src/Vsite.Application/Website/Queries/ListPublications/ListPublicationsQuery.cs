using MediatR;
using Vsite.Application.Website.Dtos;

namespace Vsite.Application.Website.Queries.ListPublications;

/// <summary>Các bản đã publish của shop hiện tại (<c>ITenantContext</c>), version giảm dần, tối đa
/// <see cref="ListPublicationsHandler.MaxItems"/>. Không kèm snapshot.</summary>
public sealed record ListPublicationsQuery : IRequest<IReadOnlyList<PublicationDto>>;
