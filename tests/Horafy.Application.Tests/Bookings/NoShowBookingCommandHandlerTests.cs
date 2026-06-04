using FluentAssertions;
using Horafy.Application.Features.Bookings.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Bookings;

public sealed class NoShowBookingCommandHandlerTests
{
    private readonly Mock<IBookingRepository> _bookingRepo = new();
    private readonly Mock<ITenantUnitOfWork>  _unitOfWork  = new();

    private NoShowBookingCommandHandler MakeHandler() =>
        new(_bookingRepo.Object, _unitOfWork.Object);

    private static Booking MakeConfirmedBooking()
    {
        var b = Booking.Create(
            Service.Create("Corte", 60, 50m).Id,
            Resource.Create("João", ResourceType.Professional).Id,
            Guid.NewGuid(), "Cliente", "cliente@test.com",
            DateTimeOffset.UtcNow.AddHours(2), 60);
        b.Confirm();
        return b;
    }

    [Fact]
    public async Task Handle_ConfirmedBooking_ReturnsSuccessAndSetsNoShowStatus()
    {
        var booking = MakeConfirmedBooking();
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);

        var result = await MakeHandler().Handle(new NoShowBookingCommand(booking.Id), default);

        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.NoShow);
    }

    [Fact]
    public async Task Handle_BookingNotFound_ReturnsNotFoundError()
    {
        _bookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
                    .ReturnsAsync((Booking?)null);

        var result = await MakeHandler().Handle(new NoShowBookingCommand(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.NotFound");
    }

    [Fact]
    public async Task Handle_PendingBooking_ThrowsInvalidOperationException()
    {
        var booking = Booking.Create(
            Service.Create("Corte", 60, 50m).Id,
            Resource.Create("João", ResourceType.Professional).Id,
            Guid.NewGuid(), "Cliente", "cliente@test.com",
            DateTimeOffset.UtcNow.AddHours(2), 60);
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);

        var act = () => MakeHandler().Handle(new NoShowBookingCommand(booking.Id), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
