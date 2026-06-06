namespace Horafy.Application.Features.Notifications.Messages;

public sealed record PaymentConfirmedMessage(
    Guid    PaymentId,
    Guid    BookingId,
    string  CustomerName,
    string  CustomerEmail,
    string? CustomerPhone,
    decimal Amount,
    string  TenantSlug,
    string  TenantName);
