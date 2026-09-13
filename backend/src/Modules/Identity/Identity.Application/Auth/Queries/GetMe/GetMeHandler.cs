using Identity.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;

namespace Identity.Application.Auth.Queries.GetMe;

public sealed class GetMeHandler(IIdentityDbContext db, ICurrentUserContext currentUser) : IRequestHandler<GetMeQuery, MeDto>
{
    public async Task<MeDto> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("User", currentUser.UserId);

        return new MeDto(user.Id, user.Email, user.FullName, user.AvatarUrl, user.Phone, currentUser.Audience);
    }
}
