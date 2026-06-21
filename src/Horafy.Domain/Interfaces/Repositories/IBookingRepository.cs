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

    Task<IReadOnlyList<Booking>> GetByRecurrenceGroupAsync(
        Guid recurrenceGroupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soma as unidades de um item de locação reservadas por reservas ativas
    /// (Kind = Rental) que se sobrepõem à janela [start, end], considerando um
    /// buffer de <paramref name="bufferDays"/> dias de bloqueio após a devolução.
    /// </summary>
    Task<int> CountReservedUnitsAsync(
        Guid rentableItemId,
        DateTimeOffset start,
        DateTimeOffset end,
        int bufferDays = 0,
        Guid? excludeBookingId = null,
        CancellationToken cancellationToken = default);
}
