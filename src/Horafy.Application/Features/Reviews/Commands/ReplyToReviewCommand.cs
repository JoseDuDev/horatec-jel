using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Reviews.Commands;

/// <summary>Resposta pública do estabelecimento a uma avaliação.</summary>
public sealed record ReplyToReviewCommand(
    Guid   ReviewId,
    string Reply) : IRequest<Result>;

internal sealed class ReplyToReviewCommandHandler(
    IReviewRepository reviewRepository,
    ITenantUnitOfWork unitOfWork)
    : IRequestHandler<ReplyToReviewCommand, Result>
{
    public async Task<Result> Handle(
        ReplyToReviewCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reply))
            return Result.Failure(new Error(
                "Review.EmptyReply", "A resposta não pode ser vazia.", ErrorType.Validation));

        var review = await reviewRepository.GetByIdAsync(request.ReviewId, cancellationToken);
        if (review is null)
            return Result.Failure(new Error(
                "Review.NotFound", "Avaliação não encontrada.", ErrorType.NotFound));

        review.Reply(request.Reply);
        reviewRepository.Update(review);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
