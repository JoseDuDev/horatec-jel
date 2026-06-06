using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class PaymentConfirmedConsumer(
    IWhatsAppService whatsAppService,
    IEmailService    emailService) : IConsumer<PaymentConfirmedMessage>
{
    public async Task Consume(ConsumeContext<PaymentConfirmedMessage> context)
    {
        var msg  = context.Message;
        var vars = new Dictionary<string, string>
        {
            ["customer_name"] = msg.CustomerName,
            ["amount"]        = msg.Amount.ToString("N2"),
            ["tenant_name"]   = msg.TenantName
        };

        if (!string.IsNullOrEmpty(msg.CustomerPhone))
        {
            var text = TemplateRenderer.Render(DefaultTemplates.WhatsApp.PaymentConfirmed, vars);
            await whatsAppService.SendTextAsync(msg.CustomerPhone, text, context.CancellationToken);
        }

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.PaymentConfirmed, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.PaymentConfirmed, vars);
        await emailService.SendAsync(msg.CustomerEmail, subject, body, context.CancellationToken);
    }
}
