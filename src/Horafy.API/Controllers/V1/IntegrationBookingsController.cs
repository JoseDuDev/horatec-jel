using Asp.Versioning;
using Horafy.Application.Features.Bookings.Commands;
using Horafy.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

/// <summary>
/// Criação de agendamentos por integrações (ex.: Atendefy) usando token de serviço.
/// Rota dedicada (<c>/api/v1/integrations/bookings</c>) para não herdar o prefixo
/// padrão de controller. Aceita role TenantStaff (token de integração).
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/integrations/bookings")]
[Produces("application/json")]
[Authorize(Roles = "TenantOwner,TenantAdmin,TenantStaff,PlatformAdmin")]
public sealed class IntegrationBookingsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Cria um agendamento idempotente. Reenvio com o mesmo <c>externalId</c> retorna a
    /// mesma reserva (200) em vez de duplicar.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(IntegrationBookingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateIntegrationBookingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateIntegrationBookingCommand(
            request.ServiceIds,
            request.ResourceId,
            request.ScheduledAt,
            request.CustomerName,
            request.CustomerEmail,
            request.CustomerPhone,
            request.Notes,
            request.ExternalId,
            request.Source), cancellationToken);

        if (result.IsSuccess)
            return Ok(result.Value);

        var error = result.Error;
        var payload = new { error = error.Description, code = error.Code };
        return error.Type switch
        {
            ErrorType.NotFound     => NotFound(payload),
            ErrorType.Conflict     => Conflict(payload),
            ErrorType.Validation   => BadRequest(payload),
            ErrorType.Unauthorized => StatusCode(StatusCodes.Status403Forbidden, payload),
            _                      => StatusCode(StatusCodes.Status500InternalServerError, payload)
        };
    }
}

public sealed record CreateIntegrationBookingRequest(
    IReadOnlyList<Guid> ServiceIds,
    Guid ResourceId,
    DateTimeOffset ScheduledAt,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? Notes,
    string? ExternalId,
    string? Source);
