using FluentAssertions;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Infrastructure.Messaging;
using Horafy.Infrastructure.Messaging.Consumers;
using MassTransit;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Notifications;

public sealed class BookingCreatedConsumerTests
{
    private readonly Mock<IWhatsAppService>    _whatsApp = new();
    private readonly Mock<IEmailService>       _email    = new();
    private readonly Mock<INotificationLogger> _logger   = new();

    private BookingCreatedConsumer MakeConsumer() =>
        new(_whatsApp.Object, _email.Object, _logger.Object);

    private static ConsumeContext<BookingCreatedMessage> MakeContext(string? phone = "5511999999999")
    {
        var msg = new BookingCreatedMessage(
            Guid.NewGuid(), "João", "joao@test.com", phone,
            "Corte", "Ana", DateTimeOffset.UtcNow.AddHours(2),
            "barbearia", "Barbearia do João");

        var ctx = new Mock<ConsumeContext<BookingCreatedMessage>>();
        ctx.SetupGet(c => c.Message).Returns(msg);
        ctx.SetupGet(c => c.CancellationToken).Returns(default(CancellationToken));
        return ctx.Object;
    }

    [Fact]
    public async Task Consume_WithPhone_SendsWhatsAppAndEmail()
    {
        _logger.Setup(l => l.SendAndLogAsync(
            It.IsAny<Func<Task>>(),
            It.IsAny<Domain.Entities.Notifications.NotificationEventType>(),
            It.IsAny<Domain.Entities.Notifications.NotificationChannel>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, Domain.Entities.Notifications.NotificationEventType,
                Domain.Entities.Notifications.NotificationChannel, string, string, CancellationToken>(
                async (send, _, _, _, _, _) => await send());

        var ctx = MakeContext(phone: "5511999999999");
        await MakeConsumer().Consume(ctx);

        _whatsApp.Verify(w => w.SendTextAsync(
            "5511999999999", It.IsAny<string>(), default), Times.Once);
        _email.Verify(e => e.SendAsync(
            "joao@test.com", It.IsAny<string>(), It.IsAny<string>(), default), Times.Once);
    }

    [Fact]
    public async Task Consume_WithoutPhone_SendsEmailOnly()
    {
        _logger.Setup(l => l.SendAndLogAsync(
            It.IsAny<Func<Task>>(),
            It.IsAny<Domain.Entities.Notifications.NotificationEventType>(),
            It.IsAny<Domain.Entities.Notifications.NotificationChannel>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, Domain.Entities.Notifications.NotificationEventType,
                Domain.Entities.Notifications.NotificationChannel, string, string, CancellationToken>(
                async (send, _, _, _, _, _) => await send());

        var ctx = MakeContext(phone: null);
        await MakeConsumer().Consume(ctx);

        _whatsApp.Verify(w => w.SendTextAsync(
            It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        _email.Verify(e => e.SendAsync(
            "joao@test.com", It.IsAny<string>(), It.IsAny<string>(), default), Times.Once);
    }
}
