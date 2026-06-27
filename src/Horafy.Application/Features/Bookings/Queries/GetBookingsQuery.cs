using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Queries;

public sealed record GetBookingsQuery(
    Guid? ResourceId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int PageNumber = 1,
    int PageSize   = 20) : IRequest<Result<PagedResult<BookingResult>>>;

public sealed record BookingServiceResult(Guid ServiceId, string ServiceName, int DurationMinutes);

public sealed record BookingResult(
    Guid Id,
    Guid? ServiceId,
    Guid? ResourceId,
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
    decimal TotalAmount,
    BookingKind Kind = BookingKind.Appointment,
    RentalLifecycle? RentalStatus = null);

internal sealed class GetBookingsQueryHandler(
    IBookingRepository bookingRepository)
    : IRequestHandler<GetBookingsQuery, Result<PagedResult<BookingResult>>>
{
    public async Task<Result<PagedResult<BookingResult>>> Handle(
        GetBookingsQuery request, CancellationToken cancellationToken)
    {
        var from = request.From ?? DateTimeOffset.UtcNow.Date;
        var to   = request.To   ?? from.AddDays(7);

        var (bookings, total) = await bookingRepository.GetPagedAsync(
            request.ResourceId, from, to, request.PageNumber, request.PageSize, cancellationToken);

        var items = bookings.Select(ToResult).ToList();
        return Result.Success(PagedResult<BookingResult>.Create(items, total, request.PageNumber, request.PageSize));
    }

    private static BookingResult ToResult(Domain.Entities.Bookings.Booking b) => new(
        b.Id, b.ServiceId, b.ResourceId, b.CustomerId,
        b.CustomerName, b.CustomerEmail, b.ScheduledAt, b.EndsAt,
        b.DurationMinutes, b.Notes, b.Status, b.CancellationReason,
        b.RecurrenceGroupId,
        b.Services.Select(s => new BookingServiceResult(s.ServiceId, s.ServiceName, s.DurationMinutes)).ToList(),
        ServiceName:  string.Join(", ", b.Services.Select(s => s.ServiceName)),
        ResourceName: b.ResourceName,
        TotalAmount:  b.TotalAmount,
        Kind:         b.Kind,
        RentalStatus: b.RentalStatus);
}
