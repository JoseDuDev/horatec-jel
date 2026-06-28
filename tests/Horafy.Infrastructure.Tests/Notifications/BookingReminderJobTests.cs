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
    public async Task ExecuteAsync_RemindersDisabled_DoesNotPublishBookingReminder()
    {
        var now     = DateTimeOffset.UtcNow;
        var booking = MakeConfirmedBookingAt(now.AddHours(24));
        var tenant  = Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop);
        tenant.UpdateReminderSettings(enabled: false, firstReminderHours: 24, secondReminderHours: 2);

        _bookingRepo.Setup(r => r.FindAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<Booking, bool>>>(), default))
            .ReturnsAsync(new List<Booking> { booking });
        _tenantRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Tenant> { tenant });

        await MakeJob().ExecuteAsync(now, default);

        _bus.Verify(b => b.Publish(It.IsAny<BookingReminderMessage>(), default), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CustomFirstReminderHours_PublishesInCustomWindow()
    {
        var now     = DateTimeOffset.UtcNow;
        var booking = MakeConfirmedBookingAt(now.AddHours(48));
        var tenant  = Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop);
        tenant.UpdateReminderSettings(enabled: true, firstReminderHours: 48, secondReminderHours: 0);

        // Aplica o predicado para respeitar a janela configurada.
        var list = new List<Booking> { booking };
        _bookingRepo.Setup(r => r.FindAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<Booking, bool>>>(), default))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Booking, bool>> pred, CancellationToken _) =>
                list.Where(pred.Compile()).ToList());
        _resourceRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(Resource.Create("Ana", ResourceType.Professional));
        _tenantRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Tenant> { tenant });

        await MakeJob().ExecuteAsync(now, default);

        _bus.Verify(b => b.Publish(
            It.Is<BookingReminderMessage>(m => m.BookingId == booking.Id && m.IsOneDayBefore),
            default), Times.Once);
    }

    private static Booking MakeRentalPickedUpEndingAt(DateTimeOffset endsAt)
    {
        var b = Booking.CreateRental(
            new[] { (Guid.NewGuid(), "Furadeira", 1, 90m) },
            customerId: Guid.NewGuid(), customerName: "João", customerEmail: "joao@test.com",
            startsAt: endsAt.AddDays(-3), endsAt: endsAt);
        b.Confirm();
        b.MarkRentalPickedUp();
        b.ClearDomainEvents();
        return b;
    }

    // Mock que aplica o predicado — distingue as janelas (devolução vs. atraso).
    private void SetupFilteredFind(params Booking[] master)
    {
        var list = master.ToList();
        _bookingRepo.Setup(r => r.FindAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<Booking, bool>>>(), default))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Booking, bool>> pred, CancellationToken _) =>
                list.Where(pred.Compile()).ToList());
    }

    [Fact]
    public async Task ExecuteAsync_RentalDueIn24Hours_PublishesReturnReminder()
    {
        var now    = DateTimeOffset.UtcNow;
        var rental = MakeRentalPickedUpEndingAt(now.AddHours(23.5));
        var tenant = Tenant.Create("Loja", "loja", TenantVertical.Barbershop);

        SetupFilteredFind(rental);
        _tenantRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Tenant> { tenant });

        await MakeJob().ExecuteAsync(now, default);

        _bus.Verify(b => b.Publish(
            It.Is<RentalReturnReminderMessage>(m =>
                m.BookingId == rental.Id && m.ItemName == "Furadeira" && m.TenantSlug == tenant.Slug),
            default), Times.Once);
        _bus.Verify(b => b.Publish(It.IsAny<RentalOverdueMessage>(), default), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_RentalOneDayOverdue_PublishesOverdueNotice()
    {
        var now    = DateTimeOffset.UtcNow;
        var rental = MakeRentalPickedUpEndingAt(now.AddHours(-23.5));
        var tenant = Tenant.Create("Loja", "loja", TenantVertical.Barbershop);

        SetupFilteredFind(rental);
        _tenantRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Tenant> { tenant });

        await MakeJob().ExecuteAsync(now, default);

        _bus.Verify(b => b.Publish(
            It.Is<RentalOverdueMessage>(m =>
                m.BookingId == rental.Id && m.DaysOverdue >= 1 && m.ItemName == "Furadeira"),
            default), Times.Once);
        _bus.Verify(b => b.Publish(It.IsAny<RentalReturnReminderMessage>(), default), Times.Never);
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
