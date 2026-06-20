using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Queries;

public sealed record GetBookingsQuery(
    Guid? ResourceId,
    DateTimeOffset? From,
    DateTimeOffset? To) : IRequest<Result<IReadOnlyList<BookingResult>>>;

public sealed record BookingServiceResult(Guid ServiceId, string ServiceName, int DurationMinutes);

public sealed record BookingResult(
    Guid Id,
    Guid ServiceId,
    Guid ResourceId,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    DateTimeOffset ScheduledAt,
    DateTimeOffset EndsAt,
    int DurationMinutes,
    string? Notes,
    BookingStatus Status,
    string? CancellationReason,
    Guid? RecurrenceGroupId,
    IReadOnlyList<BookingServiceResult> Services,
    string ServiceName,
    string ResourceName,
    decimal TotalAmount);

internal sealed class GetBookingsQueryHandler(
    IBookingRepository bookingRepository)
    : IRequestHandler<GetBookingsQuery, Result<IReadOnlyList<BookingResult>>>
{
    public async Task<Result<IReadOnlyList<BookingResult>>> Handle(
        GetBookingsQuery request, CancellationToken cancellationToken)
    {
        var from = request.From ?? DateTimeOffset.UtcNow.Date;
        var to   = request.To   ?? from.AddDays(7);

        IReadOnlyList<Domain.Entities.Bookings.Booking> bookings = request.ResourceId.HasValue
            ? await bookingRepository.GetByResourceAsync(
                request.ResourceId.Value, from, to, cancellationToken)
            : await bookingRepository.FindAsync(
                b => b.ScheduledAt >= from && b.ScheduledAt < to, cancellationToken);

        var result = bookings.Select(ToResult).ToList();
        return Result.Success<IReadOnlyList<BookingResult>>(result);
    }

    private static BookingResult ToResult(Domain.Entities.Bookings.Booking b) => new(
        b.Id, b.ServiceId, b.ResourceId, b.CustomerId,
        b.CustomerName, b.CustomerEmail, b.ScheduledAt, b.EndsAt,
        b.DurationMinutes, b.Notes, b.Status, b.CancellationReason,
        b.RecurrenceGroupId,
        b.Services.Select(s => new BookingServiceResult(s.ServiceId, s.ServiceName, s.DurationMinutes)).ToList(),
        ServiceName:  string.Join(", ", b.Services.Select(s => s.ServiceName)),
        ResourceName: b.ResourceName,
        TotalAmount:  b.TotalAmount);
}
