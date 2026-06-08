using FluentAssertions;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Messaging.Jobs;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Horafy.Infrastructure.Tests.Notifications;

public sealed class BookingReminderJobTests
{
    private readonly Mock<IBookingRepository>  _bookingRepo  = new();
    private readonly Mock<IResourceRepository> _resourceRepo = new();
    private readonly Mock<ITenantRepository>   _tenantRepo   = new();
    private readonly Mock<IBus>                _bus          = new();

    private BookingReminderJob MakeJob()
    {
        var services = new ServiceCollection();
        services.AddScoped<ICurrentTenantService, NoopTenantService>();
        services.AddScoped<IBookingRepository>(_ => _bookingRepo.Object);
        services.AddScoped<IResourceRepository>(_ => _resourceRepo.Object);
        services.AddScoped<ITenantRepository>(_ => _tenantRepo.Object);

        var provider = services.BuildServiceProvider();
        return new BookingReminderJob(provider.GetRequiredService<IServiceScopeFactory>(), _bus.Object);
    }

    private static Booking MakeConfirmedBookingAt(DateTimeOffset scheduledAt)
    {
        var b = Booking.Create(
            serviceId:       Guid.NewGuid(),
            resourceId:      Guid.NewGuid(),
            customerId:      Guid.NewGuid(),
            customerName:    "João",
            customerEmail:   "joao@test.com",
            scheduledAt:     scheduledAt,
            durationMinutes: 60);
        b.Confirm();
        b.ClearDomainEvents();
        return b;
    }

    [Fact]
    public async Task ExecuteAsync_BookingIn24Hours_PublishesOneDayBeforeReminder()
    {
        var now      = DateTimeOffset.UtcNow;
        var booking  = MakeConfirmedBookingAt(now.AddHours(24));
        var tenant   = Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop);
        var resource = Resource.Create("Ana", ResourceType.Professional);

        _bookingRepo.Setup(r => r.FindAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<Booking, bool>>>(), default))
            .ReturnsAsync(new List<Booking> { booking });
        _resourceRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync(resource);
        _tenantRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Tenant> { tenant });

        await MakeJob().ExecuteAsync(now, default);

        _bus.Verify(b => b.Publish(
            It.Is<BookingReminderMessage>(m =>
                m.BookingId      == booking.Id    &&
                m.IsOneDayBefore == true          &&
                m.TenantSlug     == tenant.Slug   &&
                m.TenantName     == tenant.Name),
            default), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_NoUpcomingBookings_DoesNotPublish()
    {
        _bookingRepo.Setup(r => r.FindAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<Booking, bool>>>(), default))
            .ReturnsAsync(new List<Booking>());
        _tenantRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(new List<Tenant>());

        await MakeJob().ExecuteAsync(DateTimeOffset.UtcNow, default);

        _bus.Verify(b => b.Publish(It.IsAny<BookingReminderMessage>(), default), Times.Never);
    }

    private sealed class NoopTenantService : ICurrentTenantService
    {
        public Guid?    TenantId   { get; private set; }
        public string?  SchemaName { get; private set; }
        public string?  Slug       { get; private set; }

        public void SetTenant(Guid tenantId, string schemaName, string slug)
        {
            TenantId   = tenantId;
            SchemaName = schemaName;
            Slug       = slug;
        }
    }
}
