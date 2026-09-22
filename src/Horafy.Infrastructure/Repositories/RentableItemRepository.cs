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
            .Include(i => i.Images)
            .Where(i => i.IsActive)
            .OrderBy(i => i.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RentableItem>> GetByIdsAsync(
        IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await DbSet
            .AsNoTracking()
            .Include(i => i.Images)
            .Where(i => idList.Contains(i.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<RentableItem?> GetByIdWithImagesAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(i => i.Images)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public override async Task<IReadOnlyList<RentableItem>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Include(i => i.Images)
            .OrderBy(i => i.Name)
            .ToListAsync(cancellationToken);
}
