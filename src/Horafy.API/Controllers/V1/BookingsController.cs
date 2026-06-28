using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Bookings.Commands;
using Horafy.Application.Features.Bookings.Queries;
using Horafy.Domain.Entities.Bookings;
using Horafy.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize]
public sealed class BookingsController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet("my")]
    [ProducesResponseType(typeof(PagedResult<BookingResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new GetMyBookingsQuery(page, pageSize), cancellationToken));

    [HttpGet]
    [Authorize(Roles = "TenantOwner,TenantAdmin,TenantStaff,PlatformAdmin")]
    [ProducesResponseType(typeof(PagedResult<BookingResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? resourceId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? q             = null,
        [FromQuery] BookingStatus? status = null,
        [FromQuery] BookingKind? kind     = null,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(
            new GetBookingsQuery(resourceId, from, to, q, status, kind, page, pageSize), cancellationToken));

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
                request.ServiceIds, request.ResourceId,
                request.ScheduledAt, request.Notes),
            cancellationToken);

        if (result.IsFailure) return ToActionResult(result);
        return CreatedAtRoute("GetBookingById", new { id = result.Value }, result.Value);
    }

    [HttpPost("admin")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,TenantStaff,PlatformAdmin")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdminCreate(
        [FromBody] AdminCreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new AdminCreateBookingCommand(
                request.ServiceIds,
                request.ResourceId,
                request.ScheduledAt,
                request.CustomerName,
                request.CustomerEmail,
                request.CustomerPhone,
                request.Notes),
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

    [HttpPost("recurring")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateRecurring(
        [FromBody] CreateRecurringBookingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new CreateRecurringBookingCommand(
                request.ServiceId, request.ResourceId, request.FirstOccurrence,
                request.Frequency, request.OccurrenceCount, request.Notes),
            cancellationToken);

        if (result.IsFailure) return ToActionResult(result);
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/reschedule")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reschedule(
        Guid id,
        [FromBody] RescheduleBookingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new RescheduleBookingCommand(id, request.NewScheduledAt), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpGet("recurring/{groupId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<BookingResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRecurringSeries(Guid groupId, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetRecurringSeriesQuery(groupId), cancellationToken));

    [HttpDelete("recurring/{groupId:guid}")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,TenantStaff,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelRecurringSeries(
        Guid groupId,
        [FromBody] CancelRecurringSeriesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new CancelRecurringSeriesCommand(groupId, request.Reason, request.FromDate),
            cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record CreateBookingRequest(
    IReadOnlyList<Guid> ServiceIds,
    Guid ResourceId,
    DateTimeOffset ScheduledAt,
    string? Notes);

public sealed record CancelBookingRequest(string? Reason);
public sealed record RescheduleBookingRequest(DateTimeOffset NewScheduledAt);

public sealed record CreateRecurringBookingRequest(
    Guid ServiceId,
    Guid ResourceId,
    DateTimeOffset FirstOccurrence,
    RecurrenceFrequency Frequency,
    int OccurrenceCount,
    string? Notes);

public sealed record CancelRecurringSeriesRequest(string? Reason, DateTimeOffset? FromDate);

public sealed record AdminCreateBookingRequest(
    IReadOnlyList<Guid> ServiceIds,
    Guid ResourceId,
    DateTimeOffset ScheduledAt,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? Notes);
