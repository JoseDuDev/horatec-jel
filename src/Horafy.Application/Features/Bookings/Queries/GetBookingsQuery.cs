using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Queries;

public sealed record GetBookingsQuery(
    Guid? ProfessionalId,
    DateTimeOffset? From,
    DateTimeOffset? To) : IRequest<Result<IReadOnlyList<BookingResult>>>;

public sealed record BookingResult(
    Guid Id,
    Guid ServiceId,
    Guid ProfessionalId,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    DateTimeOffset ScheduledAt,
    DateTimeOffset EndsAt,
    int DurationMinutes,
    string? Notes,
    BookingStatus Status,
    string? CancellationReason);

internal sealed class GetBookingsQueryHandler(
    IBookingRepository bookingRepository)
    : IRequestHandler<GetBookingsQuery, Result<IReadOnlyList<BookingResult>>>
{
    public async Task<Result<IReadOnlyList<BookingResult>>> Handle(
        GetBookingsQuery request, CancellationToken cancellationToken)
    {
        var from = request.From ?? DateTimeOffset.UtcNow.Date;
        var to   = request.To   ?? from.AddDays(7);

        IReadOnlyList<Domain.Entities.Bookings.Booking> bookings;

        if (request.ProfessionalId.HasValue)
        {
            bookings = await bookingRepository.GetByProfessionalAsync(
                request.ProfessionalId.Value, from, to, cancellationToken);
        }
        else
        {
            bookings = await bookingRepository.FindAsync(
                b => b.ScheduledAt >= from && b.ScheduledAt < to, cancellationToken);
        }

        var result = bookings.Select(ToResult).ToList();
        return Result.Success<IReadOnlyList<BookingResult>>(result);
    }

    private static BookingResult ToResult(Domain.Entities.Bookings.Booking b) => new(
        b.Id, b.ServiceId, b.ProfessionalId, b.CustomerId,
        b.CustomerName, b.CustomerEmail, b.ScheduledAt, b.EndsAt,
        b.DurationMinutes, b.Notes, b.Status, b.CancellationReason);
}
