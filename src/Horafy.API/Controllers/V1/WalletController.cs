using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Wallet.Commands.AddCredits;
using Horafy.Application.Features.Wallet.Queries.GetWallet;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
public sealed class WalletController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(WalletResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyWallet(CancellationToken ct) =>
        ToActionResult(await Sender.Send(new GetWalletQuery(), ct));

    [HttpPost("users/{userId:guid}/credits")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddCredits(
        Guid userId,
        [FromBody] AddCreditsRequest request,
        CancellationToken ct) =>
        ToActionResult(await Sender.Send(new AddCreditsCommand(userId, request.Amount, request.Description), ct));
}

public sealed record AddCreditsRequest(decimal Amount, string Description);
