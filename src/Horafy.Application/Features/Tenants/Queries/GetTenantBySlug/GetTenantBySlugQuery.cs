using Horafy.Application.Features.Tenants.Queries.GetCurrentTenant;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Queries.GetTenantBySlug;

/// <summary>Consulta pública — retorna dados visíveis do tenant (landing page).</summary>
public sealed record GetTenantBySlugQuery(string Slug) : IRequest<Result<TenantResult>>;

internal sealed class GetTenantBySlugQueryHandler(
    ITenantRepository tenantRepository)
    : IRequestHandler<GetTenantBySlugQuery, Result<TenantResult>>
{
    public async Task<Result<TenantResult>> Handle(
        GetTenantBySlugQuery request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetBySlugAsync(request.Slug, cancellationToken);
        if (tenant is null) return Result.Failure<TenantResult>(TenantErrors.NotFound);

        return Result.Success(GetCurrentTenantQueryHandler.ToResult(tenant));
    }
}
