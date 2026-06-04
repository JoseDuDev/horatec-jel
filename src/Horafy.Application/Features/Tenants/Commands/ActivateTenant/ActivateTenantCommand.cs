using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Commands.ActivateTenant;

public sealed record ActivateTenantCommand(Guid TenantId) : IRequest<Result>;

internal sealed class ActivateTenantCommandHandler(
    ITenantRepository tenantRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ActivateTenantCommand, Result>
{
    public async Task<Result> Handle(
        ActivateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null) return Result.Failure(TenantErrors.NotFound);

        tenant.Activate();
        tenantRepository.Update(tenant);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
