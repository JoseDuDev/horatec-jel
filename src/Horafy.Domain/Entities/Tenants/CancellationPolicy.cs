namespace Horafy.Domain.Entities.Tenants;

public sealed class CancellationPolicy
{
    private CancellationPolicy() { }

    public int MinCancellationHours { get; private set; }
    public decimal CancellationFeePercent { get; private set; }
    public bool AllowCustomerCancellation { get; private set; } = true;

    public static readonly CancellationPolicy Default =
        new() { MinCancellationHours = 0, CancellationFeePercent = 0, AllowCustomerCancellation = true };

    public static CancellationPolicy Create(int minHours, decimal feePercent, bool allowCustomer)
    {
        if (minHours < 0) throw new ArgumentException("MinCancellationHours não pode ser negativo.", nameof(minHours));
        if (feePercent < 0 || feePercent > 100) throw new ArgumentException("CancellationFeePercent deve estar entre 0 e 100.", nameof(feePercent));
        return new() { MinCancellationHours = minHours, CancellationFeePercent = feePercent, AllowCustomerCancellation = allowCustomer };
    }

    public bool CanCancelAt(DateTimeOffset scheduledAt, DateTimeOffset now) =>
        (scheduledAt - now) >= TimeSpan.FromHours(MinCancellationHours);
}
