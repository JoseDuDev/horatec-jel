using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Resources.Commands;
using Horafy.Application.Features.Resources.Queries;
using Horafy.Domain.Entities.Resources;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize]
public sealed class ResourcesController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<ResourceResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool onlyActive = true,
        [FromQuery] ResourceType? type = null,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new GetResourcesQuery(onlyActive, type), cancellationToken));

    [HttpGet("{id:guid}", Name = "GetResourceById")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ResourceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetResourceByIdQuery(id), cancellationToken));

    [HttpPost]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateResourceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new CreateResourceCommand(request.Name, request.Type, request.Email, request.Phone,
                request.Specialty, request.Bio, request.AvatarUrl, request.UserId),
            cancellationToken);

        if (result.IsFailure) return ToActionResult(result);
        return CreatedAtRoute("GetResourceById", new { id = result.Value }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateResourceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new UpdateResourceCommand(id, request.Name, request.Email, request.Phone,
                request.Specialty, request.Bio, request.AvatarUrl),
            cancellationToken);

        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteResourceCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record CreateResourceRequest(
    string Name, ResourceType Type, string? Email, string? Phone,
    string? Specialty, string? Bio, string? AvatarUrl, Guid? UserId);

public sealed record UpdateResourceRequest(
    string Name, string? Email, string? Phone,
    string? Specialty, string? Bio, string? AvatarUrl);
