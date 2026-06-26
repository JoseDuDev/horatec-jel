using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Horafy.Application.Features.Integrations.Webhooks;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared.Security;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Horafy.Infrastructure.Messaging.Consumers;

/// <summary>
/// Entrega o webhook de saída: carrega a config do tenant, assina o corpo (HMAC-SHA256)
/// e faz o POST. Erros de entrega lançam exceção para acionar o retry do MassTransit.
/// </summary>
internal sealed class IntegrationWebhookConsumer(
    IIntegrationWebhookRepository webhookRepository,
    IHttpClientFactory            httpClientFactory,
    ILogger<IntegrationWebhookConsumer> logger)
    : IConsumer<IntegrationWebhookMessage>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task Consume(ConsumeContext<IntegrationWebhookMessage> context)
    {
        var msg = context.Message;

        var config = await webhookRepository.GetByTenantAsync(msg.TenantId, context.CancellationToken);
        if (config is null || !config.IsActive || string.IsNullOrWhiteSpace(config.Url))
            return; // tenant sem webhook configurado — nada a fazer

        var envelope = new
        {
            @event     = msg.EventType,
            occurredAt = msg.OccurredAt,
            tenantSlug = msg.TenantSlug,
            booking    = msg.Booking
        };

        var json      = JsonSerializer.Serialize(envelope, JsonOptions);
        var signature = WebhookSignature.Compute(config.Secret, json);

        using var request = new HttpRequestMessage(HttpMethod.Post, config.Url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("X-Horafy-Event", msg.EventType);
        request.Headers.TryAddWithoutValidation("X-Horafy-Signature", signature);
        request.Headers.TryAddWithoutValidation("X-Horafy-Delivery", Guid.NewGuid().ToString());

        var client   = httpClientFactory.CreateClient("integration-webhook");
        var response = await client.SendAsync(request, context.CancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Webhook {Event} para tenant {TenantId} falhou: {Status}",
                msg.EventType, msg.TenantId, (int)response.StatusCode);

            // Lança para acionar o retry exponencial do MassTransit.
            throw new HttpRequestException(
                $"Webhook retornou status {(int)response.StatusCode}.");
        }

        logger.LogInformation(
            "Webhook {Event} entregue para tenant {TenantId}.", msg.EventType, msg.TenantId);
    }
}
