using Horafy.Domain.Entities.Integrations;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IIntegrationWebhookRepository : IRepository<IntegrationWebhook>
{
    /// <summary>Endpoint do tenant (rastreado — usado em upsert e leitura do consumer).</summary>
    Task<IntegrationWebhook?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
}
