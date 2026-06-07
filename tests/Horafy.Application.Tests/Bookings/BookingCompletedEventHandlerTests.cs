using FluentAssertions;
using Horafy.Application.Features.Bookings.EventHandlers;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using WalletEntity = Horafy.Domain.Entities.Wallet.Wallet;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Bookings;

public sealed class BookingCompletedEventHandlerTests
{
    private readonly Mock<ITenantRepository>     _tenantRepo  = new();
    private readonly Mock<IPaymentRepository>    _paymentRepo = new();
    private readonly Mock<IWalletRepository>     _walletRepo  = new();
    private readonly Mock<ICurrentTenantService> _tenantSvc   = new();
    private readonly Mock<ITenantUnitOfWork>     _uow         = new();

    private BookingCompletedEventHandler MakeHandler() => new(
        _tenantRepo.Object, _paymentRepo.Object,
        _walletRepo.Object, _tenantSvc.Object, _uow.Object);

    private static Tenant MakeTenantWithLoyalty(bool enabled, decimal rate = 10m, decimal min = 0m)
    {
        var t = Tenant.Create("T", "t", TenantVertical.Barbershop);
        t.UpdateLoyaltySettings(enabled, rate, min);
        return t;
    }

    [Fact]
    public async Task Handle_LoyaltyEnabled_AwardsWalletBonusToCustomer()
    {
        var tenantId   = Guid.NewGuid();
        var bookingId  = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var tenant     = MakeTenantWithLoyalty(enabled: true, rate: 10m);

        var payment = Payment.Create(bookingId, "pref", PaymentMethod.Pix, 100m, 0m);
        payment.Approve("mp-dummy-id");

        _tenantSvc.Setup(t => t.TenantId).Returns(tenantId);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);
        _paymentRepo.Setup(r => r.GetByBookingIdAsync(bookingId, default)).ReturnsAsync(payment);
        _walletRepo.Setup(r => r.GetByUserIdAsync(customerId, default)).ReturnsAsync((WalletEntity?)null);

        await MakeHandler().Handle(
            new BookingCompletedEvent(bookingId, customerId), default);

        _walletRepo.Verify(r => r.Add(It.Is<WalletEntity>(w =>
            w.UserId == customerId && w.Balance == 10m)), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_LoyaltyDisabled_DoesNotAwardBonus()
    {
        var tenantId  = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var tenant    = MakeTenantWithLoyalty(enabled: false);

        _tenantSvc.Setup(t => t.TenantId).Returns(tenantId);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        await MakeHandler().Handle(
            new BookingCompletedEvent(bookingId, Guid.NewGuid()), default);

        _paymentRepo.Verify(r => r.GetByBookingIdAsync(It.IsAny<Guid>(), default), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_PaymentNotFound_DoesNotAwardBonus()
    {
        var tenantId  = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var tenant    = MakeTenantWithLoyalty(enabled: true, rate: 10m);

        _tenantSvc.Setup(t => t.TenantId).Returns(tenantId);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);
        _paymentRepo.Setup(r => r.GetByBookingIdAsync(bookingId, default)).ReturnsAsync((Payment?)null);

        await MakeHandler().Handle(
            new BookingCompletedEvent(bookingId, Guid.NewGuid()), default);

        _walletRepo.Verify(r => r.Add(It.IsAny<WalletEntity>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }
}
