using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Events.Base;

namespace Horafy.Domain.Events.Payments;

public sealed record PaymentCreatedEvent(
    Guid PaymentId, Guid BookingId, decimal Amount, PaymentMethod Method) : DomainEvent;
