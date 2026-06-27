using Horafy.Domain.Entities.Notifications;
using Horafy.Infrastructure.Persistence;

namespace Horafy.Infrastructure.Messaging;

public interface INotificationLogger
{
    Task SendAndLogAsync(
        Func<Task> send,
        NotificationEventType eventType,
        NotificationChannel channel,
        string recipient,
        string tenantSlug,
        CancellationToken ct);
}

internal sealed class NotificationLogger(HorafyDbContext db) : INotificationLogger
{
    public async Task SendAndLogAsync(
        Func<Task> send,
        NotificationEventType eventType,
        NotificationChannel channel,
        string recipient,
        string tenantSlug,
        CancellationToken ct)
    {
        string? error = null;
        try
        {
            await send();
        }
        catch (Exception ex)
        {
            error = ex.Message;
            throw;
        }
        finally
        {
            try
            {
                db.NotificationLogs.Add(
                    NotificationLog.Record(tenantSlug, eventType, channel, recipient, error is null, error));
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch { /* swallow — logging errors must not mask the original exception */ }
        }
    }
}
