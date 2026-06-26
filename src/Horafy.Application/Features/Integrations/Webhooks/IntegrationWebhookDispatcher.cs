using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using MassTransit;

namespace Horafy.Application.Features.Integrations.Webhooks;

/// <summary>
/// Monta o payload do webhook (no contexto do tenant) e publica a mensagem na bus.
/// A entrega HTTP (com assinatura/retry) fica no consumer, fora do caminho da requisição.
/// </summary>
internal sealed class IntegrationWebhookDispatcher(
    IBookingRepository    bookingRepository,
    ICurrentTenantService currentTenant,
    IPublishEndpoint      publishEndpoint)
{
    public async Task DispatchAsync(
        Guid bookingId, string eventType, bool skipIntegrationOrigin, CancellationToken ct)
    {
        var tenantId = currentTenant.TenantId;
        if (tenantId is null) return;

        var booking = await bookingRepository.GetByIdAsync(bookingId, ct);
        if (booking is null) return;

        // Evita "eco": não reenviamos o created de uma reserva que a própria integração criou.
        if (skipIntegrationOrigin && !string.IsNullOrEmpty(booking.Source))
            return;

        var payload = new WebhookBookingPayload(
            booking.Id,
            booking.ExternalId,
            booking.Source,
            booking.Status.ToString(),
            booking.ResourceId,
            booking.ResourceName,
            booking.Services.Select(s => new WebhookServicePayload(s.ServiceId, s.ServiceName)).ToList(),
            booking.ScheduledAt,
            booking.EndsAt,
            booking.DurationMinutes,
            new WebhookCustomerPayload(booking.CustomerName, booking.CustomerEmail, booking.CustomerPhone));

        await publishEndpoint.Publish(new IntegrationWebhookMessage(
            tenantId.Value,
            currentTenant.Slug ?? string.Empty,
            eventType,
            DateTimeOffset.UtcNow,
            payload), ct);
    }
}
