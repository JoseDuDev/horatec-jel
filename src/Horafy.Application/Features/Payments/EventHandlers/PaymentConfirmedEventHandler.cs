using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Events.Payments;
using Horafy.Domain.Interfaces.Repositories;
using MediatR;

namespace Horafy.Application.Features.Payments.EventHandlers;

internal sealed class PaymentConfirmedEventHandler(
    IBookingRepository bookingRepository,
    ITenantUnitOfWork unitOfWork)
    : INotificationHandler<PaymentConfirmedEvent>
{
    public async Task Handle(PaymentConfirmedEvent notification, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);
        if (booking is null) return;

        if (booking.Status == BookingStatus.Pending)
            booking.Confirm();

        if (notification.IsDeposit)
            booking.MarkPaymentPartial();
        else
            booking.MarkPaymentPaid();

        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
