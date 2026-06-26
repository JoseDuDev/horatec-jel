using Horafy.Shared;

namespace Horafy.Application.Features.Integrations;

public static class IntegrationErrors
{
    public static readonly Error TenantMissing = new(
        "Integration.TenantMissing",
        "Não foi possível identificar o tenant da requisição.",
        ErrorType.Validation);

    public static readonly Error ApiKeyNotFound = new(
        "Integration.ApiKeyNotFound",
        "Chave de API não encontrada.",
        ErrorType.NotFound);

    public static readonly Error InvalidApiKey = new(
        "Integration.InvalidApiKey",
        "Chave de API inválida ou revogada.",
        ErrorType.Unauthorized);

    public static readonly Error WebhookNotConfigured = new(
        "Integration.WebhookNotConfigured",
        "Nenhum webhook configurado para este tenant.",
        ErrorType.NotFound);

    public static readonly Error InvalidWebhookUrl = new(
        "Integration.InvalidWebhookUrl",
        "URL de webhook inválida. Use uma URL http(s) completa.",
        ErrorType.Validation);
}
