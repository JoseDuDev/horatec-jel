using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Auth.Commands.CustomerTestLogin;

/// <summary>Cria ou recupera um cliente de teste sem OAuth. Só deve ser exposto em E2ETest.</summary>
public sealed record CustomerTestLoginCommand(string Email, string TenantSlug) : IRequest<Result<TokenPair>>;

internal sealed class CustomerTestLoginCommandHandler(
    IUserRepository userRepository,
    ITenantRepository tenantRepository,
    ITokenService tokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<CustomerTestLoginCommand, Result<TokenPair>>
{
    public async Task<Result<TokenPair>> Handle(
        CustomerTestLoginCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetBySlugAsync(request.TenantSlug, cancellationToken);
        if (tenant is null)
            return Result.Failure<TokenPair>(AuthErrors.TenantNotFound);

        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);

        // If user belongs to a different tenant, create a fresh one for this tenant
        if (user is not null && user.TenantId != tenant.Id)
            user = null;

        if (user is null)
        {
            user = User.CreateWithEmail(
                request.Email,
                passwordHash: "e2e-placeholder",
                name: request.Email.Split('@')[0],
                tenantId: tenant.Id,
                role: UserRole.Customer);

            userRepository.Add(user);
        }

        user.RecordLogin();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(tokenService.GenerateTokens(user));
    }
}
