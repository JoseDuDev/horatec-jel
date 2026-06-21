using FluentAssertions;
using Horafy.Application.Features.Rentals;
using Horafy.Application.Features.Rentals.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Rentals;
using Horafy.Domain.Entities.Users;
using System.Data;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Rentals;

public class CreateRentalBookingCommandHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Start = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));
    private static readonly DateOnly End   = Start.AddDays(3); // 3 diárias

    private sealed class Harness
    {
        public Mock<IRentableItemRepository> Items   = new();
        public Mock<IBookingRepository>      Bookings = new();
        public Mock<IUserRepository>         Users   = new();
        public Mock<ICurrentUserService>     Current = new();
        public Mock<ITenantUnitOfWork>       Uow     = new();
        public Booking? Captured;

        public Harness()
        {
            Current.SetupGet(c => c.IsAuthenticated).Returns(true);
            Current.SetupGet(c => c.UserId).Returns(UserId);
            Current.SetupGet(c => c.Email).Returns("cliente@test.com");
            Users.Setup(u => u.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((User?)null);
            Bookings.Setup(r => r.Add(It.IsAny<Booking>()))
                    .Callback<Booking>(b => Captured = b);
            Uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            // Executa a operação direto (sem transação real) no teste.
            Uow.Setup(u => u.ExecuteInTransactionAsync(
                    It.IsAny<Func<CancellationToken, Task<Result<Guid>>>>(),
                    It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()))
               .Returns((Func<CancellationToken, Task<Result<Guid>>> op, IsolationLevel _, CancellationToken ct) => op(ct));
        }

        public CreateRentalBookingCommandHandler Build() =>
            new(Items.Object, Bookings.Object, Users.Object, Current.Object, Uow.Object);

        public void SetupItem(RentableItem item) =>
            Items.Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        public void SetupReserved(Guid itemId, int reserved) =>
            Bookings.Setup(r => r.CountReservedUnitsAsync(
                    itemId, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
                    It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(reserved);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesRentalBookingWithPricedLine()
    {
        var h = new Harness();
        var item = RentableItem.Create("Furadeira", quantity: 5, dailyRate: 30m, securityDeposit: 100m);
        h.SetupItem(item);
        h.SetupReserved(item.Id, 0);

        var result = await h.Build().Handle(
            new CreateRentalBookingCommand(
                new[] { new RentalItemLine(item.Id, 2) }, Start, End, "Obra"), default);

        result.IsSuccess.Should().BeTrue();
        h.Captured.Should().NotBeNull();
        h.Captured!.Kind.Should().Be(BookingKind.Rental);
        h.Captured.ServiceId.Should().BeNull();
        h.Captured.ResourceId.Should().BeNull();
        h.Captured.Services.Should().HaveCount(1);

        var line = h.Captured.Services[0];
        line.RentableItemId.Should().Be(item.Id);
        line.Quantity.Should().Be(2);
        // 3 diárias × R$ 30 × 2 unidades = R$ 180
        line.Price.Should().Be(180m);
        h.Captured.TotalAmount.Should().Be(180m);
    }

    [Fact]
    public async Task Handle_Unauthenticated_Fails()
    {
        var h = new Harness();
        h.Current.SetupGet(c => c.IsAuthenticated).Returns(false);

        var result = await h.Build().Handle(
            new CreateRentalBookingCommand(
                new[] { new RentalItemLine(Guid.NewGuid(), 1) }, Start, End, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.Unauthorized);
    }

    [Fact]
    public async Task Handle_ItemNotFound_Fails()
    {
        var h = new Harness();
        h.Items.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((RentableItem?)null);

        var result = await h.Build().Handle(
            new CreateRentalBookingCommand(
                new[] { new RentalItemLine(Guid.NewGuid(), 1) }, Start, End, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RentalErrors.ItemNotFound);
    }

    [Fact]
    public async Task Handle_InsufficientStock_FailsOutOfStock()
    {
        var h = new Harness();
        var item = RentableItem.Create("Furadeira", quantity: 2, dailyRate: 30m);
        h.SetupItem(item);
        h.SetupReserved(item.Id, 1); // 1 disponível, pedem 2

        var result = await h.Build().Handle(
            new CreateRentalBookingCommand(
                new[] { new RentalItemLine(item.Id, 2) }, Start, End, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RentalErrors.OutOfStock);
    }

    [Fact]
    public async Task Handle_InactiveItem_Fails()
    {
        var h = new Harness();
        var item = RentableItem.Create("Furadeira", quantity: 5, dailyRate: 30m);
        item.Deactivate();
        h.SetupItem(item);

        var result = await h.Build().Handle(
            new CreateRentalBookingCommand(
                new[] { new RentalItemLine(item.Id, 1) }, Start, End, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RentalErrors.ItemInactive);
    }

    [Fact]
    public async Task Handle_PastStartDate_Fails()
    {
        var h = new Harness();
        var past = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));

        var result = await h.Build().Handle(
            new CreateRentalBookingCommand(
                new[] { new RentalItemLine(Guid.NewGuid(), 1) }, past, past.AddDays(2), null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RentalErrors.PastDate);
    }
}
