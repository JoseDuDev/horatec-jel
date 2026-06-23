using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Commands.UpdateTenantPlan;

/// <summary>
/// Define o pacote contratado de um tenant (ação da plataforma/Super Admin):
/// os módulos (capacidades) e o plano (limites).
/// </summary>
public sealed record UpdateTenantPlanCommand(
    Guid TenantId,
    TenantCapability Capabilities,
    TenantPlan Plan) : IRequest<Result>;

internal sealed class UpdateTenantPlanCommandHandler(
    ITenantRepository tenantRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateTenantPlanCommand, Result>
{
    public async Task<Result> Handle(UpdateTenantPlanCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
            return Result.Failure(TenantErrors.NotFound);

        tenant.SetCapabilities(request.Capabilities);
        tenant.SetPlan(request.Plan);

        tenantRepository.Update(tenant);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
