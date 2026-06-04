using Horafy.Domain.Entities.Bookings;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IBookingRepository : IRepository<Booking>
{
    Task<IReadOnlyList<Booking>> GetByResourceAsync(
        Guid resourceId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<bool> HasConflictAsync(
        Guid resourceId,
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? excludeBookingId = null,
        CancellationToken cancellationToken = default);
}
