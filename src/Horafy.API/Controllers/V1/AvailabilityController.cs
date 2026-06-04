using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Availability.Commands;
using Horafy.Application.Features.Availability.Queries;
using Horafy.Application.Features.Resources.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize]
public sealed class AvailabilityController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet("resources/{resourceId:guid}/slots")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<DateTimeOffset>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSlots(
        Guid resourceId,
        [FromQuery] DateOnly date,
        [FromQuery] Guid? serviceId = null,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(
            new GetAvailableSlotsQuery(resourceId, date, serviceId), cancellationToken));

    [HttpPut("business-hours")]
    [Authorize(Roles = "TenantOwner,TenantAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetBusinessHours(
        [FromBody] SetBusinessHoursRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new SetBusinessHoursCommand(
                request.DayOfWeek, request.OpenTime, request.CloseTime, request.IsOpen),
            cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpPut("resources/{resourceId:guid}/rules")]
    [Authorize(Roles = "TenantOwner,TenantAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetRule(
        Guid resourceId,
        [FromBody] SetAvailabilityRuleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new SetAvailabilityRuleCommand(
                resourceId, request.DayOfWeek, request.StartTime, request.EndTime,
                request.SlotDurationMinutes, request.BreakAfterMinutes),
            cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpPut("resources/{resourceId:guid}/exceptions")]
    [Authorize(Roles = "TenantOwner,TenantAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetException(
        Guid resourceId,
        [FromBody] SetAvailabilityExceptionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new SetAvailabilityExceptionCommand(
                resourceId, request.Date, request.IsBlocked,
                request.CustomStart, request.CustomEnd, request.Reason),
            cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpPost("resources/{resourceId:guid}/services/{serviceId:guid}")]
    [Authorize(Roles = "TenantOwner,TenantAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddService(
        Guid resourceId, Guid serviceId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new AddResourceServiceCommand(resourceId, serviceId), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpDelete("resources/{resourceId:guid}/services/{serviceId:guid}")]
    [Authorize(Roles = "TenantOwner,TenantAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveService(
        Guid resourceId, Guid serviceId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new RemoveResourceServiceCommand(resourceId, serviceId), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record SetBusinessHoursRequest(
    DayOfWeek DayOfWeek, TimeOnly OpenTime, TimeOnly CloseTime, bool IsOpen);

public sealed record SetAvailabilityRuleRequest(
    DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime,
    int SlotDurationMinutes, int BreakAfterMinutes = 0);

public sealed record SetAvailabilityExceptionRequest(
    DateOnly Date, bool IsBlocked,
    TimeOnly? CustomStart, TimeOnly? CustomEnd, string? Reason);
