using System.Linq.Expressions;
using FluentAssertions;
using Horafy.Application.Features.Rentals.Queries;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Rentals;

public class GetRentalFinancialSummaryQueryHandlerTests
{
    private static Booking ActiveRental(decimal rental, decimal deposit)
    {
        var b = Booking.CreateRental(
            new[] { (Guid.NewGuid(), "Item", 1, rental) },
            customerId: Guid.NewGuid(), customerName: "C", customerEmail: "c@test.com",
            startsAt: DateTimeOffset.UtcNow.AddDays(1), endsAt: DateTimeOffset.UtcNow.AddDays(3),
            securityDeposit: deposit);
        b.Confirm();
        return b;
    }

    private static Booking ReturnedRental(decimal rental, decimal deposit, decimal lateFee, decimal refund)
    {
        var b = Booking.CreateRental(
            new[] { (Guid.NewGuid(), "Item", 1, rental) },
            customerId: Guid.NewGuid(), customerName: "C", customerEmail: "c@test.com",
            startsAt: DateTimeOffset.UtcNow.AddDays(-5), endsAt: DateTimeOffset.UtcNow.AddDays(-2),
            securityDeposit: deposit);
        b.Confirm();
        b.MarkRentalPickedUp();
        b.MarkRentalReturned(DateTimeOffset.UtcNow, lateFee, refund);
        return b;
    }

    [Fact]
    public async Task Handle_AggregatesRevenueDepositsAndFees()
    {
        var active   = ActiveRental(rental: 90m, deposit: 50m);
        var returned = ReturnedRental(rental: 60m, deposit: 40m, lateFee: 10m, refund: 30m);

        var repo = new Mock<IBookingRepository>();
        repo.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<Booking, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { active, returned });

        var handler = new GetRentalFinancialSummaryQueryHandler(repo.Object);
        var result = await handler.Handle(
            new GetRentalFinancialSummaryQuery(DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow), default);

        result.IsSuccess.Should().BeTrue();
        var s = result.Value;
        s.RentalCount.Should().Be(2);
        s.RentalRevenue.Should().Be(150m);     // 90 + 60
        s.LateFeesCollected.Should().Be(10m);
        s.DepositsCharged.Should().Be(90m);     // 50 + 40
        s.DepositsRefunded.Should().Be(30m);
        s.DepositsHeld.Should().Be(60m);        // 90 − 30
        s.NetRevenue.Should().Be(160m);         // 150 + 10
    }

    [Fact]
    public async Task Handle_NoRentals_ReturnsZeros()
    {
        var repo = new Mock<IBookingRepository>();
        repo.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<Booking, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        var handler = new GetRentalFinancialSummaryQueryHandler(repo.Object);
        var result = await handler.Handle(
            new GetRentalFinancialSummaryQuery(DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow), default);

        result.Value.RentalCount.Should().Be(0);
        result.Value.NetRevenue.Should().Be(0m);
    }
}
