using MediatR;
using Microsoft.EntityFrameworkCore;
using Vsite.Application.Common.Interfaces;
using Vsite.Domain.Exceptions;

namespace Vsite.Application.Identity.Auth.Queries.GetMe;

public sealed class GetMeHandler(IAppDbContext db, ICurrentUserContext currentUser) : IRequestHandler<GetMeQuery, MeDto>
{
    public async Task<MeDto> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("User", currentUser.UserId);

        return new MeDto(user.Id, user.Email, user.FullName, user.AvatarUrl, user.Phone, currentUser.Audience);
    }
}
