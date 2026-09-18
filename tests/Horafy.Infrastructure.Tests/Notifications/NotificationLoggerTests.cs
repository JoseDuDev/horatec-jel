using FluentAssertions;
using Horafy.Domain.Entities.Notifications;
using Horafy.Infrastructure.Messaging;
using Horafy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Horafy.Infrastructure.Tests.Notifications;

public sealed class NotificationLoggerTests
{
    private static (NotificationLogger Logger, HorafyDbContext Db) Make()
    {
        var options = new DbContextOptionsBuilder<HorafyDbContext>()
            .UseInMemoryDatabase($"notif-{Guid.NewGuid()}")
            .Options;
        var db = new HorafyDbContext(options);
        return (new NotificationLogger(db, NullLogger<NotificationLogger>.Instance), db);
    }

    private static Task Run(NotificationLogger l, Func<Task> send, bool rethrow) =>
        rethrow
            ? l.SendAndLogAsync(send, NotificationEventType.BookingCreated,
                NotificationChannel.Email, "cliente@test.com", "barbearia", default)
            : l.TrySendAndLogAsync(send, NotificationEventType.BookingCreated,
                NotificationChannel.WhatsApp, "5511999999999", "barbearia", default);

    [Fact]
    public async Task SendAndLogAsync_QuandoFalha_Repropaga()
    {
        var (logger, _) = Make();

        var act = async () => await Run(logger,
            () => throw new HttpRequestException("SMTP fora do ar"), rethrow: true);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // O motivo de existir: uma instância errada na Evolution não pode impedir o e-mail
    // que o consumer envia logo depois.
    [Fact]
    public async Task TrySendAndLogAsync_QuandoFalha_NaoRepropaga()
    {
        var (logger, _) = Make();

        var act = async () => await Run(logger,
            () => throw new HttpRequestException("404 — instance does not exist"), rethrow: false);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task TrySendAndLogAsync_QuandoFalha_RegistraOErro()
    {
        var (logger, db) = Make();

        await Run(logger, () => throw new HttpRequestException("404 — instance does not exist"),
            rethrow: false);

        var log = await db.NotificationLogs.SingleAsync();
        log.Success.Should().BeFalse();
        log.ErrorMessage.Should().Contain("404");
        log.Channel.Should().Be(NotificationChannel.WhatsApp);
    }

    [Fact]
    public async Task TrySendAndLogAsync_QuandoDaCerto_RegistraSucesso()
    {
        var (logger, db) = Make();

        await Run(logger, () => Task.CompletedTask, rethrow: false);

        var log = await db.NotificationLogs.SingleAsync();
        log.Success.Should().BeTrue();
        log.ErrorMessage.Should().BeNull();
    }
}
