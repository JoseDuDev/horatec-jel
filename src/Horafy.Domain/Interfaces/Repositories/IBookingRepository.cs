using Horafy.Domain.Entities.Bookings;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IBookingRepository : IRepository<Booking>
{
    Task<IReadOnlyList<Booking>> GetByProfessionalAsync(
        Guid professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<bool> HasConflictAsync(
        Guid professionalId,
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? excludeBookingId = null,
        CancellationToken cancellationToken = default);
}
