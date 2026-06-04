using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Payments.Commands;
using Horafy.Application.Features.Payments.Queries;
using Horafy.Domain.Entities.Payments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize]
public sealed class PaymentsController(ISender sender) : ApiControllerBase(sender)
{
    [HttpPost]
    [ProducesResponseType(typeof(CreatePaymentResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new CreatePaymentCommand(request.BookingId, request.Amount, request.Method, request.BackUrl),
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("booking/{bookingId:guid}")]
    [ProducesResponseType(typeof(PaymentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByBooking(Guid bookingId, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetPaymentByBookingQuery(bookingId), cancellationToken));

    [HttpPost("{id:guid}/refund")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Refund(
        Guid id,
        [FromBody] RefundRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new RefundPaymentCommand(id, request.Amount), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record CreatePaymentRequest(
    Guid BookingId, decimal Amount, PaymentMethod Method, string BackUrl);

public sealed record RefundRequest(decimal? Amount);
