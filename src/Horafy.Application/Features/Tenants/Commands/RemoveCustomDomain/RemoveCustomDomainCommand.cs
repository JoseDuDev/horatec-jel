using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Commands.RemoveCustomDomain;

public sealed record RemoveCustomDomainCommand : IRequest<Result>;

internal sealed class RemoveCustomDomainCommandHandler(
    ICurrentTenantService currentTenant,
    ITenantRepository tenantRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<RemoveCustomDomainCommand, Result>
{
    public async Task<Result> Handle(
        RemoveCustomDomainCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.TenantId.HasValue)
            return Result.Failure(Error.Unauthorized);

        var tenant = await tenantRepository.GetByIdAsync(
            currentTenant.TenantId.Value, cancellationToken);

        if (tenant is null) return Result.Failure(TenantErrors.NotFound);

        tenant.RemoveCustomDomain();
        tenantRepository.Update(tenant);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
