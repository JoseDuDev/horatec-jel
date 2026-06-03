using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

public sealed class TenantRepository(HorafyDbContext context)
    : BaseRepository<Tenant>(context), ITenantRepository
{
    public async Task<Tenant?> GetBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == slug.ToLowerInvariant(), cancellationToken);

    public async Task<Tenant?> GetByCustomDomainAsync(
        string domain,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.CustomDomain == domain.ToLowerInvariant(), cancellationToken);

    public async Task<bool> SlugExistsAsync(
        string slug,
        CancellationToken cancellationToken = default) =>
        await DbSet.AnyAsync(t => t.Slug == slug.ToLowerInvariant(), cancellationToken);
}
