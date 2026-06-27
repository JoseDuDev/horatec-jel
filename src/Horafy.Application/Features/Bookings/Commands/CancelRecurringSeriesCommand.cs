using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Commands;

public sealed record CancelRecurringSeriesCommand(
    Guid RecurrenceGroupId,
    string? Reason,
    DateTimeOffset? FromDate = null) : IRequest<Result>;

internal sealed class CancelRecurringSeriesCommandHandler(
    IBookingRepository bookingRepository,
    ITenantUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
    : IRequestHandler<CancelRecurringSeriesCommand, Result>
{
    public async Task<Result> Handle(
        CancelRecurringSeriesCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
            return Result.Failure(Error.Unauthorized);

        var series = await bookingRepository.GetByRecurrenceGroupAsync(
            request.RecurrenceGroupId, cancellationToken);

        if (series.Count == 0)
            return Result.Failure(BookingErrors.NotFound);

        var cutoff = request.FromDate ?? DateTimeOffset.UtcNow;

        var toCancel = series
            .Where(b => b.ScheduledAt >= cutoff
                     && b.Status is BookingStatus.Pending or BookingStatus.Confirmed)
            .ToList();

        if (toCancel.Count == 0)
            return Result.Success();

        foreach (var booking in toCancel)
        {
            booking.Cancel(request.Reason);
            bookingRepository.Update(booking);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
