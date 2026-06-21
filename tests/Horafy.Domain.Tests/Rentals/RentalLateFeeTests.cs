using FluentAssertions;
using Horafy.Domain.Entities.Rentals;
using Xunit;

namespace Horafy.Domain.Tests.Rentals;

public sealed class RentalLateFeeTests
{
    [Fact]
    public void LateDays_OnTimeOrEarly_IsZero()
    {
        var planned = new DateOnly(2026, 6, 10);
        RentalPricing.LateDays(planned, planned).Should().Be(0);
        RentalPricing.LateDays(planned, planned.AddDays(-1)).Should().Be(0);
    }

    [Fact]
    public void LateDays_Late_ReturnsDelta()
    {
        var planned = new DateOnly(2026, 6, 10);
        RentalPricing.LateDays(planned, planned.AddDays(3)).Should().Be(3);
    }

    [Fact]
    public void CalculateLateFee_MultipliesRateDaysQuantity()
    {
        // R$ 30/dia × 3 dias × 2 unidades = R$ 180
        RentalPricing.CalculateLateFee(30m, 3, 2).Should().Be(180m);
    }

    [Fact]
    public void CalculateLateFee_ZeroLateDays_IsZero()
    {
        RentalPricing.CalculateLateFee(30m, 0, 1).Should().Be(0m);
    }

    [Theory]
    [InlineData(-1, 1, 1, "dailyLateFee")]
    [InlineData(10, -1, 1, "lateDays")]
    [InlineData(10, 1, 0, "quantity")]
    public void CalculateLateFee_InvalidArgs_Throws(decimal fee, int lateDays, int qty, string param)
    {
        var act = () => RentalPricing.CalculateLateFee(fee, lateDays, qty);
        act.Should().Throw<ArgumentException>().WithParameterName(param);
    }
}
