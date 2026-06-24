using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;

namespace Horafy.Infrastructure.MultiTenancy;

/// <summary>
/// Limites efetivos do plano: usa a linha persistida em plan_configurations se existir;
/// caso contrário, o default de <see cref="PlanLimits.For"/>.
/// </summary>
internal sealed class PlanLimitsService(IPlanConfigurationRepository repository) : IPlanLimitsService
{
    public async Task<PlanLimits> GetLimitsAsync(TenantPlan plan, CancellationToken cancellationToken = default)
    {
        var config = await repository.GetByPlanAsync(plan, cancellationToken);
        return config?.ToLimits() ?? PlanLimits.For(plan);
    }
}
