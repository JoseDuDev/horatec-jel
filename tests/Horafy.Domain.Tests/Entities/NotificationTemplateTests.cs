using FluentAssertions;
using Horafy.Domain.Entities.Notifications;
using Xunit;

namespace Horafy.Domain.Tests.Entities;

public sealed class NotificationTemplateTests
{
    [Fact]
    public void Create_SetsPropertiesCorrectly()
    {
        var t = NotificationTemplate.Create(
            NotificationEventType.BookingCreated,
            NotificationChannel.WhatsApp,
            bodyTemplate: "Olá, {{customer_name}}!");

        t.EventType.Should().Be(NotificationEventType.BookingCreated);
        t.Channel.Should().Be(NotificationChannel.WhatsApp);
        t.BodyTemplate.Should().Be("Olá, {{customer_name}}!");
        t.SubjectTemplate.Should().BeNull();
        t.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Update_ChangesTemplates()
    {
        var t = NotificationTemplate.Create(
            NotificationEventType.BookingConfirmed,
            NotificationChannel.Email,
            bodyTemplate: "original",
            subjectTemplate: "original subject");

        t.Update("novo subject", "novo body");

        t.SubjectTemplate.Should().Be("novo subject");
        t.BodyTemplate.Should().Be("novo body");
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var t = NotificationTemplate.Create(
            NotificationEventType.BookingCancelled,
            NotificationChannel.WhatsApp,
            bodyTemplate: "cancelado");

        t.Deactivate();
        t.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var t = NotificationTemplate.Create(
            NotificationEventType.PaymentPending,
            NotificationChannel.Email,
            bodyTemplate: "pagamento");
        t.Deactivate();

        t.Activate();
        t.IsActive.Should().BeTrue();
    }
}
