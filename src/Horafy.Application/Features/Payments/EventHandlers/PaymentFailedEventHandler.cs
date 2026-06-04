using Horafy.Domain.Events.Payments;
using MediatR;

namespace Horafy.Application.Features.Payments.EventHandlers;

internal sealed class PaymentFailedEventHandler : INotificationHandler<PaymentFailedEvent>
{
    public Task Handle(PaymentFailedEvent notification, CancellationToken cancellationToken)
    {
        // Booking stays Pending — customer can retry payment
        return Task.CompletedTask;
    }
}
