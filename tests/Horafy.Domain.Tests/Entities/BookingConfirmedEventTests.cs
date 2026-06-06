using FluentAssertions;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Events.Bookings;
using Xunit;

namespace Horafy.Domain.Tests.Entities;

public sealed class BookingConfirmedEventTests
{
    [Fact]
    public void Confirm_RaisesBookingConfirmedEvent()
    {
        var booking = Booking.Create(
            serviceId:       Guid.NewGuid(),
            resourceId:      Guid.NewGuid(),
            customerId:      Guid.NewGuid(),
            customerName:    "João Cliente",
            customerEmail:   "joao@test.com",
            scheduledAt:     DateTimeOffset.UtcNow.AddHours(2),
            durationMinutes: 60);

        booking.Confirm();

        var evt = booking.DomainEvents.OfType<BookingConfirmedEvent>().Single();
        evt.BookingId.Should().Be(booking.Id);
        evt.CustomerEmail.Should().Be("joao@test.com");
        evt.CustomerName.Should().Be("João Cliente");
        evt.ScheduledAt.Should().Be(booking.ScheduledAt);
    }

    [Fact]
    public void Confirm_AlreadyConfirmed_ThrowsInvalidOperationException()
    {
        var booking = Booking.Create(
            serviceId:       Guid.NewGuid(),
            resourceId:      Guid.NewGuid(),
            customerId:      Guid.NewGuid(),
            customerName:    "Maria",
            customerEmail:   "maria@test.com",
            scheduledAt:     DateTimeOffset.UtcNow.AddHours(2),
            durationMinutes: 30);

        booking.Confirm();
        var act = () => booking.Confirm();

        act.Should().Throw<InvalidOperationException>();
    }
}
