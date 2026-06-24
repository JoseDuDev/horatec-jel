using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Tenants.Commands.UpdatePlanConfig;
using Horafy.Application.Features.Tenants.Queries.GetPlans;
using Horafy.Domain.Entities.Tenants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

/// <summary>Gestão de planos e seus limites (PlatformAdmin).</summary>
[ApiVersion(1)]
[Authorize(Roles = "PlatformAdmin")]
public sealed class PlansController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet("/api/v{version:apiVersion}/platform/plans")]
    [ProducesResponseType(typeof(IReadOnlyList<PlanLimitsResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetPlansQuery(), cancellationToken));

    [HttpPut("/api/v{version:apiVersion}/platform/plans/{plan}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        TenantPlan plan,
        [FromBody] UpdatePlanConfigRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new UpdatePlanConfigCommand(
            plan, request.MaxServices, request.MaxResources, request.MaxRentableItems),
            cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record UpdatePlanConfigRequest(int MaxServices, int MaxResources, int MaxRentableItems);
