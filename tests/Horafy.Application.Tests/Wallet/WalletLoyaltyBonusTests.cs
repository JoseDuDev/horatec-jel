using FluentAssertions;
using Horafy.Domain.Entities.Wallet;
using WalletEntity = Horafy.Domain.Entities.Wallet.Wallet;
using Xunit;

namespace Horafy.Application.Tests.Wallet;

public sealed class WalletLoyaltyBonusTests
{
    private static WalletEntity MakeWallet(decimal balance = 0)
    {
        var w = WalletEntity.Create(Guid.NewGuid());
        if (balance > 0) w.AddCredits(balance, "Setup");
        return w;
    }

    [Fact]
    public void AddLoyaltyBonus_ValidAmount_IncreasesBalanceWithLoyaltyType()
    {
        var wallet    = MakeWallet();
        var bookingId = Guid.NewGuid();

        var result = wallet.AddLoyaltyBonus(5m, "Bônus de fidelidade", bookingId);

        result.IsSuccess.Should().BeTrue();
        wallet.Balance.Should().Be(5m);
        wallet.Transactions.Should().HaveCount(1);
        wallet.Transactions[0].Type.Should().Be(WalletTransactionType.LoyaltyBonus);
        wallet.Transactions[0].Amount.Should().Be(5m);
        wallet.Transactions[0].BookingId.Should().Be(bookingId);
    }

    [Fact]
    public void AddLoyaltyBonus_ZeroAmount_ReturnsFailure()
    {
        var wallet = MakeWallet();

        var result = wallet.AddLoyaltyBonus(0m, "Bônus", Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Wallet.InvalidAmount");
        wallet.Balance.Should().Be(0m);
    }
}
