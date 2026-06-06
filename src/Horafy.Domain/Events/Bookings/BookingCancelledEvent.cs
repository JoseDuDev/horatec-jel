using Horafy.Domain.Events.Base;

namespace Horafy.Domain.Events.Bookings;

public sealed record BookingCancelledEvent(
    Guid BookingId,
    Guid CustomerId,
    string? CustomerPhone,
    string? Reason) : DomainEvent;
