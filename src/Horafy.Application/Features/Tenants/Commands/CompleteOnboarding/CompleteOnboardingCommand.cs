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
    public async Task<Result> Handle(CompleteOnboardingCommand request, CancellationToken ct)
    {
        if (!currentTenant.TenantId.HasValue)
            return Result.Failure(Error.Unauthorized);

        var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, ct);
        if (tenant is null) return Result.Failure(TenantErrors.NotFound);

        tenant.CompleteOnboarding();
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
