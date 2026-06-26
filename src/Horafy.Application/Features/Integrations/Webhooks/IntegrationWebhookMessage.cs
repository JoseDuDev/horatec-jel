namespace Horafy.Application.Features.Integrations.Webhooks;

/// <summary>
/// Mensagem (MassTransit) que carrega um evento de booking já totalmente populado no
/// contexto do tenant. O consumer apenas carrega a config do webhook, assina e faz o POST.
/// </summary>
public sealed record IntegrationWebhookMessage(
    Guid TenantId,
    string TenantSlug,
    string EventType,
    DateTimeOffset OccurredAt,
    WebhookBookingPayload Booking);

public sealed record WebhookBookingPayload(
    Guid Id,
    string? ExternalId,
    string? Source,
    string Status,
    Guid? ResourceId,
    string ResourceName,
    IReadOnlyList<WebhookServicePayload> Services,
    DateTimeOffset ScheduledAt,
    DateTimeOffset EndsAt,
    int DurationMinutes,
    WebhookCustomerPayload Customer);

public sealed record WebhookServicePayload(Guid ServiceId, string ServiceName);

public sealed record WebhookCustomerPayload(string Name, string Email, string? Phone);
