using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Queries;

public sealed record GetMyBookingsQuery : IRequest<Result<IReadOnlyList<BookingResult>>>;

internal sealed class GetMyBookingsQueryHandler(
    IBookingRepository bookingRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetMyBookingsQuery, Result<IReadOnlyList<BookingResult>>>
{
    public async Task<Result<IReadOnlyList<BookingResult>>> Handle(
        GetMyBookingsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
            return Result.Failure<IReadOnlyList<BookingResult>>(Error.Unauthorized);

        var bookings = await bookingRepository.GetByCustomerAsync(
            currentUser.UserId.Value, cancellationToken);

        var result = bookings.Select(b => new BookingResult(
            b.Id, b.ServiceId, b.ResourceId, b.CustomerId,
            b.CustomerName, b.CustomerEmail, b.ScheduledAt, b.EndsAt,
            b.DurationMinutes, b.Notes, b.Status, b.CancellationReason))
            .ToList();

        return Result.Success<IReadOnlyList<BookingResult>>(result);
    }
}
