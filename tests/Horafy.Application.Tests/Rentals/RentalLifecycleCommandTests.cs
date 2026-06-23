using FluentAssertions;
using Horafy.Application.Features.Rentals;
using Horafy.Application.Features.Rentals.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Entities.Rentals;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;
using WalletEntity = Horafy.Domain.Entities.Wallet.Wallet;

namespace Horafy.Application.Tests.Rentals;

public class RentalLifecycleCommandTests
{
    private static (Mock<IBookingRepository> bookings,
                    Mock<IRentableItemRepository> items,
                    Mock<IWalletRepository> wallets,
                    Mock<IPaymentRepository> payments,
                    Mock<IPaymentGateway> gateway,
                    Mock<ITenantUnitOfWork> uow) Mocks()
    {
        var bookings = new Mock<IBookingRepository>();
        var items    = new Mock<IRentableItemRepository>();
        var wallets  = new Mock<IWalletRepository>();
        var payments = new Mock<IPaymentRepository>();
        var gateway  = new Mock<IPaymentGateway>();
        var uow      = new Mock<ITenantUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return (bookings, items, wallets, payments, gateway, uow);
    }

    private static MarkRentalReturnedCommandHandler ReturnHandler(
        Mock<IBookingRepository> bookings, Mock<IRentableItemRepository> items,
        Mock<IWalletRepository> wallets, Mock<IPaymentRepository> payments,
        Mock<IPaymentGateway> gateway, Mock<ITenantUnitOfWork> uow) =>
        new(bookings.Object, items.Object, wallets.Object,
            payments.Object, gateway.Object, uow.Object);

    private static Booking RentalIn(
        RentalLifecycle stage, RentableItem item,
        DateTimeOffset start, DateTimeOffset end, decimal deposit = 0)
    {
        var b = Booking.CreateRental(
            new[] { (item.Id, item.Name, 1, 90m) },
            customerId: Guid.NewGuid(), customerName: "C", customerEmail: "c@test.com",
            startsAt: start, endsAt: end, securityDeposit: deposit);
        if (stage >= RentalLifecycle.PickedUp) { b.Confirm(); b.MarkRentalPickedUp(); }
        return b;
    }

    private static Payment ApprovedPayment(Guid bookingId, decimal amount, decimal deposit)
    {
        var p = Payment.Create(bookingId, "pref-1", PaymentMethod.CreditCard, amount, deposit);
        p.Approve("mp-123");
        return p;
    }

    // ── Pickup ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pickup_ConfirmedReserved_Succeeds()
    {
        var (bookings, _, _, _, _, uow) = Mocks();
        var item = RentableItem.Create("Furadeira", 5, 30m);
        var b = Booking.CreateRental(
            new[] { (item.Id, item.Name, 1, 90m) },
            customerId: Guid.NewGuid(), customerName: "C", customerEmail: "c@test.com",
            startsAt: DateTimeOffset.UtcNow.AddDays(1), endsAt: DateTimeOffset.UtcNow.AddDays(4));
        b.Confirm();
        bookings.Setup(r => r.GetByIdAsync(b.Id, It.IsAny<CancellationToken>())).ReturnsAsync(b);

        var handler = new MarkRentalPickedUpCommandHandler(bookings.Object, uow.Object);
        var result = await handler.Handle(new MarkRentalPickedUpCommand(b.Id), default);

        result.IsSuccess.Should().BeTrue();
        b.RentalStatus.Should().Be(RentalLifecycle.PickedUp);
        bookings.Verify(r => r.Update(b), Times.Once);
    }

