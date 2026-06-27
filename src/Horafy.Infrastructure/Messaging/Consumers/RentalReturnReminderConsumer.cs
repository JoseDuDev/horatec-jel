using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Notifications;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class RentalReturnReminderConsumer(
    IWhatsAppService   whatsAppService,
    IEmailService      emailService,
    INotificationLogger logger) : IConsumer<RentalReturnReminderMessage>
{
    public async Task Consume(ConsumeContext<RentalReturnReminderMessage> context)
    {
        var msg  = context.Message;
        var ct   = context.CancellationToken;
        var vars = new Dictionary<string, string>
        {
            ["customer_name"] = msg.CustomerName,
            ["item_name"]     = msg.ItemName,
            ["due_at"]        = TemplateRenderer.FormatBrazilian(msg.DueAt),
            ["tenant_name"]   = msg.TenantName,
        };

        if (!string.IsNullOrEmpty(msg.CustomerPhone))
        {
            var text = TemplateRenderer.Render(DefaultTemplates.WhatsApp.RentalReturnReminder, vars);
            await logger.SendAndLogAsync(
                () => whatsAppService.SendTextAsync(msg.CustomerPhone, text, ct),
                NotificationEventType.RentalReturnReminder, NotificationChannel.WhatsApp,
                msg.CustomerPhone, msg.TenantSlug, ct);
        }

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.RentalReturnReminder, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.RentalReturnReminder, vars);
        await logger.SendAndLogAsync(
            () => emailService.SendAsync(msg.CustomerEmail, subject, body, ct),
            NotificationEventType.RentalReturnReminder, NotificationChannel.Email,
            msg.CustomerEmail, msg.TenantSlug, ct);
    }
}
