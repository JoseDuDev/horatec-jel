using Horafy.Domain.Events.Base;

namespace Horafy.Domain.Events.Payments;

public sealed record PaymentFailedEvent(Guid PaymentId, Guid BookingId) : DomainEvent;
