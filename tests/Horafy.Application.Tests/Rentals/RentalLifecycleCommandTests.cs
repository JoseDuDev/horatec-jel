using FluentAssertions;
using Horafy.Application.Features.Rentals;
using Horafy.Application.Features.Rentals.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
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
                    Mock<ITenantUnitOfWork> uow) Mocks()
    {
        var bookings = new Mock<IBookingRepository>();
        var items    = new Mock<IRentableItemRepository>();
        var wallets  = new Mock<IWalletRepository>();
        var uow      = new Mock<ITenantUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return (bookings, items, wallets, uow);
    }

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

    // ── Pickup ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pickup_ConfirmedReserved_Succeeds()
    {
        var (bookings, _, _, uow) = Mocks();
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
        var (bookings, _, _, uow) = Mocks();
        bookings.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Booking?)null);

        var handler = new MarkRentalPickedUpCommandHandler(bookings.Object, uow.Object);
        var result = await handler.Handle(new MarkRentalPickedUpCommand(Guid.NewGuid()), default);

        result.Error.Should().Be(RentalErrors.BookingNotFound);
    }

    [Fact]
    public async Task Pickup_OnAppointment_Fails()
    {
        var (bookings, _, _, uow) = Mocks();
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
        var (bookings, items, wallets, uow) = Mocks();
        var item = RentableItem.Create("Furadeira", 5, 30m);
        var b = RentalIn(RentalLifecycle.PickedUp, item,
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(4));
        bookings.Setup(r => r.GetByIdAsync(b.Id, It.IsAny<CancellationToken>())).ReturnsAsync(b);

        var handler = new MarkRentalReturnedCommandHandler(
            bookings.Object, items.Object, wallets.Object, uow.Object);
        var result = await handler.Handle(new MarkRentalReturnedCommand(b.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.LateDays.Should().Be(0);
        result.Value.LateFee.Should().Be(0m);
        result.Value.DepositRefunded.Should().Be(0m);
        b.RentalStatus.Should().Be(RentalLifecycle.Returned);
        b.Status.Should().Be(BookingStatus.Completed);
    }

    [Fact]
    public async Task Return_WithDeposit_RefundsToWallet()
    {
        var (bookings, items, wallets, uow) = Mocks();
        var item = RentableItem.Create("Furadeira", 5, 30m, securityDeposit: 100m);
        var b = RentalIn(RentalLifecycle.PickedUp, item,
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(4), deposit: 100m);
        bookings.Setup(r => r.GetByIdAsync(b.Id, It.IsAny<CancellationToken>())).ReturnsAsync(b);
        wallets.Setup(r => r.GetByUserIdAsync(b.CustomerId, It.IsAny<CancellationToken>()))
               .ReturnsAsync((WalletEntity?)null);

        WalletEntity? added = null;
        wallets.Setup(r => r.Add(It.IsAny<WalletEntity>())).Callback<WalletEntity>(w => added = w);

        var handler = new MarkRentalReturnedCommandHandler(
            bookings.Object, items.Object, wallets.Object, uow.Object);
        var result = await handler.Handle(new MarkRentalReturnedCommand(b.Id), default);

        result.Value.DepositRefunded.Should().Be(100m);
        added.Should().NotBeNull();
        added!.Balance.Should().Be(100m);
    }

    [Fact]
    public async Task Return_Late_DeductsFeeFromDepositRefund()
    {
        var (bookings, items, wallets, uow) = Mocks();
        var item = RentableItem.Create("Furadeira", 5, 30m, securityDeposit: 100m);
        // devolução prevista 2 dias atrás → multa 2 × R$ 30 = R$ 60; estorno = 100 − 60 = 40
        var b = RentalIn(RentalLifecycle.PickedUp, item,
            DateTimeOffset.UtcNow.AddDays(-5), DateTimeOffset.UtcNow.AddDays(-2), deposit: 100m);
        bookings.Setup(r => r.GetByIdAsync(b.Id, It.IsAny<CancellationToken>())).ReturnsAsync(b);
        items.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[] { item });
        wallets.Setup(r => r.GetByUserIdAsync(b.CustomerId, It.IsAny<CancellationToken>()))
               .ReturnsAsync((WalletEntity?)null);

        var handler = new MarkRentalReturnedCommandHandler(
            bookings.Object, items.Object, wallets.Object, uow.Object);
        var result = await handler.Handle(new MarkRentalReturnedCommand(b.Id), default);

        result.Value.LateDays.Should().Be(2);
        result.Value.LateFee.Should().Be(60m);
        result.Value.DepositRefunded.Should().Be(40m);
    }

    [Fact]
    public async Task Return_WhenReserved_FailsInvalidTransition()
    {
        var (bookings, items, wallets, uow) = Mocks();
        var item = RentableItem.Create("Furadeira", 5, 30m);
        var b = RentalIn(RentalLifecycle.Reserved, item,
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(4));
        b.Confirm();
        bookings.Setup(r => r.GetByIdAsync(b.Id, It.IsAny<CancellationToken>())).ReturnsAsync(b);

        var handler = new MarkRentalReturnedCommandHandler(
            bookings.Object, items.Object, wallets.Object, uow.Object);
        var result = await handler.Handle(new MarkRentalReturnedCommand(b.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Rental.InvalidLifecycleTransition");
    }
}
