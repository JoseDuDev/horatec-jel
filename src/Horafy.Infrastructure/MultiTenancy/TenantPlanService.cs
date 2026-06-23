using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;

namespace Horafy.Infrastructure.MultiTenancy;

/// <summary>
/// Resolve o tenant atual a partir do <see cref="ICurrentTenantService"/> (schema public),
/// para que os comandos de cadastro consultem capacidades e limites de plano.
/// </summary>
internal sealed class TenantPlanService(
    ICurrentTenantService currentTenant,
    ITenantRepository tenantRepository) : ITenantPlanService
{
    public async Task<Tenant?> GetCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        if (currentTenant.TenantId is not { } tenantId)
            return null;

        return await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
    }
}
