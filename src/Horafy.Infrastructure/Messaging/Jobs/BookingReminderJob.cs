using Horafy.Application.Features.Notifications.Messages;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Horafy.Infrastructure.Messaging.Jobs;

[DisallowConcurrentExecution]
public sealed class BookingReminderJob(
    IBookingRepository  bookingRepository,
    IResourceRepository resourceRepository,
    ITenantRepository   tenantRepository,
    IBus                bus,
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

        var oneDayBookings = await bookingRepository.FindAsync(
            b => b.Status == BookingStatus.Confirmed &&
                 b.ScheduledAt >= oneDayMin && b.ScheduledAt <= oneDayMax, ct);

        var twoHourBookings = await bookingRepository.FindAsync(
            b => b.Status == BookingStatus.Confirmed &&
                 b.ScheduledAt >= twoHrMin && b.ScheduledAt <= twoHrMax, ct);

        var tenants = await tenantRepository.GetAllAsync(ct);
        var tenantMap = tenants.ToDictionary(t => t.Id, t => t.Name);

        foreach (var (booking, isOneDay) in
            oneDayBookings.Select(b => (b, true))
            .Concat(twoHourBookings.Select(b => (b, false))))
        {
            var resource    = await resourceRepository.GetByIdAsync(booking.ResourceId, ct);
            var serviceName = booking.Services.FirstOrDefault()?.ServiceName
                              ?? booking.ServiceId.ToString();

            var msg = new BookingReminderMessage(
                BookingId:      booking.Id,
                CustomerName:   booking.CustomerName,
                CustomerEmail:  booking.CustomerEmail,
                CustomerPhone:  null,
                ServiceName:    serviceName,
                ResourceName:   resource?.Name ?? "Profissional",
                ScheduledAt:    booking.ScheduledAt,
                TenantSlug:     "horafy",
                TenantName:     "Horafy",
                IsOneDayBefore: isOneDay);

            await bus.Publish(msg, ct);

            logger?.LogInformation(
                "Lembrete {Type} publicado para booking {Id}",
                isOneDay ? "D-1" : "H-2", booking.Id);
        }
    }
}
