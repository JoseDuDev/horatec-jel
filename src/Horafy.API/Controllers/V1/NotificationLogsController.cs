using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Notifications.Queries;
using Horafy.Application.Interfaces;
using Horafy.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
public sealed class NotificationLogsController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NotificationLogResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] bool?            success,
        [FromQuery] int              page     = 1,
        [FromQuery] int              pageSize = 50,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(
            new GetNotificationLogsQuery(from, to, success, page, pageSize),
            cancellationToken));
}
