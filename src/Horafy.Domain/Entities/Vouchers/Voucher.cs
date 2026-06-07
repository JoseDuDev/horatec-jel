using Horafy.Domain.Entities.Base;
using Horafy.Shared;

namespace Horafy.Domain.Entities.Vouchers;

public sealed class Voucher : BaseEntity
{
    private Voucher() { }

    public string Code { get; private set; } = default!;
    public VoucherDiscountType DiscountType { get; private set; }
    public decimal DiscountValue { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public int? MaxUses { get; private set; }
    public int UsedCount { get; private set; }
    public bool IsActive { get; private set; }

    public static Voucher Create(
        string code,
        VoucherDiscountType discountType,
        decimal discountValue,
        string? description,
        DateTimeOffset? expiresAt,
        int? maxUses) =>
        new()
        {
            Code = code.ToUpperInvariant(),
            DiscountType = discountType,
            DiscountValue = discountValue,
            Description = description,
            ExpiresAt = expiresAt,
            MaxUses = maxUses,
            IsActive = true,
            UsedCount = 0,
        };

    public Result<decimal> CalculateDiscount(decimal totalPrice)
    {
        if (!IsActive)
            return Result.Failure<decimal>(VoucherErrors.Inactive);
        if (ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.UtcNow)
            return Result.Failure<decimal>(VoucherErrors.Expired);
        if (MaxUses.HasValue && UsedCount >= MaxUses.Value)
            return Result.Failure<decimal>(VoucherErrors.MaxUsesReached);

        var discount = DiscountType == VoucherDiscountType.Percentage
            ? totalPrice * (DiscountValue / 100m)
            : Math.Min(DiscountValue, totalPrice);

        return Result.Success(Math.Round(discount, 2));
    }

    public void Redeem() => UsedCount++;
    public void Deactivate() => IsActive = false;
}
