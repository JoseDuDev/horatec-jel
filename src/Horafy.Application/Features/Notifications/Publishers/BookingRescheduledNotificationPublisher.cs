using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using MassTransit;
using MediatR;

namespace Horafy.Application.Features.Notifications.Publishers;

internal sealed class BookingRescheduledNotificationPublisher(
    IBookingRepository    bookingRepository,
    IResourceRepository   resourceRepository,
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenant,
    IPublishEndpoint      publishEndpoint)
    : INotificationHandler<BookingRescheduledEvent>
{
    public async Task Handle(BookingRescheduledEvent notification, CancellationToken ct)
    {
        var booking = await bookingRepository.GetByIdAsync(notification.BookingId, ct);
        if (booking is null) return;

        var resource   = booking.ResourceId.HasValue
            ? await resourceRepository.GetByIdAsync(booking.ResourceId.Value, ct)
            : null;
        var tenantName = "Horafy";
        var tenantSlug = currentTenant.Slug ?? "horafy";

        if (currentTenant.TenantId.HasValue)
        {
            var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, ct);
            if (tenant is not null) tenantName = tenant.Name;
        }

        var serviceName = booking.Services.FirstOrDefault()?.ServiceName ?? "Reserva";

        await publishEndpoint.Publish(new BookingRescheduledMessage(
            BookingId:      booking.Id,
            CustomerName:   booking.CustomerName,
            CustomerEmail:  booking.CustomerEmail,
            CustomerPhone:  booking.CustomerPhone,
            ServiceName:    serviceName,
            ResourceName:   resource?.Name ?? "Profissional",
            NewScheduledAt: notification.NewScheduledAt,
            TenantSlug:     tenantSlug,
            TenantName:     tenantName), ct);
    }
}
