using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Auth.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery : IRequest<Result<CurrentUserResult>>;

public sealed record CurrentUserResult(
    Guid Id,
    string Email,
    string? Name,
    string? AvatarUrl,
    UserRole Role,
    Guid? TenantId,
    bool IsEmailVerified,
    IReadOnlyCollection<UserPermission> Permissions,
    DateTimeOffset? LastLoginAt);

internal sealed class GetCurrentUserQueryHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository) : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserResult>>
{
    public async Task<Result<CurrentUserResult>> Handle(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
            return Result.Failure<CurrentUserResult>(Error.Unauthorized);

        var user = await userRepository.GetByIdAsync(currentUser.UserId.Value, cancellationToken);
        if (user is null)
            return Result.Failure<CurrentUserResult>(AuthErrors.UserNotFound);

        return Result.Success(new CurrentUserResult(
            user.Id,
            user.Email,
            user.Name,
            user.AvatarUrl,
            user.Role,
            user.TenantId,
            user.IsEmailVerified,
            user.Permissions,
            user.LastLoginAt));
    }
}
