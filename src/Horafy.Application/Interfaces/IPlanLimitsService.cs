using Horafy.Domain.Entities.Tenants;

namespace Horafy.Application.Interfaces;

/// <summary>
/// Resolve os limites efetivos de um plano: a configuração persistida (editável pela
/// plataforma) sobrepõe os defaults de <see cref="PlanLimits.For"/>.
/// </summary>
public interface IPlanLimitsService
{
    Task<PlanLimits> GetLimitsAsync(TenantPlan plan, CancellationToken cancellationToken = default);
}
