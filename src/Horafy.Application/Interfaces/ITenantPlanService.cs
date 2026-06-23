using Horafy.Domain.Entities.Tenants;

namespace Horafy.Application.Interfaces;

/// <summary>
/// Fornece o tenant atual (schema public) para checagem de capacidades e limites de plano
/// nos comandos de cadastro. Retorna null quando não há tenant resolvido na requisição.
/// </summary>
public interface ITenantPlanService
{
    Task<Tenant?> GetCurrentTenantAsync(CancellationToken cancellationToken = default);
}
