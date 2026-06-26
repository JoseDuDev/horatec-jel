using Horafy.Domain.Entities.Integrations;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

/// <summary>
/// Repositório global (schema public) para chaves de API de integração.
/// </summary>
internal sealed class IntegrationApiKeyRepository(HorafyDbContext context)
    : BaseRepository<IntegrationApiKey, HorafyDbContext>(context), IIntegrationApiKeyRepository
{
    // Tracked: o handler de token-exchange chama MarkUsed() e persiste.
    public async Task<IntegrationApiKey?> GetByPrefixAsync(
        string keyPrefix, CancellationToken ct = default) =>
        await DbSet.FirstOrDefaultAsync(k => k.KeyPrefix == keyPrefix, ct);

    // Tracked: o handler de revogação chama Revoke() e persiste.
    public async Task<IntegrationApiKey?> GetTrackedByIdAsync(
        Guid id, CancellationToken ct = default) =>
        await DbSet.FirstOrDefaultAsync(k => k.Id == id, ct);

    public async Task<IReadOnlyList<IntegrationApiKey>> GetByTenantAsync(
        Guid tenantId, CancellationToken ct = default) =>
        await DbSet.AsNoTracking()
            .Where(k => k.TenantId == tenantId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);
}
