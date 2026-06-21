namespace Horafy.Application.Features.Notifications.Messages;

public sealed record RentalReturnReminderMessage(
    Guid   BookingId,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    string ItemName,
    DateTimeOffset DueAt,
    string TenantSlug,
    string TenantName);
