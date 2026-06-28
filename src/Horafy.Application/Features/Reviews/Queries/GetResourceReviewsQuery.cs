using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Reviews.Queries;

public sealed record GetResourceReviewsQuery(
    Guid ResourceId,
    int  PageNumber = 1,
    int  PageSize   = 20) : IRequest<Result<ResourceReviewsResult>>;

public sealed record ReviewResult(
    Guid           Id,
    Guid           BookingId,
    Guid           CustomerId,
    int            Stars,
    string?        Comment,
    string?        OwnerReply,
    DateTimeOffset? OwnerRepliedAt,
    DateTimeOffset CreatedAt);

public sealed record ResourceReviewsResult(
    double                    AverageStars,
    int                       TotalReviews,
    PagedResult<ReviewResult> Page);

internal sealed class GetResourceReviewsQueryHandler(IReviewRepository reviewRepository)
    : IRequestHandler<GetResourceReviewsQuery, Result<ResourceReviewsResult>>
{
    public async Task<Result<ResourceReviewsResult>> Handle(
        GetResourceReviewsQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize   = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var (items, total) = await reviewRepository.GetByResourcePagedAsync(
            request.ResourceId, pageNumber, pageSize, cancellationToken);

        var (average, count) = await reviewRepository.GetRatingSummaryAsync(
            request.ResourceId, cancellationToken);

        var dtos = items
            .Select(r => new ReviewResult(
                r.Id, r.BookingId, r.CustomerId,
                r.Stars, r.Comment, r.OwnerReply, r.OwnerRepliedAt, r.CreatedAt))
            .ToList();

        var page = PagedResult<ReviewResult>.Create(dtos, total, pageNumber, pageSize);

        return Result.Success(new ResourceReviewsResult(average, count, page));
    }
}
