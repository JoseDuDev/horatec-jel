using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class BookingCreatedConsumer(
    IWhatsAppService whatsAppService,
    IEmailService    emailService) : IConsumer<BookingCreatedMessage>
{
    public async Task Consume(ConsumeContext<BookingCreatedMessage> context)
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
            var text = TemplateRenderer.Render(DefaultTemplates.WhatsApp.BookingCreated, vars);
            await whatsAppService.SendTextAsync(msg.CustomerPhone, text, context.CancellationToken);
        }

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.BookingCreated, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.BookingCreated, vars);
        await emailService.SendAsync(msg.CustomerEmail, subject, body, context.CancellationToken);
    }
}
