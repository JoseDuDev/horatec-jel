using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Payments.Commands;
using Horafy.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
public sealed class WebhooksController(ISender sender, IPaymentGateway gateway)
    : ApiControllerBase(sender)
{
    [HttpPost("mercadopago")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> MercadoPago(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();

        var xSignature = Request.Headers["x-signature"].FirstOrDefault() ?? string.Empty;
        var xRequestId = Request.Headers["x-request-id"].FirstOrDefault() ?? string.Empty;

        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);

        var payload = JsonSerializer.Deserialize<MpWebhookPayload>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (payload?.Type == "payment" && payload.Data?.Id is { } mpPaymentId)
        {
            if (!string.IsNullOrEmpty(xSignature)
                && !gateway.ValidateWebhookSignature(mpPaymentId, xRequestId, xSignature))
                return Unauthorized();

            var result = await Sender.Send(new ConfirmPaymentCommand(mpPaymentId), cancellationToken);
            return result.IsSuccess ? Ok() : BadRequest(result.Error.Description);
        }

        return Ok();
    }
}

public sealed record MpWebhookPayload(string? Type, MpWebhookData? Data);
public sealed record MpWebhookData(string? Id);
