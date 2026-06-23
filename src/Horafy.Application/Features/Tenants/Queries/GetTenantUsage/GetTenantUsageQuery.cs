using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Queries.GetTenantUsage;

/// <summary>Uso atual (cadastros) vs. limites do plano, no contexto do tenant atual.</summary>
public sealed record GetTenantUsageQuery : IRequest<Result<TenantUsageResult>>;

/// <summary><see cref="Max"/> = -1 indica ilimitado.</summary>
public sealed record UsageItem(int Used, int Max);

public sealed record TenantUsageResult(
    TenantCapability Capabilities,
    TenantPlan       Plan,
    UsageItem        Services,
    UsageItem        Resources,
    UsageItem        RentableItems);

internal sealed class GetTenantUsageQueryHandler(
    ITenantPlanService      tenantPlan,
    IServiceRepository      services,
    IResourceRepository     resources,
    IRentableItemRepository items) : IRequestHandler<GetTenantUsageQuery, Result<TenantUsageResult>>
{
    public async Task<Result<TenantUsageResult>> Handle(
        GetTenantUsageQuery request, CancellationToken cancellationToken)
    {
        var tenant = await tenantPlan.GetCurrentTenantAsync(cancellationToken);
        if (tenant is null)
            return Result.Failure<TenantUsageResult>(TenantErrors.NotFound);

        var limits = tenant.Limits;
        var serviceCount  = await services.CountAsync(cancellationToken: cancellationToken);
        var resourceCount = await resources.CountAsync(cancellationToken: cancellationToken);
        var itemCount     = await items.CountAsync(cancellationToken: cancellationToken);

        return Result.Success(new TenantUsageResult(
            tenant.Capabilities,
            tenant.Plan,
            new UsageItem(serviceCount,  limits.MaxServices),
            new UsageItem(resourceCount, limits.MaxResources),
            new UsageItem(itemCount,     limits.MaxRentableItems)));
    }
}
