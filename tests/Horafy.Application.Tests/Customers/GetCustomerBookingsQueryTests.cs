using FluentAssertions;
using Horafy.Application.Features.Customers.Queries;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Customers;

public sealed class GetCustomerBookingsQueryTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IBookingRepository>  _bookingRepo = new();

    private GetCustomerBookingsQueryHandler MakeHandler() =>
        new(_currentUser.Object, _bookingRepo.Object);

    [Fact]
    public async Task Handle_CustomerWithBookings_ReturnsAllBookings()
    {
        var customerId = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns((Guid?)customerId);

        var booking = Booking.Create(
            Service.Create("Corte", 30, 50m).Id,
            Resource.Create("Profissional", ResourceType.Professional).Id,
            customerId,
            "João",
            "j@test.com",
            DateTimeOffset.UtcNow.AddHours(2),
            30);
        booking.ClearDomainEvents();

        _bookingRepo.Setup(r => r.GetByCustomerAsync(customerId, default))
            .ReturnsAsync(new List<Booking> { booking });

        var result = await MakeHandler().Handle(new GetCustomerBookingsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].CustomerId.Should().Be(customerId);
    }

    [Fact]
    public async Task Handle_CustomerWithNoBookings_ReturnsEmptyList()
    {
        _currentUser.Setup(c => c.UserId).Returns((Guid?)Guid.NewGuid());
        _bookingRepo.Setup(r => r.GetByCustomerAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(new List<Booking>());

        var result = await MakeHandler().Handle(new GetCustomerBookingsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsUnauthorized()
    {
        _currentUser.Setup(c => c.UserId).Returns((Guid?)null);

        var result = await MakeHandler().Handle(new GetCustomerBookingsQuery(), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(Horafy.Shared.ErrorType.Unauthorized);
    }
}
