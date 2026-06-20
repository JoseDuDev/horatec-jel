using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Customers.Queries;

public sealed record GetCustomerBookingsQuery : IRequest<Result<IReadOnlyList<CustomerBookingResult>>>;

public sealed record CustomerBookingResult(
    Guid                 Id,
    Guid                 ServiceId,
    Guid                 ResourceId,
    Guid                 CustomerId,
    DateTimeOffset       ScheduledAt,
    DateTimeOffset       EndsAt,
    int                  DurationMinutes,
    string?              Notes,
    BookingStatus        Status,
    BookingPaymentStatus PaymentStatus,
    IReadOnlyList<CustomerBookingServiceResult> Services,
    string               ServiceName,
    string               ResourceName,
    decimal              TotalAmount);

public sealed record CustomerBookingServiceResult(
    Guid   ServiceId,
    string ServiceName,
    int    DurationMinutes);

internal sealed class GetCustomerBookingsQueryHandler(
    ICurrentUserService currentUserService,
    IBookingRepository  bookingRepository)
    : IRequestHandler<GetCustomerBookingsQuery, Result<IReadOnlyList<CustomerBookingResult>>>
{
    public async Task<Result<IReadOnlyList<CustomerBookingResult>>> Handle(
        GetCustomerBookingsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
            return Result.Failure<IReadOnlyList<CustomerBookingResult>>(Error.Unauthorized);

        var bookings = await bookingRepository.GetByCustomerAsync(
            currentUserService.UserId.Value, cancellationToken);

        var results = bookings
            .Select(b => new CustomerBookingResult(
                b.Id, b.ServiceId, b.ResourceId, b.CustomerId,
                b.ScheduledAt, b.EndsAt, b.DurationMinutes, b.Notes,
                b.Status, b.PaymentStatus,
                b.Services
                    .Select(s => new CustomerBookingServiceResult(
                        s.ServiceId, s.ServiceName, s.DurationMinutes))
                    .ToList(),
                ServiceName:  string.Join(", ", b.Services.Select(s => s.ServiceName)),
                ResourceName: b.ResourceName,
                TotalAmount:  b.TotalAmount))
            .ToList();

        return Result.Success<IReadOnlyList<CustomerBookingResult>>(results);
    }
}
