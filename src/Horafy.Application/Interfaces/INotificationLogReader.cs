using Horafy.Domain.Entities.Notifications;

namespace Horafy.Application.Interfaces;

public sealed record NotificationLogResult(
    Guid Id,
    NotificationEventType EventType,
    NotificationChannel Channel,
    string Recipient,
    bool Success,
    string? ErrorMessage,
    DateTimeOffset SentAt);

public interface INotificationLogReader
{
    Task<(IReadOnlyList<NotificationLogResult> Items, int TotalCount)> GetPagedAsync(
        string tenantSlug,
        DateTimeOffset? from,
        DateTimeOffset? to,
        bool? success,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
