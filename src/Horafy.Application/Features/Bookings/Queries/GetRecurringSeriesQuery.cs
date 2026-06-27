using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Queries;

public sealed record GetRecurringSeriesQuery(Guid RecurrenceGroupId)
    : IRequest<Result<IReadOnlyList<BookingResult>>>;

internal sealed class GetRecurringSeriesQueryHandler(
    IBookingRepository bookingRepository)
    : IRequestHandler<GetRecurringSeriesQuery, Result<IReadOnlyList<BookingResult>>>
{
    public async Task<Result<IReadOnlyList<BookingResult>>> Handle(
        GetRecurringSeriesQuery request, CancellationToken cancellationToken)
    {
        var bookings = await bookingRepository.GetByRecurrenceGroupAsync(
            request.RecurrenceGroupId, cancellationToken);

        if (bookings.Count == 0)
            return Result.Failure<IReadOnlyList<BookingResult>>(BookingErrors.NotFound);

        var result = bookings.Select(b => new BookingResult(
            b.Id, b.ServiceId, b.ResourceId, b.CustomerId,
            b.CustomerName, b.CustomerEmail, b.ScheduledAt, b.EndsAt,
            b.DurationMinutes, b.Notes, b.Status, b.CancellationReason,
            b.RecurrenceGroupId,
            b.Services.Select(s => new BookingServiceResult(s.ServiceId, s.ServiceName, s.DurationMinutes)).ToList(),
            ServiceName:  string.Join(", ", b.Services.Select(s => s.ServiceName)),
            ResourceName: b.ResourceName,
            TotalAmount:  b.TotalAmount,
            Kind:         b.Kind,
            RentalStatus: b.RentalStatus))
            .ToList();

        return Result.Success<IReadOnlyList<BookingResult>>(result);
    }
}
