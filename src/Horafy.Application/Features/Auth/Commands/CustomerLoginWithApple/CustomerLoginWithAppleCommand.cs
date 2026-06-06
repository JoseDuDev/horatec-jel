using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Auth.Commands.CustomerLoginWithApple;

public sealed record CustomerLoginWithAppleCommand(
    string  IdentityToken,
    string? TenantSlug) : IRequest<Result<TokenPair>>;

internal sealed class CustomerLoginWithAppleCommandHandler(
    IAppleOAuthService   appleOAuth,
    IUserRepository      userRepository,
    ITenantRepository    tenantRepository,
    ITokenService        tokenService,
    IUnitOfWork          unitOfWork) : IRequestHandler<CustomerLoginWithAppleCommand, Result<TokenPair>>
{
    public async Task<Result<TokenPair>> Handle(
        CustomerLoginWithAppleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantSlug))
            return Result.Failure<TokenPair>(new Error(
                "Customer.TenantRequired",
                "O slug do tenant é obrigatório para login de clientes.",
                ErrorType.Validation));

        var info = await appleOAuth.ValidateAsync(request.IdentityToken, cancellationToken);
        if (info is null)
            return Result.Failure<TokenPair>(AuthErrors.InvalidOAuthToken);

        var tenant = await tenantRepository.GetBySlugAsync(request.TenantSlug, cancellationToken);
        if (tenant is null)
            return Result.Failure<TokenPair>(AuthErrors.TenantNotFound);

        var user =
            await userRepository.GetByAppleIdAsync(info.ProviderId, cancellationToken)
            ?? await userRepository.GetByEmailAsync(info.Email, cancellationToken);

        if (user is null)
        {
            user = User.CreateWithApple(
                info.Email, info.ProviderId, info.Name,
                tenant.Id, UserRole.Customer);
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
