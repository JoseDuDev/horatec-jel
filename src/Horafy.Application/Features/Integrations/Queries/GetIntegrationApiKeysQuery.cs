using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Integrations.Queries;

public sealed record GetIntegrationApiKeysQuery()
    : IRequest<Result<IReadOnlyList<ApiKeySummary>>>;

/// <summary>Resumo seguro: nunca expõe o segredo, apenas o prefixo público.</summary>
public sealed record ApiKeySummary(
    Guid Id,
    string Name,
    string KeyPrefix,
    string Scopes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt);

internal sealed class GetIntegrationApiKeysQueryHandler(
    ICurrentUserService currentUser,
    IIntegrationApiKeyRepository repository)
    : IRequestHandler<GetIntegrationApiKeysQuery, Result<IReadOnlyList<ApiKeySummary>>>
{
    public async Task<Result<IReadOnlyList<ApiKeySummary>>> Handle(
        GetIntegrationApiKeysQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId;
        if (tenantId is null)
            return Result.Failure<IReadOnlyList<ApiKeySummary>>(IntegrationErrors.TenantMissing);

        var keys = await repository.GetByTenantAsync(tenantId.Value, cancellationToken);

        var result = keys
            .Select(k => new ApiKeySummary(
                k.Id, k.Name, k.KeyPrefix, k.Scopes, k.IsActive,
                k.CreatedAt, k.LastUsedAt, k.RevokedAt))
            .ToList();

        return Result.Success<IReadOnlyList<ApiKeySummary>>(result);
    }
}
