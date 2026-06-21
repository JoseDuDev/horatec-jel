using Horafy.Domain.Entities.Rentals;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class RentableItemRepository(TenantDbContext context)
    : BaseRepository<RentableItem, TenantDbContext>(context), IRentableItemRepository
{
    public async Task<IReadOnlyList<RentableItem>> GetActiveAsync(
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RentableItem>> GetByIdsAsync(
        IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await DbSet
            .AsNoTracking()
            .Where(i => idList.Contains(i.Id))
            .ToListAsync(cancellationToken);
    }
}
