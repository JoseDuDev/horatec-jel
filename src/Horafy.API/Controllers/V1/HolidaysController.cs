using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Availability.Commands;
using Horafy.Application.Features.Availability.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
public sealed class HolidaysController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet("holidays")]
    [ProducesResponseType(typeof(IReadOnlyList<HolidayResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? year = null,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new GetHolidaysQuery(year), cancellationToken));

    [HttpPost("holidays")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateHolidayRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new CreateHolidayCommand(
                request.Name, request.Date, request.IsRecurringAnnually, request.Reason),
            cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetAll), new { }, result.Value)
            : ToActionResult(result);
    }

    [HttpPut("holidays/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateHolidayRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new UpdateHolidayCommand(
                id, request.Name, request.Date, request.IsRecurringAnnually, request.Reason),
            cancellationToken);

        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpDelete("holidays/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteHolidayCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpPost("holidays/{id:guid}/apply")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Apply(
        Guid id,
        [FromBody] ApplyHolidayRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new ApplyHolidayCommand(id, request.ResourceIds),
            cancellationToken);

        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record CreateHolidayRequest(
    string  Name,
    DateOnly Date,
    bool    IsRecurringAnnually,
    string? Reason);

public sealed record UpdateHolidayRequest(
    string  Name,
    DateOnly Date,
    bool    IsRecurringAnnually,
    string? Reason);

public sealed record ApplyHolidayRequest(IReadOnlyList<Guid> ResourceIds);
