using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
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

        // Locação tem fluxo de notificação próprio (ver Fase 6) — evita conteúdo de agendamento.
        if (booking.Kind == BookingKind.Rental) return;

        var resource   = booking.ResourceId is { } rid
            ? await resourceRepository.GetByIdAsync(rid, cancellationToken)
            : null;
        var tenantName = "Horafy";
        var tenantSlug = currentTenant.Slug ?? "horafy";

        if (currentTenant.TenantId.HasValue)
        {
            var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
            if (tenant is not null) tenantName = tenant.Name;
        }

        var serviceName = booking.Services.FirstOrDefault()?.ServiceName
                          ?? booking.ServiceId?.ToString() ?? "Reserva";

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
