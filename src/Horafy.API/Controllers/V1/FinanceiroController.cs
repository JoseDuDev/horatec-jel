using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Payments.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
public sealed class FinanceiroController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentTransactionResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReport(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] Guid? serviceId,
        [FromQuery] Guid? resourceId,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new GetFinancialReportQuery(from, to, serviceId, resourceId), cancellationToken));

    [HttpGet("summary")]
    [ProducesResponseType(typeof(FinancialSummaryResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetFinancialSummaryQuery(from, to), cancellationToken));
}
