using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Auth.Commands.CustomerLoginWithGoogle;

public sealed record CustomerLoginWithGoogleCommand(
    string  IdToken,
    string? TenantSlug) : IRequest<Result<TokenPair>>;

internal sealed class CustomerLoginWithGoogleCommandHandler(
    IGoogleOAuthService  googleOAuth,
    IUserRepository      userRepository,
    ITenantRepository    tenantRepository,
    ITokenService        tokenService,
    IUnitOfWork          unitOfWork) : IRequestHandler<CustomerLoginWithGoogleCommand, Result<TokenPair>>
{
    public async Task<Result<TokenPair>> Handle(
        CustomerLoginWithGoogleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantSlug))
            return Result.Failure<TokenPair>(new Error(
                "Customer.TenantRequired",
                "O slug do tenant é obrigatório para login de clientes.",
                ErrorType.Validation));

        var info = await googleOAuth.ValidateAsync(request.IdToken, cancellationToken);
        if (info is null)
            return Result.Failure<TokenPair>(AuthErrors.InvalidOAuthToken);

        var tenant = await tenantRepository.GetBySlugAsync(request.TenantSlug, cancellationToken);
        if (tenant is null)
            return Result.Failure<TokenPair>(AuthErrors.TenantNotFound);

        var user =
            await userRepository.GetByGoogleIdAsync(info.ProviderId, cancellationToken)
            ?? await userRepository.GetByEmailAsync(info.Email, cancellationToken);

        if (user is null)
        {
            user = User.CreateWithGoogle(
                info.Email, info.ProviderId, info.Name, info.AvatarUrl,
                tenant.Id, UserRole.Customer);
            userRepository.Add(user);
        }
        else
        {
            if (string.IsNullOrEmpty(user.GoogleId))
                user.LinkGoogle(info.ProviderId);
        }

        user.RecordLogin();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(tokenService.GenerateTokens(user));
    }
}
