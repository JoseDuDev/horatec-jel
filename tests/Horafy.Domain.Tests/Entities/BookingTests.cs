using FluentAssertions;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Events.Bookings;
using Xunit;

namespace Horafy.Domain.Tests.Entities;

public class BookingTests
{
    private static Booking CreateFutureBooking(int minutesFromNow = 60, int duration = 60) =>
        Booking.Create(
            serviceId:       Guid.NewGuid(),
            professionalId:  Guid.NewGuid(),
            customerId:      Guid.NewGuid(),
            customerName:    "João Silva",
            customerEmail:   "joao@gmail.com",
            scheduledAt:     DateTimeOffset.UtcNow.AddMinutes(minutesFromNow),
            durationMinutes: duration);

    // ── Create ────────────────────────────────────────────────────────
    [Fact]
    public void Create_ValidData_ReturnsPendingBooking()
    {
        var booking = CreateFutureBooking();

        booking.Status.Should().Be(BookingStatus.Pending);
        booking.CustomerName.Should().Be("João Silva");
        booking.CustomerEmail.Should().Be("joao@gmail.com");
    }

    [Fact]
    public void Create_SetsEndsAtCorrectly()
    {
        var scheduled = DateTimeOffset.UtcNow.AddHours(2);
        var booking   = Booking.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "João", "joao@gmail.com", scheduled, 90);

        booking.EndsAt.Should().Be(scheduled.AddMinutes(90));
    }

    [Fact]
    public void Create_RaisesBookingCreatedEvent()
    {
        var booking = CreateFutureBooking();

        booking.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BookingCreatedEvent>();
    }

    [Fact]
    public void Create_PastDate_ThrowsArgumentException()
    {
        var act = () => Booking.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "João", "joao@gmail.com",
            scheduledAt: DateTimeOffset.UtcNow.AddMinutes(-1),
            durationMinutes: 60);

        act.Should().Throw<ArgumentException>();
    }

    // ── Confirm ───────────────────────────────────────────────────────
    [Fact]
    public void Confirm_PendingBooking_SetsConfirmedStatus()
    {
        var booking = CreateFutureBooking();
        booking.Confirm();

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ConfirmedAt.Should().NotBeNull();
    }

    [Fact]
    public void Confirm_AlreadyConfirmed_ThrowsInvalidOperation()
    {
        var booking = CreateFutureBooking();
        booking.Confirm();

        booking.Invoking(b => b.Confirm())
            .Should().Throw<InvalidOperationException>();
    }

    // ── Cancel ────────────────────────────────────────────────────────
    [Fact]
    public void Cancel_PendingBooking_SetsCancelledStatus()
    {
        var booking = CreateFutureBooking();
        booking.Cancel("Não posso comparecer");

        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.CancellationReason.Should().Be("Não posso comparecer");
        booking.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_RaisesBookingCancelledEvent()
    {
        var booking = CreateFutureBooking();
        booking.ClearDomainEvents(); // remove o Created

        booking.Cancel("motivo");

        booking.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BookingCancelledEvent>();
    }

    [Fact]
    public void Cancel_AlreadyCancelled_ThrowsInvalidOperation()
    {
        var booking = CreateFutureBooking();
        booking.Cancel();

        booking.Invoking(b => b.Cancel())
            .Should().Throw<InvalidOperationException>();
    }

    // ── OverlapsWith ──────────────────────────────────────────────────
    [Fact]
    public void OverlapsWith_OverlappingRange_ReturnsTrue()
    {
        var start   = DateTimeOffset.UtcNow.AddHours(2);
        var booking = Booking.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "João", "joao@gmail.com", start, 60);

        // Novo agendamento começa 30 min depois e vai até 90 min depois
        booking.OverlapsWith(start.AddMinutes(30), start.AddMinutes(90))
            .Should().BeTrue();
    }

    [Fact]
    public void OverlapsWith_NonOverlappingRange_ReturnsFalse()
    {
        var start   = DateTimeOffset.UtcNow.AddHours(2);
        var booking = Booking.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "João", "joao@gmail.com", start, 60);

        // Novo agendamento começa depois do fim do existente
        booking.OverlapsWith(start.AddMinutes(61), start.AddMinutes(121))
            .Should().BeFalse();
    }

    [Fact]
    public void OverlapsWith_CancelledBooking_ReturnsFalse()
    {
        var start   = DateTimeOffset.UtcNow.AddHours(2);
        var booking = Booking.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "João", "joao@gmail.com", start, 60);
        booking.Cancel();

        booking.OverlapsWith(start, start.AddMinutes(60))
            .Should().BeFalse();
    }
}
