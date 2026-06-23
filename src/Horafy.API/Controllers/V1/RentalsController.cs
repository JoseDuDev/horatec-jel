using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Rentals.Commands;
using Horafy.Application.Features.Rentals.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize]
public sealed class RentalsController(ISender sender) : ApiControllerBase(sender)
{
    // ── Catálogo de itens ─────────────────────────────────────────────────────

    [HttpGet("items")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<RentableItemResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItems(
        [FromQuery] bool onlyActive = true,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new GetRentableItemsQuery(onlyActive), cancellationToken));

    [HttpGet("items/{id:guid}/availability")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RentalAvailabilityResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailability(
        Guid id,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new GetRentalAvailabilityQuery(id, startDate, endDate), cancellationToken));

    [HttpPost("items")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateItem(
        [FromBody] CreateRentableItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CreateRentableItemCommand(
            request.Name, request.Quantity, request.DailyRate, request.SecurityDeposit,
            request.BufferDays, request.Description, request.Category, request.ImageUrl),
            cancellationToken);

        return result.IsFailure
            ? ToActionResult(result)
            : Created($"/api/v1/rentals/items/{result.Value}", result.Value);
    }

    // ── Reserva de locação ────────────────────────────────────────────────────

    [HttpPost("bookings")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateBooking(
        [FromBody] CreateRentalBookingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CreateRentalBookingCommand(
            request.Items, request.StartDate, request.EndDate, request.Notes),
            cancellationToken);

        if (result.IsFailure) return ToActionResult(result);
        return CreatedAtRoute("GetBookingById", new { id = result.Value }, result.Value);
    }

    // ── Ciclo de vida (admin/staff) ───────────────────────────────────────────

    [HttpPost("bookings/{id:guid}/pickup")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,TenantStaff,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pickup(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new MarkRentalPickedUpCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpPost("bookings/{id:guid}/return")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,TenantStaff,PlatformAdmin")]
    [ProducesResponseType(typeof(RentalReturnResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Return(
        Guid id,
        [FromQuery] bool refundToGateway = false,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(
            new MarkRentalReturnedCommand(id, refundToGateway), cancellationToken));
}

public sealed record CreateRentableItemRequest(
    string Name, int Quantity, decimal DailyRate, decimal SecurityDeposit,
    int BufferDays, string? Description, string? Category, string? ImageUrl);

public sealed record CreateRentalBookingRequest(
    IReadOnlyList<RentalItemLine> Items, DateOnly StartDate, DateOnly EndDate, string? Notes);
