using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Favorites.Commands;
using Horafy.Application.Features.Favorites.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize(Roles = "Customer")]
[Route("api/v{version:apiVersion}/customers/favorites")]
public sealed class FavoriteServicesController(ISender sender)
    : ApiControllerBase(sender)
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FavoriteServiceResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        ToActionResult(await Sender.Send(new GetCustomerFavoritesQuery(), ct));

    [HttpPost("{serviceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Add(Guid serviceId, CancellationToken ct)
    {
        var result = await Sender.Send(new AddFavoriteServiceCommand(serviceId), ct);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpDelete("{serviceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid serviceId, CancellationToken ct)
    {
        var result = await Sender.Send(new RemoveFavoriteServiceCommand(serviceId), ct);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}
