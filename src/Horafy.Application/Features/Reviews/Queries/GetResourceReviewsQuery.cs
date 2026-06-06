using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Reviews.Queries;

public sealed record GetResourceReviewsQuery(Guid ResourceId)
    : IRequest<Result<IReadOnlyList<ReviewResult>>>;

public sealed record ReviewResult(
    Guid    Id,
    Guid    BookingId,
    Guid    CustomerId,
    int     Stars,
    string? Comment,
    DateTimeOffset CreatedAt);

internal sealed class GetResourceReviewsQueryHandler(IReviewRepository reviewRepository)
    : IRequestHandler<GetResourceReviewsQuery, Result<IReadOnlyList<ReviewResult>>>
{
    public async Task<Result<IReadOnlyList<ReviewResult>>> Handle(
        GetResourceReviewsQuery request, CancellationToken cancellationToken)
    {
        var reviews = await reviewRepository.GetByResourceAsync(
            request.ResourceId, cancellationToken);

        var results = reviews
            .Select(r => new ReviewResult(
                r.Id, r.BookingId, r.CustomerId,
                r.Stars, r.Comment, r.CreatedAt))
            .ToList();

        return Result.Success<IReadOnlyList<ReviewResult>>(results);
    }
}
