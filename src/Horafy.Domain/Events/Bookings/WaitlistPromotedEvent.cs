using Horafy.Domain.Events.Base;

namespace Horafy.Domain.Events.Bookings;

public sealed record WaitlistPromotedEvent(
    Guid WaitlistEntryId,
    Guid CustomerId,
    Guid ServiceId,
    Guid ResourceId,
    DateOnly PreferredDate) : DomainEvent;
