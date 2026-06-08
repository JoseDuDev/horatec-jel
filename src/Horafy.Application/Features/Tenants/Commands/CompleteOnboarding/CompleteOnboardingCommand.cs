using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Commands.CompleteOnboarding;

public sealed record CompleteOnboardingCommand : IRequest<Result>;

internal sealed class CompleteOnboardingCommandHandler(
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenant,
    IUnitOfWork           unitOfWork) : IRequestHandler<CompleteOnboardingCommand, Result>
{
    public async Task<Result> Handle(CompleteOnboardingCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.TenantId.HasValue)
            return Result.Failure(Error.Unauthorized);

        var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
        if (tenant is null) return Result.Failure(TenantErrors.NotFound);

        tenant.CompleteOnboarding();
        tenantRepository.Update(tenant);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
