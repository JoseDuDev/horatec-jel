using FluentAssertions;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Notifications;
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

    public BookingCreatedConsumerTests()
    {
        // Os dois caminhos do logger apenas executam o envio; o que interessa aqui é o
        // consumer, não o registro. O comportamento de engolir/repropagar tem teste
        // próprio em NotificationLoggerTests.
        _logger.Setup(l => l.SendAndLogAsync(
                It.IsAny<Func<Task>>(), It.IsAny<NotificationEventType>(),
                It.IsAny<NotificationChannel>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, NotificationEventType, NotificationChannel, string, string,
                CancellationToken>(async (send, _, _, _, _, _) => await send());

        _logger.Setup(l => l.TrySendAndLogAsync(
                It.IsAny<Func<Task>>(), It.IsAny<NotificationEventType>(),
                It.IsAny<NotificationChannel>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, NotificationEventType, NotificationChannel, string, string,
                CancellationToken>(async (send, _, _, _, _, _) =>
                {
                    try { await send(); } catch { /* não repropaga, como o real */ }
                });
    }

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
        var ctx = MakeContext(phone: null);
        await MakeConsumer().Consume(ctx);

        _whatsApp.Verify(w => w.SendTextAsync(
            It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        _email.Verify(e => e.SendAsync(
            "joao@test.com", It.IsAny<string>(), It.IsAny<string>(), default), Times.Once);
    }

    /// <summary>
    /// O cenário que motivou o TrySendAndLogAsync: envs da Evolution preenchidas, mas a
    /// instância não existe. O sendText devolve 404, o EnsureSuccessStatusCode estoura —
    /// e antes disso derrubava o consumer inteiro, deixando o cliente sem o e-mail também.
    /// </summary>
    [Fact]
    public async Task Consume_WhatsAppFalha_AindaEnviaOEmail()
    {
        _whatsApp
            .Setup(w => w.SendTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("404 (Not Found) — instance does not exist"));

        var ctx = MakeContext(phone: "5511999999999");

        var act = async () => await MakeConsumer().Consume(ctx);
        await act.Should().NotThrowAsync();

        _email.Verify(e => e.SendAsync(
            "joao@test.com", It.IsAny<string>(), It.IsAny<string>(), default), Times.Once);
    }

    /// <summary>
    /// A contrapartida: o e-mail é o último canal e continua repropagando, para a mensagem
    /// voltar para a fila e o MassTransit tentar de novo.
    /// </summary>
    [Fact]
    public async Task Consume_EmailFalha_Repropaga()
    {
        _email
            .Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP fora do ar"));

        var ctx = MakeContext(phone: null);

        var act = async () => await MakeConsumer().Consume(ctx);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
