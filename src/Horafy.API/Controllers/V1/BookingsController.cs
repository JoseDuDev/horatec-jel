using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Bookings.Commands;
using Horafy.Application.Features.Bookings.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize]
public sealed class BookingsController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet("my")]
    [ProducesResponseType(typeof(IReadOnlyList<BookingResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetMyBookingsQuery(), cancellationToken));

    [HttpGet]
    [Authorize(Roles = "TenantOwner,TenantAdmin,TenantStaff,PlatformAdmin")]
    [ProducesResponseType(typeof(IReadOnlyList<BookingResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? resourceId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new GetBookingsQuery(resourceId, from, to), cancellationToken));

    [HttpGet("{id:guid}", Name = "GetBookingById")]
    [ProducesResponseType(typeof(BookingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetBookingByIdQuery(id), cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new CreateBookingCommand(
                request.ServiceId, request.ResourceId,
                request.ScheduledAt, request.Notes),
            cancellationToken);

        if (result.IsFailure) return ToActionResult(result);
        return CreatedAtRoute("GetBookingById", new { id = result.Value }, result.Value);
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,TenantStaff,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ConfirmBookingCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelBookingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new CancelBookingCommand(id, request.Reason), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,TenantStaff,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CompleteBookingCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpPost("{id:guid}/no-show")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,TenantStaff,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> NoShow(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new NoShowBookingCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record CreateBookingRequest(
    Guid ServiceId, Guid ResourceId, DateTimeOffset ScheduledAt, string? Notes);

public sealed record CancelBookingRequest(string? Reason);
