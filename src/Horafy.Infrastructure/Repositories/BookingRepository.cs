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

    public async Task<Booking?> GetByExternalIdAsync(
        string externalId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.ExternalId == externalId, cancellationToken);

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

    public async Task<int> CountReservedUnitsAsync(
        Guid rentableItemId,
        DateTimeOffset start,
        DateTimeOffset end,
        int bufferDays = 0,
        Guid? excludeBookingId = null,
        CancellationToken cancellationToken = default)
    {
        // Janela expandida pelo buffer em ambos os lados: garante o intervalo de
        // bloqueio (limpeza/conferência) entre a devolução de uma locação e a
        // retirada da próxima da mesma unidade física.
        var from = start.AddDays(-bufferDays);
        var to   = end.AddDays(bufferDays);

        return await DbSet
            .Where(b => b.Kind == BookingKind.Rental
                     && b.Status != BookingStatus.Cancelled
                     && b.Status != BookingStatus.NoShow
                     && b.RentalStatus != RentalLifecycle.Returned   // devolvido → estoque liberado
                     && b.ScheduledAt < to
                     && b.EndsAt > from
                     && (excludeBookingId == null || b.Id != excludeBookingId))
            .SelectMany(b => b.Services)
            .Where(s => s.RentableItemId == rentableItemId)
            .SumAsync(s => (int?)s.Quantity, cancellationToken) ?? 0;
    }

    // Override FindAsync to eager-load Services (used by GetBookingsQuery for the all-resources path).
    public override async Task<IReadOnlyList<Booking>> FindAsync(
        Expression<Func<Booking, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Include(b => b.Services)
            .Where(predicate)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Booking> Items, int TotalCount)> GetByCustomerPagedAsync(
        Guid customerId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .AsNoTracking()
            .Include(b => b.Services)
            .Where(b => b.CustomerId == customerId)
            .OrderByDescending(b => b.ScheduledAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<(IReadOnlyList<Booking> Items, int TotalCount)> GetPagedAsync(
        Guid? resourceId,
        DateTimeOffset from,
        DateTimeOffset to,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .AsNoTracking()
            .Include(b => b.Services)
            .Where(b => b.ScheduledAt >= from && b.ScheduledAt < to);

        if (resourceId.HasValue)
            query = query.Where(b => b.ResourceId == resourceId.Value);

        query = query.OrderBy(b => b.ScheduledAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

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
