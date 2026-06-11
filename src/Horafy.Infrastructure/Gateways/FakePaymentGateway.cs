using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;

namespace Horafy.Infrastructure.Gateways;

/// <summary>Gateway de pagamento para testes E2E. Ativo quando PAYMENT_GATEWAY=fake.</summary>
internal sealed class FakePaymentGateway : IPaymentGateway
{
    public Task<PaymentPreferenceResult> CreatePreferenceAsync(
        CreatePaymentPreferenceRequest request, CancellationToken ct = default) =>
        Task.FromResult(new PaymentPreferenceResult(
            PreferenceId: $"fake-{request.BookingId}",
            PaymentUrl: string.Empty,   // string vazia → frontend não redireciona para MP
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1)));

    public Task<PaymentStatusResult> GetPaymentStatusAsync(
        string mpPaymentId, CancellationToken ct = default) =>
        Task.FromResult(new PaymentStatusResult(
            MpPaymentId: mpPaymentId,
            PreferenceId: mpPaymentId,
            Status: PaymentStatus.Approved,
            PaidAt: DateTimeOffset.UtcNow));

    public Task<RefundResult> RefundAsync(
        string mpPaymentId, decimal amount, CancellationToken ct = default) =>
        Task.FromResult(new RefundResult(true, null));

    public bool ValidateWebhookSignature(
        string mpPaymentId, string requestId, string xSignature) => true;
}
