using Horafy.Domain.Entities.Base;
using Horafy.Domain.Events.Payments;

namespace Horafy.Domain.Entities.Payments;

public sealed class Payment : BaseEntity
{
    private Payment() { }

    public Guid            BookingId             { get; private set; }
    public string          PreferenceId          { get; private set; } = default!;
    public string?         MpPaymentId           { get; private set; }
    public PaymentMethod   Method                { get; private set; }
    public PaymentStatus   Status                { get; private set; } = PaymentStatus.Pending;
    public decimal         Amount                { get; private set; }
    public decimal         DepositAmount         { get; private set; }
    public string?         PaymentUrl            { get; private set; }
    public DateTimeOffset? PaidAt                { get; private set; }
    public DateTimeOffset? ExpiresAt             { get; private set; }
    public string?         VoucherCode           { get; private set; }
    public decimal         VoucherDiscountAmount { get; private set; }
    public decimal         WalletAmount          { get; private set; }

    public static Payment Create(
        Guid bookingId, string preferenceId, PaymentMethod method,
        decimal amount, decimal depositAmount,
        string? paymentUrl = null, DateTimeOffset? expiresAt = null,
        string? voucherCode = null, decimal voucherDiscountAmount = 0,
        decimal walletAmount = 0)
    {
        var payment = new Payment
        {
            BookingId             = bookingId,
            PreferenceId          = preferenceId,
            Method                = method,
            Amount                = amount,
            DepositAmount         = depositAmount,
            PaymentUrl            = paymentUrl,
            ExpiresAt             = expiresAt,
            VoucherCode           = voucherCode,
            VoucherDiscountAmount = voucherDiscountAmount,
            WalletAmount          = walletAmount,
        };
        payment.RaiseDomainEvent(new PaymentCreatedEvent(payment.Id, bookingId, amount, method));
        return payment;
    }

    public void Approve(string mpPaymentId)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"Não é possível aprovar pagamento no status {Status}.");
        MpPaymentId = mpPaymentId;
        Status      = PaymentStatus.Approved;
        PaidAt      = DateTimeOffset.UtcNow;
        UpdatedAt   = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new PaymentConfirmedEvent(Id, BookingId, DepositAmount > 0 && DepositAmount < Amount));
    }

    public void ApproveDirectly()
    {
        Status    = PaymentStatus.Approved;
        PaidAt    = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new PaymentConfirmedEvent(Id, BookingId, false));
    }

    public void Reject(string mpPaymentId)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"Não é possível rejeitar pagamento no status {Status}.");
        MpPaymentId = mpPaymentId;
        Status      = PaymentStatus.Rejected;
        UpdatedAt   = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new PaymentFailedEvent(Id, BookingId));
    }

    public void Refund()
    {
        if (Status != PaymentStatus.Approved)
            throw new InvalidOperationException("Apenas pagamentos aprovados podem ser estornados.");
        Status    = PaymentStatus.Refunded;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new PaymentRefundedEvent(Id, BookingId));
    }
}
