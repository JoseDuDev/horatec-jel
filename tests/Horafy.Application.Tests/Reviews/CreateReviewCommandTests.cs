using FluentAssertions;
using Horafy.Application.Features.Reviews.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Reviews;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Reviews;

public sealed class CreateReviewCommandTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IBookingRepository>  _bookingRepo = new();
    private readonly Mock<IReviewRepository>   _reviewRepo  = new();
    private readonly Mock<ITenantUnitOfWork>   _uow         = new();

    private CreateReviewCommandHandler MakeHandler() =>
        new(_currentUser.Object, _bookingRepo.Object, _reviewRepo.Object, _uow.Object);

    private static Booking MakeCompletedBooking(Guid customerId, Guid resourceId)
    {
        var b = Booking.Create(
            services: new[] { (Guid.NewGuid(), "Corte", 30) },
            resourceId: resourceId,
            customerId: customerId,
            customerName: "João",
            customerEmail: "j@test.com",
            scheduledAt: DateTimeOffset.UtcNow.AddHours(2));
        b.Confirm();
        b.Complete();
        b.ClearDomainEvents();
        return b;
    }

    [Fact]
    public async Task Handle_ValidReview_CreatesAndSaves()
    {
        var customerId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns((Guid?)customerId);

        var booking = MakeCompletedBooking(customerId, resourceId);
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _reviewRepo.Setup(r => r.GetByBookingAsync(booking.Id, default))
            .ReturnsAsync((Review?)null);

        var result = await MakeHandler().Handle(
            new CreateReviewCommand(booking.Id, Stars: 5, Comment: "Ótimo!"), default);

        result.IsSuccess.Should().BeTrue();
        _reviewRepo.Verify(r => r.Add(It.IsAny<Review>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_BookingNotFound_ReturnsError()
    {
        _currentUser.Setup(c => c.UserId).Returns((Guid?)Guid.NewGuid());
        _bookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Booking?)null);

        var result = await MakeHandler().Handle(
            new CreateReviewCommand(Guid.NewGuid(), 5, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Review.BookingNotFound");
    }

    [Fact]
    public async Task Handle_BookingBelongsToDifferentCustomer_ReturnsError()
    {
        var customerId = Guid.NewGuid();
        var otherId    = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns((Guid?)customerId);

        var booking = MakeCompletedBooking(otherId, Guid.NewGuid());
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);

        var result = await MakeHandler().Handle(
            new CreateReviewCommand(booking.Id, 5, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Review.NotYourBooking");
    }

    [Fact]
    public async Task Handle_BookingNotCompleted_ReturnsError()
    {
        var customerId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns((Guid?)customerId);

        var booking = Booking.Create(
            services: new[] { (Guid.NewGuid(), "Corte", 30) },
            resourceId: resourceId,
            customerId: customerId,
            customerName: "João",
            customerEmail: "j@test.com",
            scheduledAt: DateTimeOffset.UtcNow.AddHours(2));
        booking.ClearDomainEvents();
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);

        var result = await MakeHandler().Handle(
            new CreateReviewCommand(booking.Id, 5, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Review.BookingNotCompleted");
    }

    [Fact]
    public async Task Handle_AlreadyReviewed_ReturnsError()
    {
        var customerId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns((Guid?)customerId);

        var booking = MakeCompletedBooking(customerId, resourceId);
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);

        var existingReview = Review.Create(booking.Id, resourceId, customerId, 4, "Bom");
        _reviewRepo.Setup(r => r.GetByBookingAsync(booking.Id, default))
            .ReturnsAsync(existingReview);

        var result = await MakeHandler().Handle(
            new CreateReviewCommand(booking.Id, 5, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Review.AlreadyReviewed");
    }
}
