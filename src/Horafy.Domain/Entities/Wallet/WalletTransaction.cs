using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Wallet;

public sealed class WalletTransaction : BaseEntity
{
    private WalletTransaction() { }

    public Guid WalletId { get; private set; }
    public WalletTransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = default!;
    public Guid? BookingId { get; private set; }

    internal static WalletTransaction Create(
        Guid walletId,
        WalletTransactionType type,
        decimal amount,
        string description,
        Guid? bookingId = null) =>
        new()
        {
            WalletId = walletId,
            Type = type,
            Amount = amount,
            Description = description,
            BookingId = bookingId,
        };
}
