namespace Horafy.Domain.Entities.Tenants;

public sealed class LoyaltySettings
{
    private LoyaltySettings() { }

    public bool    IsEnabled         { get; private set; }
    public decimal CreditRatePercent { get; private set; }
    public decimal MinBookingAmount  { get; private set; }

    public static readonly LoyaltySettings Default =
        new() { IsEnabled = false, CreditRatePercent = 0, MinBookingAmount = 0 };

    public static LoyaltySettings Create(bool isEnabled, decimal ratePercent, decimal minAmount)
    {
        if (ratePercent < 0 || ratePercent > 100)
            throw new ArgumentException("CreditRatePercent deve estar entre 0 e 100.", nameof(ratePercent));
        if (minAmount < 0)
            throw new ArgumentException("MinBookingAmount não pode ser negativo.", nameof(minAmount));
        return new() { IsEnabled = isEnabled, CreditRatePercent = ratePercent, MinBookingAmount = minAmount };
    }

    public decimal CalculateBonus(decimal bookingAmount)
    {
        if (!IsEnabled || bookingAmount < MinBookingAmount) return 0;
        return Math.Round(bookingAmount * CreditRatePercent / 100m, 2);
    }
}
