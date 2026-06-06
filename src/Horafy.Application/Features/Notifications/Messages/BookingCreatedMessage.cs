namespace Horafy.Application.Features.Notifications.Messages;

public sealed record BookingCreatedMessage(
    Guid   BookingId,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    string ServiceName,
    string ResourceName,
    DateTimeOffset ScheduledAt,
    string TenantSlug,
    string TenantName);
