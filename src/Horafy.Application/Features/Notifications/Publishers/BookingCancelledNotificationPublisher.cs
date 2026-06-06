using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using MassTransit;
using MediatR;

namespace Horafy.Application.Features.Notifications.Publishers;

internal sealed class BookingCancelledNotificationPublisher(
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenant,
    IPublishEndpoint      publishEndpoint)
    : INotificationHandler<BookingCancelledEvent>
{
    public async Task Handle(BookingCancelledEvent notification, CancellationToken cancellationToken)
    {
        var tenantName = "Horafy";
        var tenantSlug = currentTenant.Slug ?? "horafy";

        if (currentTenant.TenantId.HasValue)
        {
            var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
            if (tenant is not null) tenantName = tenant.Name;
        }

        var reason = string.IsNullOrWhiteSpace(notification.Reason)
            ? string.Empty
            : $"Motivo: {notification.Reason}. ";

        await publishEndpoint.Publish(new BookingCancelledMessage(
            BookingId:     notification.BookingId,
            CustomerName:  "Cliente",
            CustomerEmail: string.Empty,
            CustomerPhone: notification.CustomerPhone,
            Reason:        reason,
            TenantSlug:    tenantSlug,
            TenantName:    tenantName), cancellationToken);
    }
}
