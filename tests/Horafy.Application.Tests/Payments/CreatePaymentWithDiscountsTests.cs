using FluentAssertions;
using Horafy.Application.Features.Payments.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Entities.Vouchers;
using Horafy.Domain.Interfaces.Repositories;
using WalletEntity = Horafy.Domain.Entities.Wallet.Wallet;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Payments;

public sealed class CreatePaymentWithDiscountsTests
{
    private readonly Mock<IBookingRepository>    _bookingRepo  = new();
    private readonly Mock<ITenantRepository>     _tenantRepo   = new();
    private readonly Mock<IPaymentRepository>    _paymentRepo  = new();
    private readonly Mock<IPaymentGateway>       _gateway      = new();
    private readonly Mock<ICurrentTenantService> _tenantSvc    = new();
    private readonly Mock<ITenantUnitOfWork>     _unitOfWork   = new();
    private readonly Mock<IVoucherRepository>    _voucherRepo  = new();
    private readonly Mock<IWalletRepository>     _walletRepo   = new();
    private readonly Mock<ICurrentUserService>   _currentUser  = new();

    private CreatePaymentCommandHandler MakeHandler() =>
        new(_bookingRepo.Object, _tenantRepo.Object, _paymentRepo.Object,
            _gateway.Object, _tenantSvc.Object, _unitOfWork.Object,
            _voucherRepo.Object, _walletRepo.Object, _currentUser.Object);

    private static Booking MakeBooking()
    {
        var service  = Service.Create("Corte", 60, 100m);
        var resource = Resource.Create("João", ResourceType.Professional);
        return Booking.Create(
            new[] { (service.Id, "Corte", 60, 100m) },
            resource.Id,
            resource.Name,
            Guid.NewGuid(), "Cliente", "cliente@test.com",
            DateTimeOffset.UtcNow.AddHours(2));
    }

    private static WalletEntity MakeWallet(Guid userId, decimal balance)
    {
        var w = WalletEntity.Create(userId);
        if (balance > 0) w.AddCredits(balance, "Setup");
        return w;
    }

    [Fact]
    public async Task Handle_NoDiscounts_CallsGatewayWithFullAmount()
    {
        var booking = MakeBooking();
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _gateway.Setup(g => g.CreatePreferenceAsync(It.IsAny<CreatePaymentPreferenceRequest>(), default))
            .ReturnsAsync(new PaymentPreferenceResult("pref-1", "https://mp.com/pay", null));

        var result = await MakeHandler().Handle(
            new CreatePaymentCommand(booking.Id, 100m, PaymentMethod.Pix, "https://back.url"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.PaymentUrl.Should().NotBeNull();
        _gateway.Verify(g => g.CreatePreferenceAsync(
            It.Is<CreatePaymentPreferenceRequest>(r => r.Amount == 100m), default), Times.Once);
    }

    [Fact]
    public async Task Handle_WithVoucher_ReducesGatewayAmount()
    {
        var booking = MakeBooking();
        var voucher = Voucher.Create("SAVE10", VoucherDiscountType.Percentage, 10m, null, null, null);

        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _voucherRepo.Setup(r => r.GetByCodeAsync("SAVE10", default)).ReturnsAsync(voucher);
        _gateway.Setup(g => g.CreatePreferenceAsync(It.IsAny<CreatePaymentPreferenceRequest>(), default))
            .ReturnsAsync(new PaymentPreferenceResult("pref-2", "https://mp.com/pay2", null));

        var result = await MakeHandler().Handle(
            new CreatePaymentCommand(booking.Id, 100m, PaymentMethod.Pix, "https://back.url",
                VoucherCode: "SAVE10"), default);

        result.IsSuccess.Should().BeTrue();
        // 100m - 10% = 90m enviado ao gateway
        _gateway.Verify(g => g.CreatePreferenceAsync(
            It.Is<CreatePaymentPreferenceRequest>(r => r.Amount == 90m), default), Times.Once);
    }

    [Fact]
    public async Task Handle_WalletCoversAll_SkipsGatewayAndApprovesDirectly()
    {
        var userId  = Guid.NewGuid();
        var booking = MakeBooking();
        var wallet  = MakeWallet(userId, 150m);

        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _currentUser.Setup(u => u.UserId).Returns(userId);
        _walletRepo.Setup(r => r.GetByUserIdAsync(userId, default)).ReturnsAsync(wallet);

        var result = await MakeHandler().Handle(
            new CreatePaymentCommand(booking.Id, 100m, PaymentMethod.Pix, "https://back.url",
                UseWalletCredits: true), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.PaymentUrl.Should().BeNull();
        _gateway.Verify(g => g.CreatePreferenceAsync(
            It.IsAny<CreatePaymentPreferenceRequest>(), default), Times.Never);
        wallet.Balance.Should().Be(50m); // 150 - 100 = 50
    }

    [Fact]
    public async Task Handle_InvalidVoucherCode_ReturnsNotFound()
    {
        var booking = MakeBooking();
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _voucherRepo.Setup(r => r.GetByCodeAsync("INVALID", default)).ReturnsAsync((Voucher?)null);

        var result = await MakeHandler().Handle(
            new CreatePaymentCommand(booking.Id, 100m, PaymentMethod.Pix, "https://back.url",
                VoucherCode: "INVALID"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Voucher.NotFound");
        _gateway.Verify(g => g.CreatePreferenceAsync(
            It.IsAny<CreatePaymentPreferenceRequest>(), default), Times.Never);
    }
}
