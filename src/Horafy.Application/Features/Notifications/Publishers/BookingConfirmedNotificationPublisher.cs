using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using MassTransit;
using MediatR;

namespace Horafy.Application.Features.Notifications.Publishers;

internal sealed class BookingConfirmedNotificationPublisher(
    IBookingRepository    bookingRepository,
    IResourceRepository   resourceRepository,
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenant,
    IPublishEndpoint      publishEndpoint)
    : INotificationHandler<BookingConfirmedEvent>
{
    public async Task Handle(BookingConfirmedEvent notification, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);
        if (booking is null) return;

        var resource   = await resourceRepository.GetByIdAsync(booking.ResourceId, cancellationToken);
        var tenantName = "Horafy";
        var tenantSlug = currentTenant.Slug ?? "horafy";

        if (currentTenant.TenantId.HasValue)
        {
            var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
            if (tenant is not null) tenantName = tenant.Name;
        }

        var serviceName = booking.Services.FirstOrDefault()?.ServiceName
                          ?? booking.ServiceId.ToString();

        await publishEndpoint.Publish(new BookingConfirmedMessage(
            BookingId:     booking.Id,
            CustomerName:  notification.CustomerName,
            CustomerEmail: notification.CustomerEmail,
            CustomerPhone: booking.CustomerPhone,
            ServiceName:   serviceName,
            ResourceName:  resource?.Name ?? "Profissional",
            ScheduledAt:   notification.ScheduledAt,
            TenantSlug:    tenantSlug,
            TenantName:    tenantName), cancellationToken);
    }
}
