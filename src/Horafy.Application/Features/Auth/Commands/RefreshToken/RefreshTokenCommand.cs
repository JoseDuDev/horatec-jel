using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<Result<TokenPair>>;

internal sealed class RefreshTokenCommandHandler(
    ITokenService tokenService,
    IUserRepository userRepository) : IRequestHandler<RefreshTokenCommand, Result<TokenPair>>
{
    public async Task<Result<TokenPair>> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        var principal = tokenService.ValidateRefreshToken(request.RefreshToken);
        if (principal is null)
            return Result.Failure<TokenPair>(AuthErrors.InvalidRefreshToken);

        var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Result.Failure<TokenPair>(AuthErrors.InvalidRefreshToken);

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.IsDeleted)
            return Result.Failure<TokenPair>(AuthErrors.UserNotFound);

        return Result.Success(tokenService.GenerateTokens(user));
    }
}
