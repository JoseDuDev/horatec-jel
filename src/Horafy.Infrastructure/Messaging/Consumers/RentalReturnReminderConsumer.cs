using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class RentalReturnReminderConsumer(
    IWhatsAppService whatsAppService,
    IEmailService    emailService) : IConsumer<RentalReturnReminderMessage>
{
    public async Task Consume(ConsumeContext<RentalReturnReminderMessage> context)
    {
        var msg  = context.Message;
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
            await whatsAppService.SendTextAsync(msg.CustomerPhone, text, context.CancellationToken);
        }

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.RentalReturnReminder, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.RentalReturnReminder, vars);
        await emailService.SendAsync(msg.CustomerEmail, subject, body, context.CancellationToken);
    }
}
