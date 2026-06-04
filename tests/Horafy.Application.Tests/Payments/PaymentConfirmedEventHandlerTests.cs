using FluentAssertions;
using Horafy.Application.Features.Payments.EventHandlers;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Events.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Payments;

public sealed class PaymentConfirmedEventHandlerTests
{
    private readonly Mock<IBookingRepository> _bookingRepo = new();
    private readonly Mock<ITenantUnitOfWork>  _unitOfWork  = new();

    private PaymentConfirmedEventHandler MakeHandler() =>
        new(_bookingRepo.Object, _unitOfWork.Object);

    private static Booking MakePendingBooking()
    {
        var service  = Service.Create("Corte", 60, 100m);
        var resource = Resource.Create("João", ResourceType.Professional);
        return Booking.Create(
            new[] { (service.Id, "Corte", 60) },
            resource.Id,
            Guid.NewGuid(), "Cliente", "cliente@test.com",
            DateTimeOffset.UtcNow.AddHours(2));
    }

    [Fact]
    public async Task Handle_FullPayment_ConfirmsBookingAndMarksPaid()
    {
        var booking = MakePendingBooking();
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);

        await MakeHandler().Handle(
            new PaymentConfirmedEvent(Guid.NewGuid(), booking.Id, IsDeposit: false), default);

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.PaymentStatus.Should().Be(BookingPaymentStatus.Paid);
    }

    [Fact]
    public async Task Handle_DepositPayment_ConfirmsBookingAndMarksPartiallyPaid()
    {
        var booking = MakePendingBooking();
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);

        await MakeHandler().Handle(
            new PaymentConfirmedEvent(Guid.NewGuid(), booking.Id, IsDeposit: true), default);

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.PaymentStatus.Should().Be(BookingPaymentStatus.PartiallyPaid);
    }
}
