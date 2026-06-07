using Horafy.Domain.Events.Base;

namespace Horafy.Domain.Events.Bookings;

public sealed record BookingCompletedEvent(
    Guid BookingId,
    Guid CustomerId) : DomainEvent;
