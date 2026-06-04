using Horafy.Domain.Events.Base;

namespace Horafy.Domain.Events.Payments;

public sealed record PaymentConfirmedEvent(
    Guid PaymentId, Guid BookingId, bool IsDeposit) : DomainEvent;
