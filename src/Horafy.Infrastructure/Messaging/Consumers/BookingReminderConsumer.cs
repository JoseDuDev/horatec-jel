using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class BookingReminderConsumer(
    IWhatsAppService whatsAppService,
    IEmailService    emailService) : IConsumer<BookingReminderMessage>
{
    public async Task Consume(ConsumeContext<BookingReminderMessage> context)
    {
        var msg  = context.Message;
        var vars = new Dictionary<string, string>
        {
            ["customer_name"] = msg.CustomerName,
            ["service_name"]  = msg.ServiceName,
            ["resource_name"] = msg.ResourceName,
            ["scheduled_at"]  = TemplateRenderer.FormatBrazilian(msg.ScheduledAt),
            ["scheduled_time"] = msg.ScheduledAt.ToString("HH:mm"),
            ["tenant_name"]   = msg.TenantName
        };

        var whatsAppTemplate = msg.IsOneDayBefore
            ? DefaultTemplates.WhatsApp.BookingReminderOneDay
            : DefaultTemplates.WhatsApp.BookingReminderTwoHours;

        if (!string.IsNullOrEmpty(msg.CustomerPhone))
        {
            var text = TemplateRenderer.Render(whatsAppTemplate, vars);
            await whatsAppService.SendTextAsync(msg.CustomerPhone, text, context.CancellationToken);
        }

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.BookingReminder, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.BookingReminder, vars);
        await emailService.SendAsync(msg.CustomerEmail, subject, body, context.CancellationToken);
    }
}
