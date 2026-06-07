using Horafy.Domain.Entities.Base;
using Horafy.Shared;

namespace Horafy.Domain.Entities.Wallet;

public sealed class Wallet : BaseEntity
{
    private readonly List<WalletTransaction> _transactions = new();
    private Wallet() { }

    public Guid UserId { get; private set; }
    public decimal Balance { get; private set; }
    public IReadOnlyList<WalletTransaction> Transactions => _transactions.AsReadOnly();

    public static Wallet Create(Guid userId) => new() { UserId = userId, Balance = 0 };

    public Result AddCredits(decimal amount, string description)
    {
        if (amount <= 0) return Result.Failure(WalletErrors.InvalidAmount);
        if (string.IsNullOrWhiteSpace(description)) return Result.Failure(WalletErrors.DescriptionRequired);
        Balance += amount;
        _transactions.Add(WalletTransaction.Create(Id, WalletTransactionType.CreditAdded, amount, description));
        return Result.Success();
    }

    public Result RefundFromBooking(decimal amount, string description, Guid bookingId)
    {
        if (amount <= 0) return Result.Failure(WalletErrors.InvalidAmount);
        Balance += amount;
        _transactions.Add(WalletTransaction.Create(Id, WalletTransactionType.BookingRefund, amount, description, bookingId));
        return Result.Success();
    }

    public Result DebitPayment(decimal amount, string description, Guid bookingId)
    {
        if (amount <= 0) return Result.Failure(WalletErrors.InvalidAmount);
        if (Balance < amount) return Result.Failure(WalletErrors.InsufficientBalance);
        Balance -= amount;
        _transactions.Add(WalletTransaction.Create(
            Id, WalletTransactionType.BookingPayment, amount, description, bookingId));
        return Result.Success();
    }

    public Result AddLoyaltyBonus(decimal amount, string description, Guid bookingId)
    {
        if (amount <= 0) return Result.Failure(WalletErrors.InvalidAmount);
        Balance += amount;
        _transactions.Add(WalletTransaction.Create(
            Id, WalletTransactionType.LoyaltyBonus, amount, description, bookingId));
        return Result.Success();
    }
}
