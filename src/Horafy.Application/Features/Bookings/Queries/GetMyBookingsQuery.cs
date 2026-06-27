using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Queries;

public sealed record GetMyBookingsQuery(
    int PageNumber = 1,
    int PageSize   = 20) : IRequest<Result<PagedResult<BookingResult>>>;

internal sealed class GetMyBookingsQueryHandler(
    IBookingRepository bookingRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetMyBookingsQuery, Result<PagedResult<BookingResult>>>
{
    public async Task<Result<PagedResult<BookingResult>>> Handle(
        GetMyBookingsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
            return Result.Failure<PagedResult<BookingResult>>(Error.Unauthorized);

        var (bookings, total) = await bookingRepository.GetByCustomerPagedAsync(
            currentUser.UserId.Value, request.PageNumber, request.PageSize, cancellationToken);

        var items = bookings.Select(b => new BookingResult(
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

        return Result.Success(PagedResult<BookingResult>.Create(items, total, request.PageNumber, request.PageSize));
    }
}
