using Horafy.Domain.Events.Base;

namespace Horafy.Domain.Events.Bookings;

public sealed record BookingRescheduledEvent(
    Guid            BookingId,
    Guid            CustomerId,
    string?         CustomerPhone,
    DateTimeOffset  NewScheduledAt) : DomainEvent;
