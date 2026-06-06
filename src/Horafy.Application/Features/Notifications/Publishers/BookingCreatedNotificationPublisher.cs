using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using MassTransit;
using MediatR;

namespace Horafy.Application.Features.Notifications.Publishers;

internal sealed class BookingCreatedNotificationPublisher(
    IBookingRepository    bookingRepository,
    IResourceRepository   resourceRepository,
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenant,
    IPublishEndpoint      publishEndpoint)
    : INotificationHandler<BookingCreatedEvent>
{
    public async Task Handle(BookingCreatedEvent notification, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);
        if (booking is null) return;

        var resource   = await resourceRepository.GetByIdAsync(notification.ResourceId, cancellationToken);
        var tenantName = "Horafy";
        var tenantSlug = currentTenant.Slug ?? "horafy";

        if (currentTenant.TenantId.HasValue)
        {
            var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
            if (tenant is not null) tenantName = tenant.Name;
        }

        var serviceName = booking.Services.FirstOrDefault()?.ServiceName
                          ?? booking.ServiceId.ToString();

        await publishEndpoint.Publish(new BookingCreatedMessage(
            BookingId:     booking.Id,
            CustomerName:  booking.CustomerName,
            CustomerEmail: booking.CustomerEmail,
            CustomerPhone: null,
            ServiceName:   serviceName,
            ResourceName:  resource?.Name ?? "Profissional",
            ScheduledAt:   booking.ScheduledAt,
            TenantSlug:    tenantSlug,
            TenantName:    tenantName), cancellationToken);
    }
}
