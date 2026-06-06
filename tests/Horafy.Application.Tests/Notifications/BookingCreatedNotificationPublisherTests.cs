using FluentAssertions;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Features.Notifications.Publishers;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using MassTransit;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Notifications;

public sealed class BookingCreatedNotificationPublisherTests
{
    private readonly Mock<IBookingRepository>    _bookingRepo  = new();
    private readonly Mock<IResourceRepository>   _resourceRepo = new();
    private readonly Mock<ITenantRepository>     _tenantRepo   = new();
    private readonly Mock<ICurrentTenantService> _tenantSvc    = new();
    private readonly Mock<IPublishEndpoint>       _bus          = new();

    private BookingCreatedNotificationPublisher MakeHandler() =>
        new(_bookingRepo.Object, _resourceRepo.Object, _tenantRepo.Object,
            _tenantSvc.Object, _bus.Object);

    private static Booking MakeBooking()
    {
        var svcId = Service.Create("Corte", 60, 100m).Id;
        return Booking.Create(
            serviceId:       svcId,
            resourceId:      Guid.NewGuid(),
            customerId:      Guid.NewGuid(),
            customerName:    "João Cliente",
            customerEmail:   "joao@test.com",
            scheduledAt:     DateTimeOffset.UtcNow.AddHours(3),
            durationMinutes: 60);
    }

    [Fact]
    public async Task Handle_BookingAndTenantFound_PublishesBookingCreatedMessage()
    {
        var booking  = MakeBooking();
        var tenantId = Guid.NewGuid();
        var resource = Resource.Create("Ana", ResourceType.Professional);
        var tenant   = Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop);

        _tenantSvc.SetupGet(t => t.TenantId).Returns(tenantId);
        _tenantSvc.SetupGet(t => t.Slug).Returns("barbearia");
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _resourceRepo.Setup(r => r.GetByIdAsync(booking.ResourceId, default)).ReturnsAsync(resource);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        var evt = new BookingCreatedEvent(
            booking.Id, booking.ServiceId, booking.ResourceId,
            booking.CustomerId, booking.CustomerPhone, booking.ScheduledAt);

        await MakeHandler().Handle(evt, default);

        _bus.Verify(b => b.Publish(
            It.Is<BookingCreatedMessage>(m =>
                m.BookingId     == booking.Id &&
                m.CustomerEmail == "joao@test.com" &&
                m.TenantSlug    == "barbearia"),
            default), Times.Once);
    }

    [Fact]
    public async Task Handle_BookingNotFound_DoesNotPublish()
    {
        _tenantSvc.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
        _tenantSvc.SetupGet(t => t.Slug).Returns("barbearia");
        _bookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Booking?)null);

        var evt = new BookingCreatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            null, DateTimeOffset.UtcNow.AddHours(2));

        await MakeHandler().Handle(evt, default);

        _bus.Verify(b => b.Publish(It.IsAny<BookingCreatedMessage>(), default), Times.Never);
    }
}
