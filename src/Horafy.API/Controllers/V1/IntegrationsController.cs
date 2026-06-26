using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Integrations.Commands;
using Horafy.Application.Features.Integrations.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

/// <summary>
/// Gestão de chaves de API por tenant e troca de chave por token (M2M).
/// Usado por integrações como o Atendefy.
/// </summary>
[ApiVersion(1)]
[Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
public sealed class IntegrationsController(ISender sender) : ApiControllerBase(sender)
{
    /// <summary>Cria uma nova API key. O segredo é retornado UMA única vez.</summary>
    [HttpPost("api-keys")]
    [ProducesResponseType(typeof(CreatedApiKeyResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateApiKey(
        [FromBody] CreateApiKeyRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new CreateIntegrationApiKeyCommand(request.Name, request.Scopes), cancellationToken));

    /// <summary>Lista as API keys do tenant (sem expor o segredo).</summary>
    [HttpGet("api-keys")]
    [ProducesResponseType(typeof(IReadOnlyList<ApiKeySummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetApiKeys(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetIntegrationApiKeysQuery(), cancellationToken));

    /// <summary>Revoga uma API key.</summary>
    [HttpDelete("api-keys/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeApiKey(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new RevokeIntegrationApiKeyCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    /// <summary>Cria/atualiza o webhook de saída do tenant. Segredo retornado 1x (criação/rotação).</summary>
    [HttpPut("webhook")]
    [ProducesResponseType(typeof(WebhookConfigResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertWebhook(
        [FromBody] UpsertWebhookRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new UpsertIntegrationWebhookCommand(request.Url, request.RotateSecret), cancellationToken));

    /// <summary>Retorna a config do webhook do tenant (sem o segredo).</summary>
    [HttpGet("webhook")]
    [ProducesResponseType(typeof(WebhookSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWebhook(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetIntegrationWebhookQuery(), cancellationToken));

    /// <summary>Desativa o webhook do tenant.</summary>
    [HttpDelete("webhook")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWebhook(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteIntegrationWebhookCommand(), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    /// <summary>
    /// Troca a API key (header <c>X-Api-Key</c>) por um JWT de serviço de curta duração.
    /// Público: a chave identifica o tenant.
    /// </summary>
    [HttpPost("token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ServiceTokenResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ExchangeToken(
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return Unauthorized(new { error = "Header X-Api-Key ausente." });

        return ToActionResult(await Sender.Send(
            new ExchangeApiKeyForTokenCommand(apiKey), cancellationToken));
    }
}

public sealed record CreateApiKeyRequest(string Name, string? Scopes);

public sealed record UpsertWebhookRequest(string Url, bool RotateSecret = false);
