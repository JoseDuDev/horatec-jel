namespace Horafy.Application.Features.Notifications.Messages;

public sealed record RentalOverdueMessage(
    Guid   BookingId,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    string ItemName,
    DateTimeOffset DueAt,
    int    DaysOverdue,
    string TenantSlug,
    string TenantName);
