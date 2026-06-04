using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Commands.SuspendTenant;

public sealed record SuspendTenantCommand(Guid TenantId, string Reason) : IRequest<Result>;

internal sealed class SuspendTenantCommandHandler(
    ITenantRepository tenantRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<SuspendTenantCommand, Result>
{
    public async Task<Result> Handle(
        SuspendTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null) return Result.Failure(TenantErrors.NotFound);

        tenant.Suspend(request.Reason);
        tenantRepository.Update(tenant);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
