using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Queries;

public sealed record GetBookingByIdQuery(Guid Id) : IRequest<Result<BookingResult>>;

internal sealed class GetBookingByIdQueryHandler(
    IBookingRepository bookingRepository)
    : IRequestHandler<GetBookingByIdQuery, Result<BookingResult>>
{
    public async Task<Result<BookingResult>> Handle(
        GetBookingByIdQuery request, CancellationToken cancellationToken)
    {
        var b = await bookingRepository.GetByIdAsync(request.Id, cancellationToken);
        if (b is null) return Result.Failure<BookingResult>(BookingErrors.NotFound);

        return Result.Success(new BookingResult(
            b.Id, b.ServiceId, b.ResourceId, b.CustomerId,
            b.CustomerName, b.CustomerEmail, b.ScheduledAt, b.EndsAt,
            b.DurationMinutes, b.Notes, b.Status, b.CancellationReason));
    }
}
