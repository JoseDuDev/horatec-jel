using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Events.Payments;
using Horafy.Domain.Interfaces.Repositories;
using MassTransit;
using MediatR;

namespace Horafy.Application.Features.Notifications.Publishers;

internal sealed class PaymentConfirmedNotificationPublisher(
    IPaymentRepository    paymentRepository,
    IBookingRepository    bookingRepository,
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenant,
    IPublishEndpoint      publishEndpoint)
    : INotificationHandler<PaymentConfirmedEvent>
{
    public async Task Handle(PaymentConfirmedEvent notification, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByIdAsync(notification.PaymentId, cancellationToken);
        if (payment is null) return;

        var booking = await bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);
        if (booking is null) return;

        var tenantName = "Horafy";
        var tenantSlug = currentTenant.Slug ?? "horafy";

        if (currentTenant.TenantId.HasValue)
        {
            var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
            if (tenant is not null) tenantName = tenant.Name;
        }

        await publishEndpoint.Publish(new PaymentConfirmedMessage(
            PaymentId:     payment.Id,
            BookingId:     notification.BookingId,
            CustomerName:  booking.CustomerName,
            CustomerEmail: booking.CustomerEmail,
            CustomerPhone: booking.CustomerPhone,
            Amount:        payment.Amount,
            TenantSlug:    tenantSlug,
            TenantName:    tenantName), cancellationToken);
    }
}
