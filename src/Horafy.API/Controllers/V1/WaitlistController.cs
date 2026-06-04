using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Waitlist.Commands;
using Horafy.Application.Features.Waitlist.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize]
public sealed class WaitlistController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WaitlistEntryResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetMyWaitlistQuery(), cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Join(
        [FromBody] JoinWaitlistRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new JoinWaitlistCommand(request.ServiceId, request.ResourceId, request.PreferredDate),
            cancellationToken);

        if (result.IsFailure) return ToActionResult(result);
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Leave(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new LeaveWaitlistCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record JoinWaitlistRequest(Guid ServiceId, Guid ResourceId, DateOnly PreferredDate);
