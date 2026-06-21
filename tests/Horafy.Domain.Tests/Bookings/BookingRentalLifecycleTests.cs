using FluentAssertions;
using Horafy.Domain.Entities.Bookings;
using Xunit;

namespace Horafy.Domain.Tests.Bookings;

public sealed class BookingRentalLifecycleTests
{
    private static Booking MakeRental(DateTimeOffset? start = null, DateTimeOffset? end = null)
    {
        var s = start ?? DateTimeOffset.UtcNow.AddDays(1);
        var e = end ?? s.AddDays(3);
        return Booking.CreateRental(
            new[] { (Guid.NewGuid(), "Furadeira", 1, 90m) },
            customerId: Guid.NewGuid(), customerName: "Cliente", customerEmail: "c@test.com",
            startsAt: s, endsAt: e);
    }

    private static Booking MakeConfirmedRental()
    {
        var b = MakeRental();
        b.Confirm();
        return b;
    }

    [Fact]
    public void CreateRental_StartsAsReserved()
    {
        MakeRental().RentalStatus.Should().Be(RentalLifecycle.Reserved);
    }

    [Fact]
    public void MarkRentalPickedUp_FromConfirmedReserved_Succeeds()
    {
        var b = MakeConfirmedRental();
        b.MarkRentalPickedUp();
        b.RentalStatus.Should().Be(RentalLifecycle.PickedUp);
    }

    [Fact]
    public void MarkRentalPickedUp_WhenNotConfirmed_Throws()
    {
        var b = MakeRental(); // Pending
        var act = () => b.MarkRentalPickedUp();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkRentalPickedUp_OnAppointment_Throws()
    {
        var appt = Booking.Create(
            services: new[] { (Guid.NewGuid(), "Corte", 30, 50m) },
            resourceId: Guid.NewGuid(), resourceName: "R", customerId: Guid.NewGuid(),
            customerName: "C", customerEmail: "c@test.com",
            scheduledAt: DateTimeOffset.UtcNow.AddHours(2));

        var act = () => appt.MarkRentalPickedUp();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkRentalReturned_FromPickedUp_SetsReturnedAndCompleted()
    {
        var b = MakeConfirmedRental();
        b.MarkRentalPickedUp();

        var when = DateTimeOffset.UtcNow;
        b.MarkRentalReturned(when);

        b.RentalStatus.Should().Be(RentalLifecycle.Returned);
        b.Status.Should().Be(BookingStatus.Completed);
        b.CompletedAt.Should().Be(when);
    }

    [Fact]
    public void MarkRentalReturned_PersistsLateFeeAndRefund()
    {
        var b = MakeConfirmedRental();
        b.MarkRentalPickedUp();

        b.MarkRentalReturned(DateTimeOffset.UtcNow, lateFee: 15m, depositRefunded: 35m);

        b.LateFee.Should().Be(15m);
        b.DepositRefunded.Should().Be(35m);
    }

    [Fact]
    public void MarkRentalReturned_WhenNotPickedUp_Throws()
    {
        var b = MakeConfirmedRental(); // Reserved
        var act = () => b.MarkRentalReturned(DateTimeOffset.UtcNow);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkRentalReturned_DoesNotRaiseCompletedEvent()
    {
        var b = MakeConfirmedRental();
        b.MarkRentalPickedUp();
        b.ClearDomainEvents();

        b.MarkRentalReturned(DateTimeOffset.UtcNow);

        // Locação não gera bônus de fidelidade — nenhum evento de conclusão.
        b.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void IsOverdue_PickedUpPastEnd_IsTrue()
    {
        var b = MakeRental(
            start: DateTimeOffset.UtcNow.AddDays(-5),
            end:   DateTimeOffset.UtcNow.AddDays(-2));
        b.Confirm();
        b.MarkRentalPickedUp();

        b.IsOverdue(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void IsOverdue_Reserved_IsFalse()
    {
        var b = MakeRental(
            start: DateTimeOffset.UtcNow.AddDays(-5),
            end:   DateTimeOffset.UtcNow.AddDays(-2));

        b.IsOverdue(DateTimeOffset.UtcNow).Should().BeFalse();
    }
}
