using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Vouchers.Commands.CreateVoucher;
using Horafy.Application.Features.Vouchers.Commands.DeactivateVoucher;
using Horafy.Application.Features.Vouchers.Queries.GetVouchers;
using Horafy.Application.Features.Vouchers.Queries.ValidateVoucher;
using Horafy.Domain.Entities.Vouchers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
public sealed class VouchersController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<VoucherSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        ToActionResult(await Sender.Send(new GetVouchersQuery(), ct));

    [HttpGet("validate")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(VoucherValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Validate(
        [FromQuery] string code,
        [FromQuery] decimal totalPrice,
        CancellationToken ct) =>
        ToActionResult(await Sender.Send(new ValidateVoucherQuery(code, totalPrice), ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateVoucherRequest request,
        CancellationToken ct)
    {
        var result = await Sender.Send(
            new CreateVoucherCommand(
                request.Code, request.DiscountType, request.DiscountValue,
                request.Description, request.ExpiresAt, request.MaxUses), ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetAll), result.Value)
            : ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) =>
        ToActionResult(await Sender.Send(new DeactivateVoucherCommand(id), ct));
}

public sealed record CreateVoucherRequest(
    string              Code,
    VoucherDiscountType DiscountType,
    decimal             DiscountValue,
    string?             Description,
    DateTimeOffset?     ExpiresAt,
    int?                MaxUses);
