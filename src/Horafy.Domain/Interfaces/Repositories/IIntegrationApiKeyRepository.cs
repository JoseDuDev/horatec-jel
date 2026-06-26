using Horafy.Domain.Entities.Integrations;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IIntegrationApiKeyRepository : IRepository<IntegrationApiKey>
{
    /// <summary>Busca rastreada (tracked) pelo prefixo público — usada no token-exchange.</summary>
    Task<IntegrationApiKey?> GetByPrefixAsync(string keyPrefix, CancellationToken ct = default);

    /// <summary>Busca rastreada (tracked) por id — usada para revogar.</summary>
    Task<IntegrationApiKey?> GetTrackedByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lista as chaves de um tenant (somente leitura), mais recentes primeiro.</summary>
    Task<IReadOnlyList<IntegrationApiKey>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
}
