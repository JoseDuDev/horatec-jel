using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Notifications.Commands;
using Horafy.Application.Features.Notifications.Queries;
using Horafy.Domain.Entities.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize(Roles = "TenantOwner,TenantAdmin")]
public sealed class NotificationTemplatesController(ISender sender)
    : ApiControllerBase(sender)
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationTemplateResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        ToActionResult(await Sender.Send(new GetNotificationTemplatesQuery(), ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertTemplateRequest request, CancellationToken ct)
    {
        var result = await Sender.Send(new UpsertNotificationTemplateCommand(
            request.EventType, request.Channel,
            request.BodyTemplate, request.SubjectTemplate), ct);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record UpsertTemplateRequest(
    NotificationEventType EventType,
    NotificationChannel   Channel,
    string                BodyTemplate,
    string?               SubjectTemplate);
