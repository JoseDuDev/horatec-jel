using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class RentalOverdueConsumer(
    IWhatsAppService whatsAppService,
    IEmailService    emailService) : IConsumer<RentalOverdueMessage>
{
    public async Task Consume(ConsumeContext<RentalOverdueMessage> context)
    {
        var msg  = context.Message;
        var vars = new Dictionary<string, string>
        {
            ["customer_name"] = msg.CustomerName,
            ["item_name"]     = msg.ItemName,
            ["due_at"]        = TemplateRenderer.FormatBrazilian(msg.DueAt),
            ["days_overdue"]  = msg.DaysOverdue.ToString(),
            ["tenant_name"]   = msg.TenantName,
        };

        if (!string.IsNullOrEmpty(msg.CustomerPhone))
        {
            var text = TemplateRenderer.Render(DefaultTemplates.WhatsApp.RentalOverdue, vars);
            await whatsAppService.SendTextAsync(msg.CustomerPhone, text, context.CancellationToken);
        }

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.RentalOverdue, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.RentalOverdue, vars);
        await emailService.SendAsync(msg.CustomerEmail, subject, body, context.CancellationToken);
    }
}
