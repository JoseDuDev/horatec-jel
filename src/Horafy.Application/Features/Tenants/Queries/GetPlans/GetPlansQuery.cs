using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Queries.GetPlans;

/// <summary>Lista os planos com seus limites efetivos (config persistida ou default).</summary>
public sealed record GetPlansQuery : IRequest<Result<IReadOnlyList<PlanLimitsResult>>>;

public sealed record PlanLimitsResult(
    TenantPlan Plan, int MaxServices, int MaxResources, int MaxRentableItems);

internal sealed class GetPlansQueryHandler(IPlanLimitsService planLimits)
    : IRequestHandler<GetPlansQuery, Result<IReadOnlyList<PlanLimitsResult>>>
{
    public async Task<Result<IReadOnlyList<PlanLimitsResult>>> Handle(
        GetPlansQuery request, CancellationToken cancellationToken)
    {
        var list = new List<PlanLimitsResult>();
        foreach (var plan in Enum.GetValues<TenantPlan>())
        {
            var limits = await planLimits.GetLimitsAsync(plan, cancellationToken);
            list.Add(new PlanLimitsResult(
                plan, limits.MaxServices, limits.MaxResources, limits.MaxRentableItems));
        }
        return Result.Success<IReadOnlyList<PlanLimitsResult>>(list);
    }
}
