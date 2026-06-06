using Horafy.Domain.Events.Base;

namespace Horafy.Domain.Events.Bookings;

public sealed record BookingConfirmedEvent(
    Guid BookingId,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    DateTimeOffset ScheduledAt) : DomainEvent;
