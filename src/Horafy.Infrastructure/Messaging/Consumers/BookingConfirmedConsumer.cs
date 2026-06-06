using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class BookingConfirmedConsumer(
    IWhatsAppService whatsAppService,
    IEmailService    emailService) : IConsumer<BookingConfirmedMessage>
{
    public async Task Consume(ConsumeContext<BookingConfirmedMessage> context)
    {
        var msg  = context.Message;
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
            await whatsAppService.SendTextAsync(msg.CustomerPhone, text, context.CancellationToken);
        }

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.BookingConfirmed, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.BookingConfirmed, vars);
        await emailService.SendAsync(msg.CustomerEmail, subject, body, context.CancellationToken);
    }
}
