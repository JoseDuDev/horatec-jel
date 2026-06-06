namespace Horafy.Application.Features.Notifications.Messages;

public sealed record PaymentPendingMessage(
    Guid    PaymentId,
    Guid    BookingId,
    string  CustomerName,
    string  CustomerEmail,
    string? CustomerPhone,
    string? PaymentUrl,
    decimal Amount,
    string  TenantSlug,
    string  TenantName);
