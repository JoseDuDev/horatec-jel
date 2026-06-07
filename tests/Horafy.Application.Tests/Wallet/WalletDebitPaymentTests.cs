using FluentAssertions;
using Horafy.Domain.Entities.Wallet;
using WalletEntity = Horafy.Domain.Entities.Wallet.Wallet;
using Xunit;

namespace Horafy.Application.Tests.Wallet;

public sealed class WalletDebitPaymentTests
{
    private static WalletEntity MakeWallet(decimal initialBalance)
    {
        var w = WalletEntity.Create(Guid.NewGuid());
        if (initialBalance > 0) w.AddCredits(initialBalance, "Setup");
        return w;
    }

    [Fact]
    public void DebitPayment_ValidAmount_DecreasesBalanceAndAddsTransaction()
    {
        var wallet = MakeWallet(100m);

        var bookingId = Guid.NewGuid();

        var result = wallet.DebitPayment(30m, "Pagamento de agendamento", bookingId);

        result.IsSuccess.Should().BeTrue();
        wallet.Balance.Should().Be(70m);
        wallet.Transactions.Should().HaveCount(2); // AddCredits + DebitPayment
        wallet.Transactions[1].Type.Should().Be(WalletTransactionType.BookingPayment);
        wallet.Transactions[1].Amount.Should().Be(30m);
        wallet.Transactions[1].BookingId.Should().Be(bookingId);
    }

    [Fact]
    public void DebitPayment_InsufficientBalance_ReturnsFailure()
    {
        var wallet = MakeWallet(20m);

        var result = wallet.DebitPayment(50m, "Pagamento", Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Wallet.InsufficientBalance");
        wallet.Balance.Should().Be(20m);
    }

    [Fact]
    public void DebitPayment_ZeroAmount_ReturnsFailure()
    {
        var wallet = MakeWallet(100m);

        var result = wallet.DebitPayment(0m, "Inválido", Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Wallet.InvalidAmount");
    }
}
