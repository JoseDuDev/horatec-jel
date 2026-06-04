using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class BookingRepository(TenantDbContext context)
    : BaseRepository<Booking, TenantDbContext>(context), IBookingRepository
{
    public async Task<IReadOnlyList<Booking>> GetByResourceAsync(
        Guid resourceId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(b => b.ResourceId == resourceId
                     && b.ScheduledAt >= from
                     && b.ScheduledAt < to)
            .OrderBy(b => b.ScheduledAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(b => b.CustomerId == customerId)
            .OrderByDescending(b => b.ScheduledAt)
            .ToListAsync(cancellationToken);

    public async Task<bool> HasConflictAsync(
        Guid resourceId,
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? excludeBookingId = null,
        CancellationToken cancellationToken = default) =>
        await DbSet.AnyAsync(b =>
            b.ResourceId == resourceId
            && b.Status != BookingStatus.Cancelled
            && b.Status != BookingStatus.NoShow
            && b.ScheduledAt < end
            && b.EndsAt > start
            && (excludeBookingId == null || b.Id != excludeBookingId),
            cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetByRecurrenceGroupAsync(
        Guid recurrenceGroupId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(b => b.RecurrenceGroupId == recurrenceGroupId)
            .OrderBy(b => b.ScheduledAt)
            .ToListAsync(cancellationToken);
}
