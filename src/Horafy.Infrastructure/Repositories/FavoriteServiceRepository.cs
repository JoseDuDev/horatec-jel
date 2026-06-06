using Horafy.Domain.Entities.Favorites;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class FavoriteServiceRepository(TenantDbContext context)
    : BaseRepository<FavoriteService, TenantDbContext>(context), IFavoriteServiceRepository
{
    public async Task<FavoriteService?> GetAsync(
        Guid customerId, Guid serviceId, CancellationToken cancellationToken = default) =>
        await DbSet
            .FirstOrDefaultAsync(
                f => f.CustomerId == customerId && f.ServiceId == serviceId,
                cancellationToken);

    public async Task<IReadOnlyList<FavoriteService>> GetByCustomerAsync(
        Guid customerId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(f => f.CustomerId == customerId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
}
