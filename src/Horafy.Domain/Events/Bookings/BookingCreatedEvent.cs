using Horafy.Domain.Events.Base;

namespace Horafy.Domain.Events.Bookings;

public sealed record BookingCreatedEvent(
    Guid BookingId,
    Guid ServiceId,
    Guid ProfessionalId,
    Guid CustomerId,
    DateTimeOffset ScheduledAt) : DomainEvent;
