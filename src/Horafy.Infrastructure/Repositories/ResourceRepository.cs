using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class ResourceRepository(TenantDbContext context)
    : BaseRepository<Resource, TenantDbContext>(context), IResourceRepository
{
    public async Task<IReadOnlyList<Resource>> GetActiveAsync(
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Resource>> GetByTypeAsync(
        ResourceType type,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(r => r.Type == type && r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
}
