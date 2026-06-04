using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class WaitlistRepository(TenantDbContext context)
    : BaseRepository<WaitlistEntry, TenantDbContext>(context), IWaitlistRepository
{
    public async Task<IReadOnlyList<WaitlistEntry>> GetByServiceResourceDateAsync(
        Guid serviceId, Guid resourceId, DateOnly preferredDate,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(w => w.ServiceId == serviceId
                     && w.ResourceId == resourceId
                     && w.PreferredDate == preferredDate
                     && w.Status == WaitlistStatus.Waiting)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WaitlistEntry>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(w => w.CustomerId == customerId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<bool> ExistsActiveAsync(
        Guid serviceId, Guid resourceId, Guid customerId, DateOnly preferredDate,
        CancellationToken cancellationToken = default) =>
        await DbSet.AnyAsync(w =>
            w.ServiceId == serviceId
            && w.ResourceId == resourceId
            && w.CustomerId == customerId
            && w.PreferredDate == preferredDate
            && w.Status == WaitlistStatus.Waiting,
            cancellationToken);
}
