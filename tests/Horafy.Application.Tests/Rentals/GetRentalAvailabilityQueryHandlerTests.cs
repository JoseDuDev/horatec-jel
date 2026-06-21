using FluentAssertions;
using Horafy.Application.Features.Rentals;
using Horafy.Application.Features.Rentals.Queries;
using Horafy.Domain.Entities.Rentals;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Rentals;

public class GetRentalAvailabilityQueryHandlerTests
{
    private static readonly Guid ItemId    = Guid.NewGuid();
    private static readonly DateOnly Start  = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));
    private static readonly DateOnly End    = Start.AddDays(3);

    private static (GetRentalAvailabilityQueryHandler handler,
                    Mock<IRentableItemRepository> itemRepo,
                    Mock<IBookingRepository> bookingRepo) BuildHandler()
    {
        var itemRepo    = new Mock<IRentableItemRepository>();
        var bookingRepo = new Mock<IBookingRepository>();
        return (new GetRentalAvailabilityQueryHandler(itemRepo.Object, bookingRepo.Object),
                itemRepo, bookingRepo);
    }

    private static void SetupItem(Mock<IRentableItemRepository> repo, RentableItem item) =>
        repo.Setup(r => r.GetByIdAsync(ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(item);

    private static void SetupReserved(Mock<IBookingRepository> repo, int reserved) =>
        repo.Setup(r => r.CountReservedUnitsAsync(
                ItemId, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reserved);

    [Fact]
    public async Task Handle_FullStockNoReservations_ReturnsAllAvailable()
    {
        var (handler, itemRepo, bookingRepo) = BuildHandler();
        SetupItem(itemRepo, RentableItem.Create("Furadeira", quantity: 5, dailyRate: 30m));
        SetupReserved(bookingRepo, 0);

        var result = await handler.Handle(new GetRentalAvailabilityQuery(ItemId, Start, End), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalQuantity.Should().Be(5);
        result.Value.ReservedUnits.Should().Be(0);
        result.Value.AvailableUnits.Should().Be(5);
        result.Value.Days.Should().Be(3);
        result.Value.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PartialReservations_ReturnsRemaining()
    {
        var (handler, itemRepo, bookingRepo) = BuildHandler();
        SetupItem(itemRepo, RentableItem.Create("Furadeira", quantity: 5, dailyRate: 30m));
        SetupReserved(bookingRepo, 3);

        var result = await handler.Handle(new GetRentalAvailabilityQuery(ItemId, Start, End), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AvailableUnits.Should().Be(2);
        result.Value.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_StockExhausted_IsNotAvailable()
    {
        var (handler, itemRepo, bookingRepo) = BuildHandler();
        SetupItem(itemRepo, RentableItem.Create("Furadeira", quantity: 2, dailyRate: 30m));
        SetupReserved(bookingRepo, 2);

        var result = await handler.Handle(new GetRentalAvailabilityQuery(ItemId, Start, End), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AvailableUnits.Should().Be(0);
        result.Value.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_OverReserved_ClampsAvailableToZero()
    {
        var (handler, itemRepo, bookingRepo) = BuildHandler();
        SetupItem(itemRepo, RentableItem.Create("Furadeira", quantity: 2, dailyRate: 30m));
        SetupReserved(bookingRepo, 5);

        var result = await handler.Handle(new GetRentalAvailabilityQuery(ItemId, Start, End), default);

        result.Value.AvailableUnits.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PassesItemBufferDaysToRepository()
    {
        var (handler, itemRepo, bookingRepo) = BuildHandler();
        SetupItem(itemRepo, RentableItem.Create("Furadeira", quantity: 1, dailyRate: 30m, bufferDays: 2));
        SetupReserved(bookingRepo, 0);

        await handler.Handle(new GetRentalAvailabilityQuery(ItemId, Start, End), default);

        bookingRepo.Verify(r => r.CountReservedUnitsAsync(
            ItemId, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
            2, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EndBeforeOrEqualStart_FailsInvalidPeriod()
    {
        var (handler, _, _) = BuildHandler();

        var result = await handler.Handle(new GetRentalAvailabilityQuery(ItemId, Start, Start), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RentalErrors.InvalidPeriod);
    }

    [Fact]
    public async Task Handle_StartInPast_FailsPastDate()
    {
        var (handler, _, _) = BuildHandler();
        var past = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));

        var result = await handler.Handle(
            new GetRentalAvailabilityQuery(ItemId, past, past.AddDays(2)), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RentalErrors.PastDate);
    }

    [Fact]
    public async Task Handle_ItemNotFound_Fails()
    {
        var (handler, itemRepo, _) = BuildHandler();
        itemRepo.Setup(r => r.GetByIdAsync(ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RentableItem?)null);

        var result = await handler.Handle(new GetRentalAvailabilityQuery(ItemId, Start, End), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RentalErrors.ItemNotFound);
    }

    [Fact]
    public async Task Handle_InactiveItem_Fails()
    {
        var (handler, itemRepo, _) = BuildHandler();
        var item = RentableItem.Create("Furadeira", quantity: 1, dailyRate: 30m);
        item.Deactivate();
        SetupItem(itemRepo, item);

        var result = await handler.Handle(new GetRentalAvailabilityQuery(ItemId, Start, End), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RentalErrors.ItemInactive);
    }
}
