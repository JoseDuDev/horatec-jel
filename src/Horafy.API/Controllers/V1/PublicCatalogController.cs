using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Catalog.Queries;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[AllowAnonymous]
public sealed class PublicCatalogController(
    ISender               sender,
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenantService)
    : ApiControllerBase(sender)
{
    [HttpGet("public/{slug}/services")]
    [ProducesResponseType(typeof(IReadOnlyList<PublicServiceResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServices(
        string slug, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetBySlugAsync(slug, cancellationToken);
        if (tenant is null) return NotFound();

        currentTenantService.SetTenant(tenant.Id, $"tenant_{tenant.Slug}", tenant.Slug);

        return ToActionResult(
            await Sender.Send(new GetPublicServicesQuery(), cancellationToken));
    }

    [HttpGet("public/{slug}/resources")]
    [ProducesResponseType(typeof(IReadOnlyList<PublicResourceResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResources(
        string slug, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetBySlugAsync(slug, cancellationToken);
        if (tenant is null) return NotFound();

        currentTenantService.SetTenant(tenant.Id, $"tenant_{tenant.Slug}", tenant.Slug);

        return ToActionResult(
            await Sender.Send(new GetPublicResourcesQuery(), cancellationToken));
    }
}
