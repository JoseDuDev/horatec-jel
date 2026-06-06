using Horafy.Domain.Entities.Notifications;

namespace Horafy.Domain.Interfaces.Repositories;

public interface INotificationTemplateRepository : IRepository<NotificationTemplate>
{
    Task<NotificationTemplate?> GetActiveAsync(
        NotificationEventType eventType,
        NotificationChannel channel,
        CancellationToken ct = default);

    Task<IReadOnlyList<NotificationTemplate>> GetAllActiveAsync(
        CancellationToken ct = default);
}
