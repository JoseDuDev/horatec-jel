using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class BookingCancelledConsumer(
    IWhatsAppService whatsAppService,
    IEmailService    emailService) : IConsumer<BookingCancelledMessage>
{
    public async Task Consume(ConsumeContext<BookingCancelledMessage> context)
    {
        var msg = context.Message;
        if (string.IsNullOrEmpty(msg.CustomerEmail)) return;

        var vars = new Dictionary<string, string>
        {
            ["customer_name"]       = msg.CustomerName,
            ["cancellation_reason"] = string.IsNullOrEmpty(msg.Reason) ? "" : $"{msg.Reason} ",
            ["tenant_name"]         = msg.TenantName
        };

        if (!string.IsNullOrEmpty(msg.CustomerPhone))
        {
            var text = TemplateRenderer.Render(DefaultTemplates.WhatsApp.BookingCancelled, vars);
            await whatsAppService.SendTextAsync(msg.CustomerPhone, text, context.CancellationToken);
        }

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.BookingCancelled, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.BookingCancelled, vars);
        await emailService.SendAsync(msg.CustomerEmail, subject, body, context.CancellationToken);
    }
}
