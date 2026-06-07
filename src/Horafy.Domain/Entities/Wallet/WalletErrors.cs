using Horafy.Shared;

namespace Horafy.Domain.Entities.Wallet;

public static class WalletErrors
{
    public static readonly Error InvalidAmount       = new("Wallet.InvalidAmount",       "O valor deve ser maior que zero.", ErrorType.Validation);
    public static readonly Error DescriptionRequired = new("Wallet.DescriptionRequired", "A descrição é obrigatória.",       ErrorType.Validation);
    public static readonly Error InsufficientBalance = new("Wallet.InsufficientBalance", "Saldo insuficiente.",              ErrorType.Validation);
}
