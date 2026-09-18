using Horafy.Domain.Entities.Notifications;
using Horafy.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Horafy.Infrastructure.Messaging;

public interface INotificationLogger
{
    /// <summary>
    /// Envia, registra o resultado e <b>repropaga</b> a falha — a mensagem volta para
    /// a fila e o MassTransit tenta de novo. Para o último canal do consumer.
    /// </summary>
    Task SendAndLogAsync(
        Func<Task> send,
        NotificationEventType eventType,
        NotificationChannel channel,
        string recipient,
        string tenantSlug,
        CancellationToken ct);

    /// <summary>
    /// Igual, mas <b>engole</b> a falha depois de registrá-la. Para canais que não podem
    /// impedir os seguintes: o WhatsApp é enviado antes do e-mail em todo consumer, e com
    /// a repropagação uma instância errada na Evolution deixava o cliente sem o e-mail
    /// também — a mensagem inteira ia para a fila de erro antes de chegar lá.
    /// A falha não some: fica no <c>NotificationLog</c> com a mensagem do erro, e sai em
    /// warning no log da aplicação.
    /// </summary>
    Task TrySendAndLogAsync(
        Func<Task> send,
        NotificationEventType eventType,
        NotificationChannel channel,
        string recipient,
        string tenantSlug,
        CancellationToken ct);
}

internal sealed class NotificationLogger(
    HorafyDbContext db,
    ILogger<NotificationLogger> logger) : INotificationLogger
{
    public Task SendAndLogAsync(
        Func<Task> send, NotificationEventType eventType, NotificationChannel channel,
        string recipient, string tenantSlug, CancellationToken ct) =>
        RunAsync(send, eventType, channel, recipient, tenantSlug, rethrow: true);

    public Task TrySendAndLogAsync(
        Func<Task> send, NotificationEventType eventType, NotificationChannel channel,
        string recipient, string tenantSlug, CancellationToken ct) =>
        RunAsync(send, eventType, channel, recipient, tenantSlug, rethrow: false);

    private async Task RunAsync(
        Func<Task> send,
        NotificationEventType eventType,
        NotificationChannel channel,
        string recipient,
        string tenantSlug,
        bool rethrow)
    {
        string? error = null;
        try
        {
            await send();
        }
        catch (Exception ex)
        {
            error = ex.Message;
            if (rethrow) throw;

            logger.LogWarning(
                ex,
                "Notificação {Channel} de {EventType} falhou para {Recipient} (tenant {TenantSlug}) — "
                + "seguindo para os próximos canais.",
                channel, eventType, recipient, tenantSlug);
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