    [Fact]
    public async Task Pickup_NotFound_Fails()
    {
        var (bookings, _, _, _, _, uow) = Mocks();
        bookings.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Booking?)null);

        var handler = new MarkRentalPickedUpCommandHandler(bookings.Object, uow.Object);
        var result = await handler.Handle(new MarkRentalPickedUpCommand(Guid.NewGuid()), default);

        result.Error.Should().Be(RentalErrors.BookingNotFound);
    }

    [Fact]
    public async Task Pickup_OnAppointment_Fails()
    {
        var (bookings, _, _, _, _, uow) = Mocks();
        var appt = Booking.Create(
            services: new[] { (Guid.NewGuid(), "Corte", 30, 50m) },
            resourceId: Guid.NewGuid(), resourceName: "R", customerId: Guid.NewGuid(),
            customerName: "C", customerEmail: "c@test.com",
            scheduledAt: DateTimeOffset.UtcNow.AddHours(2));
        bookings.Setup(r => r.GetByIdAsync(appt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(appt);

        var handler = new MarkRentalPickedUpCommandHandler(bookings.Object, uow.Object);
        var result = await handler.Handle(new MarkRentalPickedUpCommand(appt.Id), default);

        result.Error.Should().Be(RentalErrors.NotARental);
    }

    // ── Return ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Return_OnTime_SucceedsWithNoLateFee()
    {
        var (bookings, items, wallets, payments, gateway, uow) = Mocks();
        var item = RentableItem.Create("Furadeira", 5, 30m);
        var b = RentalIn(RentalLifecycle.PickedUp, item,
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(4));
        bookings.Setup(r => r.GetByIdAsync(b.Id, It.IsAny<CancellationToken>())).ReturnsAsync(b);

        var handler = ReturnHandler(bookings, items, wallets, payments, gateway, uow);
        var result = await handler.Handle(new MarkRentalReturnedCommand(b.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.LateDays.Should().Be(0);
        result.Value.LateFee.Should().Be(0m);
        result.Value.DepositRefunded.Should().Be(0m);
        result.Value.Destination.Should().Be(RentalRefundDestination.None);
        b.RentalStatus.Should().Be(RentalLifecycle.Returned);
        b.Status.Should().Be(BookingStatus.Completed);
    }

    [Fact]
    public async Task Return_WithDeposit_RefundsToWallet()
    {
        var (bookings, items, wallets, payments, gateway, uow) = Mocks();
        var item = RentableItem.Create("Furadeira", 5, 30m, securityDeposit: 100m);
        var b = RentalIn(RentalLifecycle.PickedUp, item,
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(4), deposit: 100m);
        bookings.Setup(r => r.GetByIdAsync(b.Id, It.IsAny<CancellationToken>())).ReturnsAsync(b);
        wallets.Setup(r => r.GetByUserIdAsync(b.CustomerId, It.IsAny<CancellationToken>()))
               .ReturnsAsync((WalletEntity?)null);

        WalletEntity? added = null;
        wallets.Setup(r => r.Add(It.IsAny<WalletEntity>())).Callback<WalletEntity>(w => added = w);

        var handler = ReturnHandler(bookings, items, wallets, payments, gateway, uow);
        var result = await handler.Handle(new MarkRentalReturnedCommand(b.Id), default);

        result.Value.DepositRefunded.Should().Be(100m);
        result.Value.Destination.Should().Be(RentalRefundDestination.Wallet);
        added.Should().NotBeNull();
        added!.Balance.Should().Be(100m);
    }

    [Fact]
    public async Task Return_Late_DeductsFeeFromDepositRefund()
    {
        var (bookings, items, wallets, payments, gateway, uow) = Mocks();
        var item = RentableItem.Create("Furadeira", 5, 30m, securityDeposit: 100m);
        // devolução prevista 2 dias atrás → multa 2 × R$ 30 = R$ 60; estorno = 100 − 60 = 40
        var b = RentalIn(RentalLifecycle.PickedUp, item,
            DateTimeOffset.UtcNow.AddDays(-5), DateTimeOffset.UtcNow.AddDays(-2), deposit: 100m);
        bookings.Setup(r => r.GetByIdAsync(b.Id, It.IsAny<CancellationToken>())).ReturnsAsync(b);
        items.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[] { item });
        wallets.Setup(r => r.GetByUserIdAsync(b.CustomerId, It.IsAny<CancellationToken>()))
               .ReturnsAsync((WalletEntity?)null);

        var handler = ReturnHandler(bookings, items, wallets, payments, gateway, uow);
        var result = await handler.Handle(new MarkRentalReturnedCommand(b.Id), default);

        result.Value.LateDays.Should().Be(2);
        result.Value.LateFee.Should().Be(60m);
        result.Value.DepositRefunded.Should().Be(40m);
        result.Value.Destination.Should().Be(RentalRefundDestination.Wallet);
    }

    [Fact]
    public async Task Return_WhenReserved_FailsInvalidTransition()
    {
        var (bookings, items, wallets, payments, gateway, uow) = Mocks();
        var item = RentableItem.Create("Furadeira", 5, 30m);
        var b = RentalIn(RentalLifecycle.Reserved, item,
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(4));
        b.Confirm();
        bookings.Setup(r => r.GetByIdAsync(b.Id, It.IsAny<CancellationToken>())).ReturnsAsync(b);

        var handler = ReturnHandler(bookings, items, wallets, payments, gateway, uow);
        var result = await handler.Handle(new MarkRentalReturnedCommand(b.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Rental.InvalidLifecycleTransition");
    }

    // ── Return: estorno da caução no gateway ────────────────────────────────────

    [Fact]
    public async Task Return_RefundToGateway_WithApprovedPayment_RefundsToGateway()
    {
        var (bookings, items, wallets, payments, gateway, uow) = Mocks();
        var item = RentableItem.Create("Furadeira", 5, 30m, securityDeposit: 100m);
        var b = RentalIn(RentalLifecycle.PickedUp, item,
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(4), deposit: 100m);
        bookings.Setup(r => r.GetByIdAsync(b.Id, It.IsAny<CancellationToken>())).ReturnsAsync(b);
        payments.Setup(r => r.GetByBookingIdAsync(b.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ApprovedPayment(b.Id, amount: 190m, deposit: 100m));
        gateway.Setup(g => g.RefundAsync("mp-123", 100m, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new RefundResult(true, null));

        var handler = ReturnHandler(bookings, items, wallets, payments, gateway, uow);
        var result = await handler.Handle(new MarkRentalReturnedCommand(b.Id, RefundToGateway: true), default);

        result.Value.DepositRefunded.Should().Be(100m);
        result.Value.Destination.Should().Be(RentalRefundDestination.Gateway);
        gateway.Verify(g => g.RefundAsync("mp-123", 100m, It.IsAny<CancellationToken>()), Times.Once);
        // Estorno no gateway → carteira NÃO é creditada.
        wallets.Verify(r => r.Add(It.IsAny<WalletEntity>()), Times.Never);
        wallets.Verify(r => r.Update(It.IsAny<WalletEntity>()), Times.Never);
    }

    [Fact]
    public async Task Return_RefundToGateway_WhenGatewayFails_FallsBackToWallet()
    {
        var (bookings, items, wallets, payments, gateway, uow) = Mocks();
        var item = RentableItem.Create("Furadeira", 5, 30m, securityDeposit: 100m);
        var b = RentalIn(RentalLifecycle.PickedUp, item,
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(4), deposit: 100m);
        bookings.Setup(r => r.GetByIdAsync(b.Id, It.IsAny<CancellationToken>())).ReturnsAsync(b);
        payments.Setup(r => r.GetByBookingIdAsync(b.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ApprovedPayment(b.Id, amount: 190m, deposit: 100m));
        gateway.Setup(g => g.RefundAsync("mp-123", 100m, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new RefundResult(false, "gateway indisponível"));
        wallets.Setup(r => r.GetByUserIdAsync(b.CustomerId, It.IsAny<CancellationToken>()))
               .ReturnsAsync((WalletEntity?)null);
        WalletEntity? added = null;
        wallets.Setup(r => r.Add(It.IsAny<WalletEntity>())).Callback<WalletEntity>(w => added = w);

        var handler = ReturnHandler(bookings, items, wallets, payments, gateway, uow);
        var result = await handler.Handle(new MarkRentalReturnedCommand(b.Id, RefundToGateway: true), default);

        result.Value.Destination.Should().Be(RentalRefundDestination.Wallet);
        added.Should().NotBeNull();
        added!.Balance.Should().Be(100m);
    }

    [Fact]
    public async Task Return_RefundToGateway_WithoutApprovedPayment_FallsBackToWallet()
    {
        var (bookings, items, wallets, payments, gateway, uow) = Mocks();
        var item = RentableItem.Create("Furadeira", 5, 30m, securityDeposit: 100m);
        var b = RentalIn(RentalLifecycle.PickedUp, item,
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(4), deposit: 100m);
        bookings.Setup(r => r.GetByIdAsync(b.Id, It.IsAny<CancellationToken>())).ReturnsAsync(b);
        // Sem pagamento no gateway (ex.: pago integralmente com carteira).
        payments.Setup(r => r.GetByBookingIdAsync(b.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Payment?)null);
        wallets.Setup(r => r.GetByUserIdAsync(b.CustomerId, It.IsAny<CancellationToken>()))
               .ReturnsAsync((WalletEntity?)null);

        var handler = ReturnHandler(bookings, items, wallets, payments, gateway, uow);
        var result = await handler.Handle(new MarkRentalReturnedCommand(b.Id, RefundToGateway: true), default);

        result.Value.Destination.Should().Be(RentalRefundDestination.Wallet);
        gateway.Verify(g => g.RefundAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
