using FluentAssertions;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Entities.Tenants;
using Xunit;

namespace Horafy.Application.Tests.Payments;

public sealed class PaymentSettingsTests
{
    [Fact]
    public void CalculateDepositAmount_Percentage30_Returns30PercentOfTotal()
    {
        var settings = PaymentSettings.Create(true, DepositMode.Percentage, 30m);
        settings.CalculateDepositAmount(100m).Should().Be(30m);
    }

    [Fact]
    public void CalculateDepositAmount_FixedAmount50_ReturnsFifty()
    {
        var settings = PaymentSettings.Create(true, DepositMode.FixedAmount, 50m);
        settings.CalculateDepositAmount(200m).Should().Be(50m);
    }

    [Fact]
    public void CalculateDepositAmount_FixedAmountExceedsTotal_ReturnsTotalAmount()
    {
        var settings = PaymentSettings.Create(true, DepositMode.FixedAmount, 200m);
        settings.CalculateDepositAmount(100m).Should().Be(100m);
    }

    [Fact]
    public void CalculateDepositAmount_None_ReturnsZero()
    {
        var settings = PaymentSettings.Default;
        settings.CalculateDepositAmount(100m).Should().Be(0m);
    }

    [Fact]
    public void Create_InvalidPercentage_ThrowsArgumentException()
    {
        var act = () => PaymentSettings.Create(true, DepositMode.Percentage, 150m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_NegativeFixedAmount_ThrowsArgumentException()
    {
        var act = () => PaymentSettings.Create(true, DepositMode.FixedAmount, -1m);
        act.Should().Throw<ArgumentException>();
    }
}
