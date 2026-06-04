using Horafy.Application.Interfaces;
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using MediatR;

namespace Horafy.Application.Features.Waitlist.EventHandlers;

internal sealed class BookingCancelledEventHandler(
    IBookingRepository bookingRepository,
    IWaitlistRepository waitlistRepository,
    ITenantUnitOfWork unitOfWork)
    : INotificationHandler<BookingCancelledEvent>
{
    public async Task Handle(BookingCancelledEvent notification, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);
        if (booking is null) return;

        var date = DateOnly.FromDateTime(booking.ScheduledAt.Date);

        var waitingEntries = await waitlistRepository.GetByServiceResourceDateAsync(
            booking.ServiceId, booking.ResourceId, date, cancellationToken);

        if (!waitingEntries.Any()) return;

        var first = waitingEntries[0];
        first.Promote();
        waitlistRepository.Update(first);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
