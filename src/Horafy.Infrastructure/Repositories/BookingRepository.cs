using System.Linq.Expressions;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class BookingRepository(TenantDbContext context)
    : BaseRepository<Booking, TenantDbContext>(context), IBookingRepository
{
    // Override to eager-load Services so query handlers never see empty lists.
    public override async Task<Booking?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Include(b => b.Services)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetByResourceAsync(
        Guid resourceId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Include(b => b.Services)
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
            .Include(b => b.Services)
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

    // Override FindAsync to eager-load Services (used by GetBookingsQuery for the all-resources path).
    public override async Task<IReadOnlyList<Booking>> FindAsync(
        Expression<Func<Booking, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Include(b => b.Services)
            .Where(predicate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetByRecurrenceGroupAsync(
        Guid recurrenceGroupId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Include(b => b.Services)
            .Where(b => b.RecurrenceGroupId == recurrenceGroupId)
            .OrderBy(b => b.ScheduledAt)
            .ToListAsync(cancellationToken);
}
