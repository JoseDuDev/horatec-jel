using Horafy.Domain.Entities.Services;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class ServiceRepository(TenantDbContext context)
    : BaseRepository<Service, TenantDbContext>(context), IServiceRepository
{
    public async Task<IReadOnlyList<Service>> GetActiveAsync(
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

    public async Task<bool> ExistsByNameAsync(
        string name, CancellationToken cancellationToken = default) =>
        await DbSet.AnyAsync(s => s.Name == name.Trim(), cancellationToken);

    public async Task<IReadOnlyList<Service>> GetByIdsAsync(
        IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await DbSet
            .AsNoTracking()
            .Where(s => idList.Contains(s.Id))
            .ToListAsync(cancellationToken);
    }
}
