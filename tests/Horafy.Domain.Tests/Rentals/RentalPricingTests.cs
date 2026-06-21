using FluentAssertions;
using Horafy.Domain.Entities.Rentals;
using Xunit;

namespace Horafy.Domain.Tests.Rentals;

public sealed class RentalPricingTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public void DaysBetween_ReturnsDayDifference(int days)
    {
        var start = new DateOnly(2026, 6, 1);
        var end   = start.AddDays(days);

        RentalPricing.DaysBetween(start, end).Should().Be(days);
    }

    [Fact]
    public void DaysBetween_EndNotAfterStart_Throws()
    {
        var d = new DateOnly(2026, 6, 1);
        var act = () => RentalPricing.DaysBetween(d, d);
        act.Should().Throw<ArgumentException>().WithParameterName("end");
    }

    [Fact]
    public void Calculate_SingleUnit_NoDeposit()
    {
        // 3 diárias × R$ 30 × 1 unidade = R$ 90, sem caução
        var quote = RentalPricing.Calculate(dailyRate: 30m, days: 3, quantity: 1, securityDeposit: 0m);

        quote.RentalAmount.Should().Be(90m);
        quote.DepositAmount.Should().Be(0m);
        quote.Total.Should().Be(90m);
    }

    [Fact]
    public void Calculate_MultipleUnitsWithDeposit_SumsRentalAndDeposit()
    {
        // 2 diárias × R$ 50 × 3 unidades = R$ 300 ; caução R$ 100 × 3 = R$ 300
        var quote = RentalPricing.Calculate(dailyRate: 50m, days: 2, quantity: 3, securityDeposit: 100m);

        quote.RentalAmount.Should().Be(300m);
        quote.DepositAmount.Should().Be(300m);
        quote.Total.Should().Be(600m);
    }

    [Fact]
    public void Calculate_RoundsToTwoDecimals()
    {
        // 33.333 × 1 × 1 → 33.33
        var quote = RentalPricing.Calculate(dailyRate: 33.333m, days: 1, quantity: 1, securityDeposit: 0m);

        quote.RentalAmount.Should().Be(33.33m);
    }

    [Fact]
    public void Calculate_ByPeriod_DerivesDays()
    {
        var start = new DateOnly(2026, 6, 1);
        var end   = new DateOnly(2026, 6, 4); // 3 diárias

        var quote = RentalPricing.Calculate(dailyRate: 20m, start, end, quantity: 1, securityDeposit: 0m);

        quote.RentalAmount.Should().Be(60m);
    }

    [Theory]
    [InlineData(-1, 1, 1, 0, "dailyRate")]
    [InlineData(10, 0, 1, 0, "days")]
    [InlineData(10, 1, 0, 0, "quantity")]
    [InlineData(10, 1, 1, -5, "securityDeposit")]
    public void Calculate_InvalidArgs_Throws(
        decimal dailyRate, int days, int quantity, decimal deposit, string param)
    {
        var act = () => RentalPricing.Calculate(dailyRate, days, quantity, deposit);
        act.Should().Throw<ArgumentException>().WithParameterName(param);
    }
}
