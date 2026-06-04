using Horafy.Domain.Entities.Payments;

namespace Horafy.Application.Interfaces;

public interface IPaymentGateway
{
    Task<PaymentPreferenceResult> CreatePreferenceAsync(
        CreatePaymentPreferenceRequest request, CancellationToken ct = default);

    Task<PaymentStatusResult> GetPaymentStatusAsync(
        string mpPaymentId, CancellationToken ct = default);

    Task<RefundResult> RefundAsync(
        string mpPaymentId, decimal amount, CancellationToken ct = default);

    bool ValidateWebhookSignature(
        string mpPaymentId, string requestId, string xSignature);
}

public sealed record CreatePaymentPreferenceRequest(
    Guid BookingId,
    decimal Amount,
    decimal DepositAmount,
    PaymentMethod Method,
    string CustomerEmail,
    string BackUrl,
    string WebhookUrl);

public sealed record PaymentPreferenceResult(
    string PreferenceId,
    string PaymentUrl,
    DateTimeOffset? ExpiresAt);

public sealed record PaymentStatusResult(
    string MpPaymentId,
    string PreferenceId,
    PaymentStatus Status,
    DateTimeOffset? PaidAt);

public sealed record RefundResult(bool Success, string? ErrorMessage);
