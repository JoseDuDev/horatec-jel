using Horafy.Domain.Entities.Integrations;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

/// <summary>Repositório global (schema public) para endpoints de webhook por tenant.</summary>
internal sealed class IntegrationWebhookRepository(HorafyDbContext context)
    : BaseRepository<IntegrationWebhook, HorafyDbContext>(context), IIntegrationWebhookRepository
{
    public async Task<IntegrationWebhook?> GetByTenantAsync(
        Guid tenantId, CancellationToken ct = default) =>
        await DbSet.FirstOrDefaultAsync(w => w.TenantId == tenantId, ct);
}
