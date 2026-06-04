using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class BookingRepository(TenantDbContext context)
    : BaseRepository<Booking, TenantDbContext>(context), IBookingRepository
{
    public async Task<IReadOnlyList<Booking>> GetByProfessionalAsync(
        Guid professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(b => b.ProfessionalId == professionalId
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
        Guid professionalId,
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? excludeBookingId = null,
        CancellationToken cancellationToken = default) =>
        await DbSet.AnyAsync(b =>
            b.ProfessionalId == professionalId
            && b.Status != BookingStatus.Cancelled
            && b.Status != BookingStatus.NoShow
            && b.ScheduledAt < end
            && b.EndsAt > start
            && (excludeBookingId == null || b.Id != excludeBookingId),
            cancellationToken);
}
