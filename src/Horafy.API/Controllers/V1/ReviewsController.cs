using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Reviews.Commands;
using Horafy.Application.Features.Reviews.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
public sealed class ReviewsController(ISender sender)
    : ApiControllerBase(sender)
{
    [HttpPost]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateReviewRequest request, CancellationToken ct)
    {
        var result = await Sender.Send(
            new CreateReviewCommand(request.BookingId, request.Stars, request.Comment), ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetByResource),
                new { resourceId = result.Value }, result.Value)
            : ToActionResult(result);
    }

    [HttpGet("resources/{resourceId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<ReviewResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByResource(Guid resourceId, CancellationToken ct) =>
        ToActionResult(await Sender.Send(new GetResourceReviewsQuery(resourceId), ct));
}

public sealed record CreateReviewRequest(Guid BookingId, int Stars, string? Comment);
