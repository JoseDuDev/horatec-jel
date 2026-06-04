using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Auth.Commands.LoginWithApple;

/// <param name="IdentityToken">Token retornado pelo Sign in with Apple SDK no cliente.</param>
/// <param name="TenantSlug">Slug do tenant para criar conta como Customer; null para PlatformAdmin.</param>
public sealed record LoginWithAppleCommand(
    string IdentityToken,
    string? TenantSlug) : IRequest<Result<TokenPair>>;

internal sealed class LoginWithAppleCommandHandler(
    IAppleOAuthService appleOAuth,
    IUserRepository userRepository,
    ITenantRepository tenantRepository,
    ITokenService tokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<LoginWithAppleCommand, Result<TokenPair>>
{
    public async Task<Result<TokenPair>> Handle(
        LoginWithAppleCommand request,
        CancellationToken cancellationToken)
    {
        var info = await appleOAuth.ValidateAsync(request.IdentityToken, cancellationToken);
        if (info is null)
            return Result.Failure<TokenPair>(AuthErrors.InvalidOAuthToken);

        Guid? tenantId = null;
        if (!string.IsNullOrWhiteSpace(request.TenantSlug))
        {
            var tenant = await tenantRepository.GetBySlugAsync(request.TenantSlug, cancellationToken);
            if (tenant is null)
                return Result.Failure<TokenPair>(AuthErrors.TenantNotFound);
            tenantId = tenant.Id;
        }

        var user =
            await userRepository.GetByAppleIdAsync(info.ProviderId, cancellationToken)
            ?? await userRepository.GetByEmailAsync(info.Email, cancellationToken);

        if (user is null)
        {
            user = User.CreateWithApple(
                info.Email, info.ProviderId, info.Name,
                tenantId, tenantId.HasValue ? UserRole.Customer : UserRole.PlatformAdmin);

            userRepository.Add(user);
        }
        else
        {
            if (string.IsNullOrEmpty(user.AppleId))
                user.LinkApple(info.ProviderId);
        }

        user.RecordLogin();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(tokenService.GenerateTokens(user));
    }
}
