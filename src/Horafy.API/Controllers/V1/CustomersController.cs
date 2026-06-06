using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Customers.Commands;
using Horafy.Application.Features.Customers.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize(Roles = "Customer")]
[Route("api/v{version:apiVersion}/customers")]
public sealed class CustomersController(ISender sender)
    : ApiControllerBase(sender)
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(CustomerProfileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(CancellationToken ct) =>
        ToActionResult(await Sender.Send(new GetCustomerProfileQuery(), ct));

    [HttpPatch("me/phone")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePhone(
        [FromBody] UpdatePhoneRequest request, CancellationToken ct)
    {
        var result = await Sender.Send(new UpdateCustomerPhoneCommand(request.Phone), ct);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record UpdatePhoneRequest(string? Phone);
