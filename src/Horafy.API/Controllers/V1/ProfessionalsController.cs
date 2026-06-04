using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Professionals.Commands;
using Horafy.Application.Features.Professionals.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize]
public sealed class ProfessionalsController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<ProfessionalResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool onlyActive = true,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new GetProfessionalsQuery(onlyActive), cancellationToken));

    [HttpGet("{id:guid}", Name = "GetProfessionalById")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProfessionalResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetProfessionalByIdQuery(id), cancellationToken));

    [HttpPost]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProfessionalRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new CreateProfessionalCommand(request.Name, request.Email, request.Phone,
                request.Specialty, request.Bio, request.AvatarUrl, request.UserId),
            cancellationToken);

        if (result.IsFailure) return ToActionResult(result);
        return CreatedAtRoute("GetProfessionalById", new { id = result.Value }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProfessionalRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new UpdateProfessionalCommand(id, request.Name, request.Email, request.Phone,
                request.Specialty, request.Bio, request.AvatarUrl),
            cancellationToken);

        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteProfessionalCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record CreateProfessionalRequest(
    string Name, string? Email, string? Phone,
    string? Specialty, string? Bio, string? AvatarUrl, Guid? UserId);

public sealed record UpdateProfessionalRequest(
    string Name, string? Email, string? Phone,
    string? Specialty, string? Bio, string? AvatarUrl);
