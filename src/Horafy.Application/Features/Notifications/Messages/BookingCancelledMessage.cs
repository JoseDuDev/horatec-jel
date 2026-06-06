namespace Horafy.Application.Features.Notifications.Messages;

public sealed record BookingCancelledMessage(
    Guid   BookingId,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    string? Reason,
    string TenantSlug,
    string TenantName);
