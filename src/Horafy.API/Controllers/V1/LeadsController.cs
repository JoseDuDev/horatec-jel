using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Leads.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
public sealed class LeadsController(ISender sender) : ApiControllerBase(sender)
{
    /// <summary>Recebe o formulário de interesse da landing pública (sem autenticação).</summary>
    [HttpPost("/api/v{version:apiVersion}/platform/leads")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLeadRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CreateLeadCommand(
            request.Name, request.Phone, request.Email,
            request.BusinessType, request.Message, request.Brand),
            cancellationToken);

        if (result.IsFailure) return ToActionResult(result);
        return StatusCode(StatusCodes.Status201Created, new { id = result.Value });
    }
}

public sealed record CreateLeadRequest(
    string  Name,
    string  Phone,
    string? Email,
    string? BusinessType,
    string? Message,
    string  Brand);
