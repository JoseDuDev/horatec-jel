using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Notifications;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class WaitlistSlotAvailableConsumer(
    IEmailService       emailService,
    INotificationLogger logger) : IConsumer<WaitlistSlotAvailableMessage>
{
    public async Task Consume(ConsumeContext<WaitlistSlotAvailableMessage> context)
    {
        var msg = context.Message;
        var ct  = context.CancellationToken;

        var vars = new Dictionary<string, string>
        {
            ["customer_name"]  = msg.CustomerName,
            ["service_name"]   = msg.ServiceName,
            ["resource_name"]  = msg.ResourceName,
            ["preferred_date"] = msg.PreferredDate.ToString("dd/MM/yyyy"),
            ["tenant_name"]    = msg.TenantName
        };

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.WaitlistSlotAvailable, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.WaitlistSlotAvailable, vars);
        await logger.SendAndLogAsync(
            () => emailService.SendAsync(msg.CustomerEmail, subject, body, ct),
            NotificationEventType.BookingCreated, NotificationChannel.Email,
            msg.CustomerEmail, msg.TenantSlug, ct);
    }
}
