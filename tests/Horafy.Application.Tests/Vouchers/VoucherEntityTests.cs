using FluentAssertions;
using Horafy.Domain.Entities.Vouchers;
using Xunit;

namespace Horafy.Application.Tests.Vouchers;

public sealed class VoucherEntityTests
{
    [Fact]
    public void CalculateDiscount_Percentage_ReturnsCorrectAmount()
    {
        var voucher = Voucher.Create("TEST10", VoucherDiscountType.Percentage, 10m, null, null, null);
        var result = voucher.CalculateDiscount(200m);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(20m);
    }

    [Fact]
    public void CalculateDiscount_Fixed_CapsAtTotalPrice()
    {
        var voucher = Voucher.Create("FLAT50", VoucherDiscountType.Fixed, 50m, null, null, null);
        var result = voucher.CalculateDiscount(30m);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(30m);
    }

    [Fact]
    public void CalculateDiscount_AfterDeactivate_ReturnsFailure()
    {
        var voucher = Voucher.Create("OLD", VoucherDiscountType.Fixed, 10m, null, null, null);
        voucher.Deactivate();

        var result = voucher.CalculateDiscount(100m);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Voucher.Inactive");
    }

    [Fact]
    public void CalculateDiscount_Expired_ReturnsFailure()
    {
        var voucher = Voucher.Create("EXP", VoucherDiscountType.Fixed, 10m, null,
            DateTimeOffset.UtcNow.AddDays(-1), null);

        var result = voucher.CalculateDiscount(100m);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Voucher.Expired");
    }

    [Fact]
    public void CalculateDiscount_MaxUsesReached_ReturnsFailure()
    {
        var voucher = Voucher.Create("LIMITED", VoucherDiscountType.Fixed, 10m, null, null, maxUses: 1);
        voucher.Redeem();

        var result = voucher.CalculateDiscount(100m);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Voucher.MaxUsesReached");
    }
}
