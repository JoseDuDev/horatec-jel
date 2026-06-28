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
                new { resourceId = result.Value.ResourceId }, result.Value.ReviewId)
            : ToActionResult(result);
    }

    /// <summary>
    /// Avaliações públicas de um recurso (paginadas) com média e total.
    /// Usado no catálogo público e no perfil do profissional.
    /// </summary>
    [HttpGet("resources/{resourceId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ResourceReviewsResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByResource(
        Guid resourceId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize   = 20,
        CancellationToken ct = default) =>
        ToActionResult(await Sender.Send(
            new GetResourceReviewsQuery(resourceId, pageNumber, pageSize), ct));

    /// <summary>Responde publicamente a uma avaliação (estabelecimento).</summary>
    [HttpPost("{reviewId:guid}/reply")]
    [Authorize(Roles = "TenantOwner,TenantAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reply(
        Guid reviewId, [FromBody] ReplyToReviewRequest request, CancellationToken ct)
    {
        var result = await Sender.Send(new ReplyToReviewCommand(reviewId, request.Reply), ct);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record CreateReviewRequest(Guid BookingId, int Stars, string? Comment);
public sealed record ReplyToReviewRequest(string Reply);
