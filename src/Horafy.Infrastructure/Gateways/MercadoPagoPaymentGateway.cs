using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Horafy.Infrastructure.Gateways;

internal sealed class MercadoPagoPaymentGateway(
    HttpClient httpClient,
    IOptions<MercadoPagoOptions> options,
    ILogger<MercadoPagoPaymentGateway> logger) : IPaymentGateway
{
    private readonly MercadoPagoOptions _opts = options.Value;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public async Task<PaymentPreferenceResult> CreatePreferenceAsync(
        CreatePaymentPreferenceRequest request, CancellationToken ct = default)
    {
        var body = new
        {
            items = new[]
            {
                new { title = $"Agendamento {request.BookingId}", quantity = 1,
                      unit_price = request.DepositAmount > 0 ? request.DepositAmount : request.Amount,
                      currency_id = "BRL" }
            },
            payer = new { email = request.CustomerEmail },
            back_urls = new { success = request.BackUrl, failure = request.BackUrl, pending = request.BackUrl },
            auto_return = "approved",
            notification_url = request.WebhookUrl,
            external_reference = request.BookingId.ToString(),
            payment_methods = new
            {
                excluded_payment_types = request.Method switch
                {
                    PaymentMethod.Pix        => new[] { new { id = "credit_card" }, new { id = "debit_card" }, new { id = "ticket" } },
                    PaymentMethod.CreditCard  => new[] { new { id = "bank_transfer" }, new { id = "debit_card" }, new { id = "ticket" } },
                    PaymentMethod.DebitCard   => new[] { new { id = "bank_transfer" }, new { id = "credit_card" }, new { id = "ticket" } },
                    PaymentMethod.Boleto      => new[] { new { id = "bank_transfer" }, new { id = "credit_card" }, new { id = "debit_card" } },
                    _                         => Array.Empty<object>()
                }
            }
        };

        var response = await httpClient.PostAsJsonAsync("/checkout/preferences", body, _json, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MpPreferenceResponse>(_json, ct)
            ?? throw new InvalidOperationException("Resposta inválida do Mercado Pago ao criar preferência.");

        logger.LogInformation("Preferência MP criada: {PreferenceId}", result.Id);

        return new PaymentPreferenceResult(result.Id, result.InitPoint, ExpiresAt: null);
    }

    public async Task<PaymentStatusResult> GetPaymentStatusAsync(string mpPaymentId, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"/v1/payments/{mpPaymentId}", ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MpPaymentResponse>(_json, ct)
            ?? throw new InvalidOperationException("Resposta inválida do Mercado Pago ao consultar pagamento.");

        var status = result.Status switch
        {
            "approved"  => PaymentStatus.Approved,
            "rejected"  => PaymentStatus.Rejected,
            "cancelled" => PaymentStatus.Cancelled,
            _           => PaymentStatus.Pending
        };

        return new PaymentStatusResult(
            mpPaymentId,
            result.Metadata?.PreferenceId ?? result.PreferenceId ?? string.Empty,
            status,
            status == PaymentStatus.Approved ? result.DateApproved : null);
    }

    public async Task<RefundResult> RefundAsync(string mpPaymentId, decimal amount, CancellationToken ct = default)
    {
        try
        {
            var body     = new { amount };
            var response = await httpClient.PostAsJsonAsync($"/v1/payments/{mpPaymentId}/refunds", body, _json, ct);
            response.EnsureSuccessStatusCode();
            return new RefundResult(true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao estornar pagamento MP {MpPaymentId}", mpPaymentId);
            return new RefundResult(false, ex.Message);
        }
    }

    public bool ValidateWebhookSignature(string mpPaymentId, string requestId, string xSignature)
    {
        if (string.IsNullOrEmpty(_opts.WebhookSecret)) return true; // dev mode

        // xSignature format: "ts=TIMESTAMP,v1=HASH"
        var parts = xSignature.Split(',');
        if (parts.Length < 2) return false;

        var ts   = parts.FirstOrDefault(p => p.StartsWith("ts="))?.Substring(3) ?? string.Empty;
        var hash = parts.FirstOrDefault(p => p.StartsWith("v1="))?.Substring(3) ?? string.Empty;

        var message  = $"id:{mpPaymentId};request-id:{requestId};ts:{ts};";
        var keyBytes = Encoding.UTF8.GetBytes(_opts.WebhookSecret);
        var msgBytes = Encoding.UTF8.GetBytes(message);
        var computed = Convert.ToHexString(HMACSHA256.HashData(keyBytes, msgBytes)).ToLowerInvariant();

        return computed == hash;
    }

    private sealed record MpPreferenceResponse(string Id, string InitPoint);
    private sealed record MpPaymentResponse(
        string Id, string Status, string? PreferenceId,
        DateTimeOffset? DateApproved, MpMetadata? Metadata);
    private sealed record MpMetadata(string? PreferenceId);
}
