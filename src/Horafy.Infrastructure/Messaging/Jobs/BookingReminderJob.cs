using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Horafy.Infrastructure.Messaging.Jobs;

[DisallowConcurrentExecution]
public sealed class BookingReminderJob(
    IServiceScopeFactory         scopeFactory,
    IBus                         bus,
    ILogger<BookingReminderJob>? logger = null) : IJob
{
    public async Task Execute(IJobExecutionContext context) =>
        await ExecuteAsync(DateTimeOffset.UtcNow, context.CancellationToken);

    public async Task ExecuteAsync(DateTimeOffset now, CancellationToken ct)
    {
        var oneDayMin = now.AddHours(22);
        var oneDayMax = now.AddHours(26);
        var twoHrMin  = now.AddHours(1);
        var twoHrMax  = now.AddHours(3);

        IReadOnlyList<Domain.Entities.Tenants.Tenant> tenants;
        using (var scope = scopeFactory.CreateScope())
        {
            var tenantRepo = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
            tenants = await tenantRepo.GetAllAsync(ct);
        }

        foreach (var tenant in tenants.Where(t => t.Status == TenantStatus.Active))
        {
            using var tenantScope = scopeFactory.CreateScope();

            var tenantSvc = tenantScope.ServiceProvider.GetRequiredService<ICurrentTenantService>();
            tenantSvc.SetTenant(tenant.Id, tenant.SchemaName, tenant.Slug);

            var bookingRepo  = tenantScope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var resourceRepo = tenantScope.ServiceProvider.GetRequiredService<IResourceRepository>();

            var oneDayBookings = await bookingRepo.FindAsync(
                b => b.Status == BookingStatus.Confirmed &&
                     b.ScheduledAt >= oneDayMin && b.ScheduledAt <= oneDayMax, ct);

            var twoHourBookings = await bookingRepo.FindAsync(
                b => b.Status == BookingStatus.Confirmed &&
                     b.ScheduledAt >= twoHrMin && b.ScheduledAt <= twoHrMax, ct);

            foreach (var (booking, isOneDay) in
                oneDayBookings.Select(b => (b, true))
                .Concat(twoHourBookings.Select(b => (b, false))))
            {
                var resource    = await resourceRepo.GetByIdAsync(booking.ResourceId, ct);
                var serviceName = booking.Services.FirstOrDefault()?.ServiceName
                                  ?? booking.ServiceId.ToString();

                var msg = new BookingReminderMessage(
                    BookingId:      booking.Id,
                    CustomerName:   booking.CustomerName,
                    CustomerEmail:  booking.CustomerEmail,
                    CustomerPhone:  booking.CustomerPhone,
                    ServiceName:    serviceName,
                    ResourceName:   resource?.Name ?? "Profissional",
                    ScheduledAt:    booking.ScheduledAt,
                    TenantSlug:     tenant.Slug,
                    TenantName:     tenant.Name,
                    IsOneDayBefore: isOneDay);

                await bus.Publish(msg, ct);

                logger?.LogInformation(
                    "Lembrete {Type} publicado para booking {Id} (tenant {Slug})",
                    isOneDay ? "D-1" : "H-2", booking.Id, tenant.Slug);
            }
        }
    }
}
