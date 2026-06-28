using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using MassTransit;
using MediatR;

namespace Horafy.Application.Features.Notifications.Publishers;

internal sealed class WaitlistPromotedNotificationPublisher(
    IWaitlistRepository   waitlistRepository,
    IServiceRepository    serviceRepository,
    IResourceRepository   resourceRepository,
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenant,
    IPublishEndpoint      publishEndpoint)
    : INotificationHandler<WaitlistPromotedEvent>
{
    public async Task Handle(WaitlistPromotedEvent notification, CancellationToken ct)
    {
        var entry = await waitlistRepository.GetByIdAsync(notification.WaitlistEntryId, ct);
        if (entry is null) return;

        var service    = await serviceRepository.GetByIdAsync(notification.ServiceId, ct);
        var resource   = await resourceRepository.GetByIdAsync(notification.ResourceId, ct);
        var tenantName = "Horafy";
        var tenantSlug = currentTenant.Slug ?? "horafy";

        if (currentTenant.TenantId.HasValue)
        {
            var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, ct);
            if (tenant is not null) tenantName = tenant.Name;
        }

        await publishEndpoint.Publish(new WaitlistSlotAvailableMessage(
            WaitlistEntryId: entry.Id,
            CustomerName:    entry.CustomerName,
            CustomerEmail:   entry.CustomerEmail,
            ServiceName:     service?.Name ?? "Serviço",
            ResourceName:    resource?.Name ?? "Profissional",
            PreferredDate:   notification.PreferredDate,
            TenantSlug:      tenantSlug,
            TenantName:      tenantName), ct);
    }
}
