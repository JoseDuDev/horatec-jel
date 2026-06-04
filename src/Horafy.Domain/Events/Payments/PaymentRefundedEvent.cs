using Horafy.Domain.Events.Base;
namespace Horafy.Domain.Events.Payments;
public sealed record PaymentRefundedEvent(Guid PaymentId, Guid BookingId) : DomainEvent;
