using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Auth.Commands.LoginWithGoogle;

/// <param name="IdToken">Token retornado pelo Google Sign-In SDK no cliente.</param>
/// <param name="TenantSlug">Slug do tenant para criar conta como Customer; null para PlatformAdmin.</param>
public sealed record LoginWithGoogleCommand(
    string IdToken,
    string? TenantSlug) : IRequest<Result<TokenPair>>;

internal sealed class LoginWithGoogleCommandHandler(
    IGoogleOAuthService googleOAuth,
    IUserRepository userRepository,
    ITenantRepository tenantRepository,
    ITokenService tokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<LoginWithGoogleCommand, Result<TokenPair>>
{
    public async Task<Result<TokenPair>> Handle(
        LoginWithGoogleCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Valida o ID token com a Google
        var info = await googleOAuth.ValidateAsync(request.IdToken, cancellationToken);
        if (info is null)
            return Result.Failure<TokenPair>(AuthErrors.InvalidOAuthToken);

        // 2. Resolve tenant (quando informado)
        Guid? tenantId = null;
        if (!string.IsNullOrWhiteSpace(request.TenantSlug))
        {
            var tenant = await tenantRepository.GetBySlugAsync(request.TenantSlug, cancellationToken);
            if (tenant is null)
                return Result.Failure<TokenPair>(AuthErrors.TenantNotFound);
            tenantId = tenant.Id;
        }

        // 3. Busca ou cria o usuário
        var user =
            await userRepository.GetByGoogleIdAsync(info.ProviderId, cancellationToken)
            ?? await userRepository.GetByEmailAsync(info.Email, cancellationToken);

        if (user is null)
        {
            user = User.CreateWithGoogle(
                info.Email, info.ProviderId, info.Name, info.AvatarUrl,
                tenantId, tenantId.HasValue ? UserRole.Customer : UserRole.PlatformAdmin);

            userRepository.Add(user);
        }
        else
        {
            // Vincula Google se ainda não estava linkado
            if (string.IsNullOrEmpty(user.GoogleId))
                user.LinkGoogle(info.ProviderId);
        }

        user.RecordLogin();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(tokenService.GenerateTokens(user));
    }
}
