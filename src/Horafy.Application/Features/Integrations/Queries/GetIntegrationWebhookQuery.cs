using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Integrations.Queries;

public sealed record GetIntegrationWebhookQuery() : IRequest<Result<WebhookSummary>>;

/// <summary>Resumo seguro: nunca expõe o segredo.</summary>
public sealed record WebhookSummary(
    string Url,
    bool IsActive,
    bool HasSecret,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

internal sealed class GetIntegrationWebhookQueryHandler(
    ICurrentUserService currentUser,
    IIntegrationWebhookRepository repository)
    : IRequestHandler<GetIntegrationWebhookQuery, Result<WebhookSummary>>
{
    public async Task<Result<WebhookSummary>> Handle(
        GetIntegrationWebhookQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId;
        if (tenantId is null)
            return Result.Failure<WebhookSummary>(IntegrationErrors.TenantMissing);

        var webhook = await repository.GetByTenantAsync(tenantId.Value, cancellationToken);
        if (webhook is null)
            return Result.Failure<WebhookSummary>(IntegrationErrors.WebhookNotConfigured);

        return Result.Success(new WebhookSummary(
            webhook.Url, webhook.IsActive, !string.IsNullOrEmpty(webhook.Secret),
            webhook.CreatedAt, webhook.UpdatedAt));
    }
}
