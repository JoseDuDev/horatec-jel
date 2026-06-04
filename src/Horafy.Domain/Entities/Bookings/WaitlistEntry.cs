using Horafy.Domain.Entities.Base;
using Horafy.Domain.Events.Bookings;

namespace Horafy.Domain.Entities.Bookings;

public sealed class WaitlistEntry : BaseEntity
{
    private WaitlistEntry() { }

    public Guid   ServiceId     { get; private set; }
    public Guid   ResourceId    { get; private set; }
    public Guid   CustomerId    { get; private set; }
    public string CustomerName  { get; private set; } = default!;
    public string CustomerEmail { get; private set; } = default!;
    public DateOnly       PreferredDate { get; private set; }
    public WaitlistStatus Status        { get; private set; } = WaitlistStatus.Waiting;

    public static WaitlistEntry Create(
        Guid serviceId,
        Guid resourceId,
        Guid customerId,
        string customerName,
        string customerEmail,
        DateOnly preferredDate) =>
        new()
        {
            ServiceId     = serviceId,
            ResourceId    = resourceId,
            CustomerId    = customerId,
            CustomerName  = customerName.Trim(),
            CustomerEmail = customerEmail.ToLowerInvariant().Trim(),
            PreferredDate = preferredDate,
            Status        = WaitlistStatus.Waiting
        };

    public void Promote()
    {
        if (Status != WaitlistStatus.Waiting)
            throw new InvalidOperationException($"Não é possível promover uma entrada no status {Status}.");

        Status    = WaitlistStatus.Notified;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new WaitlistPromotedEvent(Id, CustomerId, ServiceId, ResourceId, PreferredDate));
    }

    public void Cancel()
    {
        Status    = WaitlistStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
