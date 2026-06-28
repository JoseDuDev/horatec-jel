namespace Horafy.Application.Features.Notifications.Messages;

public sealed record WaitlistSlotAvailableMessage(
    Guid    WaitlistEntryId,
    string  CustomerName,
    string  CustomerEmail,
    string  ServiceName,
    string  ResourceName,
    DateOnly PreferredDate,
    string  TenantSlug,
    string  TenantName);
