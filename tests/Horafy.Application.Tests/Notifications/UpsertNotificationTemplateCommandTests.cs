using FluentAssertions;
using Horafy.Application.Features.Notifications.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Notifications;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Notifications;

public sealed class UpsertNotificationTemplateCommandTests
{
    private readonly Mock<INotificationTemplateRepository> _repo = new();
    private readonly Mock<ITenantUnitOfWork>               _uow  = new();

    private UpsertNotificationTemplateCommandHandler MakeHandler() =>
        new(_repo.Object, _uow.Object);

    [Fact]
    public async Task Handle_NoExistingTemplate_CreatesNew()
    {
        _repo.Setup(r => r.GetActiveAsync(
                NotificationEventType.BookingCreated,
                NotificationChannel.WhatsApp, default))
            .ReturnsAsync((NotificationTemplate?)null);

        var result = await MakeHandler().Handle(new UpsertNotificationTemplateCommand(
            NotificationEventType.BookingCreated,
            NotificationChannel.WhatsApp,
            BodyTemplate: "Olá {{customer_name}}!",
            SubjectTemplate: null), default);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.Add(It.IsAny<NotificationTemplate>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingTemplate_UpdatesIt()
    {
        var existing = NotificationTemplate.Create(
            NotificationEventType.BookingCreated,
            NotificationChannel.WhatsApp,
            bodyTemplate: "original");

        _repo.Setup(r => r.GetActiveAsync(
                NotificationEventType.BookingCreated,
                NotificationChannel.WhatsApp, default))
            .ReturnsAsync(existing);

        var result = await MakeHandler().Handle(new UpsertNotificationTemplateCommand(
            NotificationEventType.BookingCreated,
            NotificationChannel.WhatsApp,
            BodyTemplate: "Novo texto",
            SubjectTemplate: null), default);

        result.IsSuccess.Should().BeTrue();
        existing.BodyTemplate.Should().Be("Novo texto");
        _repo.Verify(r => r.Add(It.IsAny<NotificationTemplate>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmptyBodyTemplate_ReturnsValidationError()
    {
        var result = await MakeHandler().Handle(new UpsertNotificationTemplateCommand(
            NotificationEventType.BookingCreated,
            NotificationChannel.WhatsApp,
            BodyTemplate: "",
            SubjectTemplate: null), default);

        result.IsFailure.Should().BeTrue();
    }
}
