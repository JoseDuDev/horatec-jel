using Horafy.Domain.Events.Base;

namespace Horafy.Domain.Events.Bookings;

public sealed record BookingCreatedEvent(
    Guid BookingId,
    Guid ServiceId,
    Guid ResourceId,
    Guid CustomerId,
    string? CustomerPhone,
    DateTimeOffset ScheduledAt) : DomainEvent;
