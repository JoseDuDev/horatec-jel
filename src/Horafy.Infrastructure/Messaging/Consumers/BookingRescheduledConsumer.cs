using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Notifications;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class BookingRescheduledConsumer(
    IWhatsAppService    whatsAppService,
    IEmailService       emailService,
    INotificationLogger logger) : IConsumer<BookingRescheduledMessage>
{
    public async Task Consume(ConsumeContext<BookingRescheduledMessage> context)
    {
        var msg = context.Message;
        var ct  = context.CancellationToken;

        var vars = new Dictionary<string, string>
        {
            ["customer_name"]    = msg.CustomerName,
            ["service_name"]     = msg.ServiceName,
            ["resource_name"]    = msg.ResourceName,
            ["new_scheduled_at"] = TemplateRenderer.FormatBrazilian(msg.NewScheduledAt),
            ["tenant_name"]      = msg.TenantName
        };

        if (!string.IsNullOrEmpty(msg.CustomerPhone))
        {
            var text = TemplateRenderer.Render(DefaultTemplates.WhatsApp.BookingRescheduled, vars);
            await logger.SendAndLogAsync(
                () => whatsAppService.SendTextAsync(msg.CustomerPhone, text, ct),
                NotificationEventType.BookingConfirmed, NotificationChannel.WhatsApp,
                msg.CustomerPhone, msg.TenantSlug, ct);
        }

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.BookingRescheduled, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.BookingRescheduled, vars);
        await logger.SendAndLogAsync(
            () => emailService.SendAsync(msg.CustomerEmail, subject, body, ct),
            NotificationEventType.BookingConfirmed, NotificationChannel.Email,
            msg.CustomerEmail, msg.TenantSlug, ct);
    }
}
