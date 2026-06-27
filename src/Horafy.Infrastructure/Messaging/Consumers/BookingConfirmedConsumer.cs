using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Notifications;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class BookingConfirmedConsumer(
    IWhatsAppService   whatsAppService,
    IEmailService      emailService,
    INotificationLogger logger) : IConsumer<BookingConfirmedMessage>
{
    public async Task Consume(ConsumeContext<BookingConfirmedMessage> context)
    {
        var msg  = context.Message;
        var ct   = context.CancellationToken;
        var vars = new Dictionary<string, string>
        {
            ["customer_name"] = msg.CustomerName,
            ["service_name"]  = msg.ServiceName,
            ["resource_name"] = msg.ResourceName,
            ["scheduled_at"]  = TemplateRenderer.FormatBrazilian(msg.ScheduledAt),
            ["tenant_name"]   = msg.TenantName
        };

        if (!string.IsNullOrEmpty(msg.CustomerPhone))
        {
            var text = TemplateRenderer.Render(DefaultTemplates.WhatsApp.BookingConfirmed, vars);
            await logger.SendAndLogAsync(
                () => whatsAppService.SendTextAsync(msg.CustomerPhone, text, ct),
                NotificationEventType.BookingConfirmed, NotificationChannel.WhatsApp,
                msg.CustomerPhone, msg.TenantSlug, ct);
        }

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.BookingConfirmed, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.BookingConfirmed, vars);
        await logger.SendAndLogAsync(
            () => emailService.SendAsync(msg.CustomerEmail, subject, body, ct),
            NotificationEventType.BookingConfirmed, NotificationChannel.Email,
            msg.CustomerEmail, msg.TenantSlug, ct);
    }
}
