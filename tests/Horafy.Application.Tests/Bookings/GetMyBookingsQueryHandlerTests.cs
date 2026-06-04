using FluentAssertions;
using Horafy.Application.Features.Bookings.Queries;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Bookings;

public sealed class GetMyBookingsQueryHandlerTests
{
    private readonly Mock<IBookingRepository>  _bookingRepo = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetMyBookingsQueryHandler MakeHandler() =>
        new(_bookingRepo.Object, _currentUser.Object);

    [Fact]
    public async Task Handle_AuthenticatedUser_ReturnsOwnBookings()
    {
        var userId = Guid.NewGuid();
        var booking = Booking.Create(
            Service.Create("Corte", 60, 50m).Id,
            Resource.Create("João", ResourceType.Professional).Id,
            userId, "Cliente", "cliente@test.com",
            DateTimeOffset.UtcNow.AddHours(2), 60);

        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _bookingRepo.Setup(r => r.GetByCustomerAsync(userId, default))
                    .ReturnsAsync(new List<Booking> { booking });

        var result = await MakeHandler().Handle(new GetMyBookingsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].CustomerId.Should().Be(userId);
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsUnauthorized()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(false);
        _currentUser.SetupGet(u => u.UserId).Returns((Guid?)null);

        var result = await MakeHandler().Handle(new GetMyBookingsQuery(), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(Horafy.Shared.ErrorType.Unauthorized);
    }
}
