using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class PaymentPendingConsumer(
    IWhatsAppService whatsAppService,
    IEmailService    emailService) : IConsumer<PaymentPendingMessage>
{
    public async Task Consume(ConsumeContext<PaymentPendingMessage> context)
    {
        var msg  = context.Message;
        var vars = new Dictionary<string, string>
        {
            ["customer_name"] = msg.CustomerName,
            ["amount"]        = msg.Amount.ToString("N2"),
            ["payment_url"]   = msg.PaymentUrl ?? "",
            ["tenant_name"]   = msg.TenantName
        };

        if (!string.IsNullOrEmpty(msg.CustomerPhone))
        {
            var text = TemplateRenderer.Render(DefaultTemplates.WhatsApp.PaymentPending, vars);
            await whatsAppService.SendTextAsync(msg.CustomerPhone, text, context.CancellationToken);
        }

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.PaymentPending, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.PaymentPending, vars);
        await emailService.SendAsync(msg.CustomerEmail, subject, body, context.CancellationToken);
    }
}
