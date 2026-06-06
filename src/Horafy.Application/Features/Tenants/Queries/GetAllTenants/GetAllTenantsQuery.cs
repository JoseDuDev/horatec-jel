using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Queries.GetAllTenants;

public sealed record GetAllTenantsQuery : IRequest<Result<IReadOnlyList<TenantSummary>>>;

public sealed record TenantSummary(
    Guid           Id,
    string         Name,
    string         Slug,
    TenantStatus   Status,
    TenantPlan     Plan,
    TenantVertical Vertical,
    string?        Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset? PlanRenewsAt);

internal sealed class GetAllTenantsQueryHandler(
    ITenantRepository tenantRepository)
    : IRequestHandler<GetAllTenantsQuery, Result<IReadOnlyList<TenantSummary>>>
{
    public async Task<Result<IReadOnlyList<TenantSummary>>> Handle(
        GetAllTenantsQuery request, CancellationToken cancellationToken)
    {
        var tenants = await tenantRepository.GetAllAsync(cancellationToken);

        var result = tenants
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TenantSummary(
                t.Id, t.Name, t.Slug,
                t.Status, t.Plan, t.Vertical,
                t.Email, t.CreatedAt, t.TrialEndsAt, t.PlanRenewsAt))
            .ToList();

        return Result.Success<IReadOnlyList<TenantSummary>>(result);
    }
}
