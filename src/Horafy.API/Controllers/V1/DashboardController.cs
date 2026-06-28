using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Dashboard.Queries;
using Horafy.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
public sealed class DashboardController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardStats), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to   = null,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new GetDashboardQuery(from, to), cancellationToken));
}
