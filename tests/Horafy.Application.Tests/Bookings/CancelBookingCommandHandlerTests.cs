using FluentAssertions;
using Horafy.Application.Features.Bookings.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Bookings;

public sealed class CancelBookingCommandHandlerTests
{
    private readonly Mock<IBookingRepository>    _bookingRepo   = new();
    private readonly Mock<ITenantRepository>     _tenantRepo    = new();
    private readonly Mock<ICurrentUserService>   _currentUser   = new();
    private readonly Mock<ICurrentTenantService> _currentTenant = new();
    private readonly Mock<ITenantUnitOfWork>     _unitOfWork    = new();

    private CancelBookingCommandHandler MakeHandler() =>
        new(_bookingRepo.Object, _tenantRepo.Object, _currentUser.Object, _currentTenant.Object, _unitOfWork.Object);

    private static Booking MakePendingBooking(Guid customerId) =>
        Booking.Create(
            Service.Create("Corte", 60, 50m).Id,
            Resource.Create("João", ResourceType.Professional).Id,
            customerId, "Cliente", "cliente@test.com",
            DateTimeOffset.UtcNow.AddHours(3), 60);

    private static Tenant MakeTenantWithPolicy(int minHours = 0, bool allowCustomer = true)
    {
        var t = Tenant.Create("Test", "test-slug", TenantVertical.Barbershop);
        t.UpdateCancellationPolicy(minHours, 0, allowCustomer);
        return t;
    }

    [Fact]
    public async Task Handle_CustomerCancels_WithinPolicy_ReturnsSuccess()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var booking = MakePendingBooking(userId);
        var tenant = MakeTenantWithPolicy(minHours: 1);  // 1h minimum, booking is 3h away

        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _currentUser.SetupGet(u => u.Role).Returns(UserRole.Customer);
        _currentTenant.SetupGet(t => t.TenantId).Returns(tenantId);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        var result = await MakeHandler().Handle(new CancelBookingCommand(booking.Id, null), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CustomerCancels_PolicyWindowClosed_ReturnsError()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var booking = MakePendingBooking(userId);
        var tenant = MakeTenantWithPolicy(minHours: 48);  // 48h minimum, booking is only 3h away

        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _currentUser.SetupGet(u => u.Role).Returns(UserRole.Customer);
        _currentTenant.SetupGet(t => t.TenantId).Returns(tenantId);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        var result = await MakeHandler().Handle(new CancelBookingCommand(booking.Id, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.CancellationWindowClosed");
    }

    [Fact]
    public async Task Handle_CustomerCancels_NotAllowed_ReturnsError()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var booking = MakePendingBooking(userId);
        var tenant = MakeTenantWithPolicy(allowCustomer: false);

        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _currentUser.SetupGet(u => u.Role).Returns(UserRole.Customer);
        _currentTenant.SetupGet(t => t.TenantId).Returns(tenantId);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        var result = await MakeHandler().Handle(new CancelBookingCommand(booking.Id, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.CancellationNotAllowed");
    }

    [Fact]
    public async Task Handle_StaffCancels_IgnoresPolicy_ReturnsSuccess()
    {
        var tenantId = Guid.NewGuid();
        var booking = MakePendingBooking(Guid.NewGuid());
        var tenant = MakeTenantWithPolicy(minHours: 48, allowCustomer: false);

        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid()); // different user = not owner
        _currentUser.SetupGet(u => u.Role).Returns(UserRole.TenantAdmin);
        _currentTenant.SetupGet(t => t.TenantId).Returns(tenantId);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        var result = await MakeHandler().Handle(new CancelBookingCommand(booking.Id, "Staff override"), default);

        result.IsSuccess.Should().BeTrue();
    }
}
