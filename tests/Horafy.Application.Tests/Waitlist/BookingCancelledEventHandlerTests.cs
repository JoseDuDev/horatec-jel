using FluentAssertions;
using Horafy.Application.Features.Waitlist.EventHandlers;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Waitlist;

public sealed class BookingCancelledEventHandlerTests
{
    private readonly Mock<IBookingRepository>  _bookingRepo  = new();
    private readonly Mock<IWaitlistRepository> _waitlistRepo = new();
    private readonly Mock<ITenantUnitOfWork>   _unitOfWork   = new();

    private BookingCancelledEventHandler MakeHandler() =>
        new(_bookingRepo.Object, _waitlistRepo.Object, _unitOfWork.Object);

    private static Booking MakeCancelledBooking(Guid serviceId, Guid resourceId, DateTimeOffset scheduledAt)
    {
        var b = Booking.Create(serviceId, resourceId,
            Guid.NewGuid(), "Cliente", "c@test.com", scheduledAt, 60);
        b.Confirm();
        b.Cancel();
        return b;
    }

    [Fact]
    public async Task Handle_WaitingEntriesExist_PromotesFirst()
    {
        var serviceId  = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var scheduled  = DateTimeOffset.UtcNow.AddDays(3);
        var date       = DateOnly.FromDateTime(scheduled.Date);
        var booking    = MakeCancelledBooking(serviceId, resourceId, scheduled);

        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);

        var entry = WaitlistEntry.Create(serviceId, resourceId, Guid.NewGuid(), "A", "a@test.com", date);
        _waitlistRepo
            .Setup(r => r.GetByServiceResourceDateAsync(serviceId, resourceId, date, default))
            .ReturnsAsync(new List<WaitlistEntry> { entry });

        var domainEvent = new BookingCancelledEvent(booking.Id, booking.CustomerId, null);

        await MakeHandler().Handle(domainEvent, default);

        entry.Status.Should().Be(WaitlistStatus.Notified);
        _waitlistRepo.Verify(r => r.Update(entry), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_NoWaitingEntries_SkipsSave()
    {
        var serviceId  = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var scheduled  = DateTimeOffset.UtcNow.AddDays(3);
        var date       = DateOnly.FromDateTime(scheduled.Date);
        var booking    = MakeCancelledBooking(serviceId, resourceId, scheduled);

        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _waitlistRepo
            .Setup(r => r.GetByServiceResourceDateAsync(serviceId, resourceId, date, default))
            .ReturnsAsync(new List<WaitlistEntry>());

        var domainEvent = new BookingCancelledEvent(booking.Id, booking.CustomerId, null);

        await MakeHandler().Handle(domainEvent, default);

        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }
}
