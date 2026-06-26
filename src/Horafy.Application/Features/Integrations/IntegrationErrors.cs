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
}
