using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Reviews;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Reviews.Commands;

public sealed record CreateReviewCommand(
    Guid    BookingId,
    int     Stars,
    string? Comment) : IRequest<Result<Guid>>;

internal sealed class CreateReviewCommandHandler(
    ICurrentUserService currentUserService,
    IBookingRepository  bookingRepository,
    IReviewRepository   reviewRepository,
    ITenantUnitOfWork   unitOfWork)
    : IRequestHandler<CreateReviewCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateReviewCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
            return Result.Failure<Guid>(new Error(
                "Customer.Unauthorized", "Usuário não autenticado.", ErrorType.Unauthorized));

        var booking = await bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking is null)
            return Result.Failure<Guid>(new Error(
                "Review.BookingNotFound", "Agendamento não encontrado.", ErrorType.NotFound));

        if (booking.CustomerId != currentUserService.UserId.Value)
            return Result.Failure<Guid>(new Error(
                "Review.NotYourBooking",
                "Você só pode avaliar seus próprios agendamentos.",
                ErrorType.Unauthorized));

        if (booking.Status != BookingStatus.Completed)
            return Result.Failure<Guid>(new Error(
                "Review.BookingNotCompleted",
                "Só é possível avaliar agendamentos concluídos.",
                ErrorType.Validation));

        var existing = await reviewRepository.GetByBookingAsync(request.BookingId, cancellationToken);
        if (existing is not null)
            return Result.Failure<Guid>(new Error(
                "Review.AlreadyReviewed",
                "Este agendamento já foi avaliado.",
                ErrorType.Conflict));

        if (booking.ResourceId is not { } resourceId)
            return Result.Failure<Guid>(new Error(
                "Review.RentalNotReviewable",
                "Locações não podem ser avaliadas.",
                ErrorType.Validation));

        var review = Review.Create(
            request.BookingId, resourceId,
            currentUserService.UserId.Value, request.Stars, request.Comment);

        reviewRepository.Add(review);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(review.Id);
    }
}
