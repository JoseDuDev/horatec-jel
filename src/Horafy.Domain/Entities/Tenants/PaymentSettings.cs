using Horafy.Domain.Entities.Payments;

namespace Horafy.Domain.Entities.Tenants;

public sealed class PaymentSettings
{
    private PaymentSettings() { }

    public bool        RequiresPayment { get; private set; }
    public DepositMode DepositMode     { get; private set; }
    public decimal     DepositValue    { get; private set; }

    public static readonly PaymentSettings Default =
        new() { RequiresPayment = false, DepositMode = DepositMode.None, DepositValue = 0m };

    public static PaymentSettings Create(bool requiresPayment, DepositMode mode, decimal value)
    {
        if (mode == DepositMode.Percentage && (value < 0 || value > 100))
            throw new ArgumentException("Percentual deve estar entre 0 e 100.", nameof(value));
        if (mode == DepositMode.FixedAmount && value < 0)
            throw new ArgumentException("Valor fixo não pode ser negativo.", nameof(value));
        if (mode == DepositMode.None) value = 0m;
        return new() { RequiresPayment = requiresPayment, DepositMode = mode, DepositValue = value };
    }

    public decimal CalculateDepositAmount(decimal totalAmount) => DepositMode switch
    {
        DepositMode.Percentage  => Math.Round(totalAmount * DepositValue / 100, 2),
        DepositMode.FixedAmount => Math.Min(DepositValue, totalAmount),
        _                       => 0m
    };
}
