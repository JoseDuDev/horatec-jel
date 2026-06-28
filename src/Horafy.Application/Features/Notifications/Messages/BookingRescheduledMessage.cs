namespace Horafy.Application.Features.Notifications.Messages;

public sealed record BookingRescheduledMessage(
    Guid           BookingId,
    string         CustomerName,
    string         CustomerEmail,
    string?        CustomerPhone,
    string         ServiceName,
    string         ResourceName,
    DateTimeOffset NewScheduledAt,
    string         TenantSlug,
    string         TenantName);
