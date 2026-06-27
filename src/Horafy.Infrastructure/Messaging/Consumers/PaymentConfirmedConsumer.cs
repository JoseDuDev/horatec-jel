using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Notifications;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class PaymentConfirmedConsumer(
    IWhatsAppService   whatsAppService,
    IEmailService      emailService,
    INotificationLogger logger) : IConsumer<PaymentConfirmedMessage>
{
    public async Task Consume(ConsumeContext<PaymentConfirmedMessage> context)
    {
        var msg  = context.Message;
        var ct   = context.CancellationToken;
        var vars = new Dictionary<string, string>
        {
            ["customer_name"] = msg.CustomerName,
            ["amount"]        = msg.Amount.ToString("N2"),
            ["tenant_name"]   = msg.TenantName
        };

        if (!string.IsNullOrEmpty(msg.CustomerPhone))
        {
            var text = TemplateRenderer.Render(DefaultTemplates.WhatsApp.PaymentConfirmed, vars);
            await logger.SendAndLogAsync(
                () => whatsAppService.SendTextAsync(msg.CustomerPhone, text, ct),
                NotificationEventType.PaymentConfirmed, NotificationChannel.WhatsApp,
                msg.CustomerPhone, msg.TenantSlug, ct);
        }

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.PaymentConfirmed, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.PaymentConfirmed, vars);
        await logger.SendAndLogAsync(
            () => emailService.SendAsync(msg.CustomerEmail, subject, body, ct),
            NotificationEventType.PaymentConfirmed, NotificationChannel.Email,
            msg.CustomerEmail, msg.TenantSlug, ct);
    }
}
