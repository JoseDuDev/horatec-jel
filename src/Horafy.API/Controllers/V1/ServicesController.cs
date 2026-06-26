using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Resources.Queries;
using Horafy.Application.Features.Services.Commands;
using Horafy.Application.Features.Services.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize]
public sealed class ServicesController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool onlyActive = true,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new GetServicesQuery(onlyActive), cancellationToken));

    [HttpGet("{id:guid}", Name = "GetServiceById")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ServiceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetServiceByIdQuery(id), cancellationToken));

    /// <summary>
    /// Lista os profissionais (recursos) que atendem o serviço informado.
    /// Passo do fluxo de agendamento: serviço → profissionais que o atendem.
    /// </summary>
    [HttpGet("{serviceId:guid}/resources")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<ResourceResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetResourcesForService(
        Guid serviceId,
        [FromQuery] bool onlyActive = true,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(
            new GetResourcesByServiceQuery(serviceId, onlyActive), cancellationToken));

    [HttpPost]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateServiceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new CreateServiceCommand(request.Name, request.DurationMinutes, request.Price,
                request.Description, request.Category), cancellationToken);

        if (result.IsFailure) return ToActionResult(result);
        return CreatedAtRoute("GetServiceById", new { id = result.Value }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateServiceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new UpdateServiceCommand(id, request.Name, request.DurationMinutes, request.Price,
                request.Description, request.Category), cancellationToken);

        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteServiceCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record CreateServiceRequest(
    string Name, int DurationMinutes, decimal Price, string? Description, string? Category);

public sealed record UpdateServiceRequest(
    string Name, int DurationMinutes, decimal Price, string? Description, string? Category);
