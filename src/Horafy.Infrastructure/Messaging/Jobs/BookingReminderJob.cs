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

            // Lembretes de agendamento: janelas vêm das configurações do tenant.
            // Mantém a largura histórica (1º ±2h, 2º ±1h) para não duplicar entre execuções horárias.
            var reminders = tenant.ReminderSettings;
            if (reminders.Enabled)
            {
                var pending = new List<(Booking Booking, bool IsFirst)>();

                if (reminders.FirstReminderHours > 0)
                {
                    var min = now.AddHours(reminders.FirstReminderHours - 2);
                    var max = now.AddHours(reminders.FirstReminderHours + 2);
                    var first = await bookingRepo.FindAsync(
                        b => b.Status == BookingStatus.Confirmed &&
                             b.ScheduledAt >= min && b.ScheduledAt <= max, ct);
                    pending.AddRange(first.Select(b => (b, true)));
                }

                if (reminders.SecondReminderHours > 0)
                {
                    var min = now.AddHours(reminders.SecondReminderHours - 1);
                    var max = now.AddHours(reminders.SecondReminderHours + 1);
                    var second = await bookingRepo.FindAsync(
                        b => b.Status == BookingStatus.Confirmed &&
                             b.ScheduledAt >= min && b.ScheduledAt <= max, ct);
                    pending.AddRange(second.Select(b => (b, false)));
                }

                foreach (var (booking, isFirst) in pending)
                {
                    // Locação tem fluxo de lembrete próprio (ver abaixo) — pula agendamento.
                    if (booking.Kind == BookingKind.Rental) continue;

                    var resource    = booking.ResourceId is { } rid
                        ? await resourceRepo.GetByIdAsync(rid, ct)
                        : null;
                    var serviceName = booking.Services.FirstOrDefault()?.ServiceName
                                      ?? booking.ServiceId?.ToString() ?? "Reserva";

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
                        IsOneDayBefore: isFirst);

                    await bus.Publish(msg, ct);

                    logger?.LogInformation(
                        "Lembrete {Type} publicado para booking {Id} (tenant {Slug})",
                        isFirst ? "1º" : "2º", booking.Id, tenant.Slug);
                }
            }

            // ── Locação: lembrete de devolução (D-1) e aviso de atraso ─────────
            // Janelas de 1h (o job roda de hora em hora) para não duplicar avisos.
            var returnMin  = now.AddHours(23);
            var returnMax  = now.AddHours(24);
            var overdueMin = now.AddHours(-24);
            var overdueMax = now.AddHours(-23);

            var returnReminders = await bookingRepo.FindAsync(
                b => b.Kind == BookingKind.Rental
                  && b.RentalStatus == RentalLifecycle.PickedUp
                  && b.EndsAt >= returnMin && b.EndsAt <= returnMax, ct);

            foreach (var booking in returnReminders)
            {
                var itemName = booking.Services.FirstOrDefault()?.ServiceName ?? "item";
                await bus.Publish(new RentalReturnReminderMessage(
                    booking.Id, booking.CustomerName, booking.CustomerEmail, booking.CustomerPhone,
                    itemName, booking.EndsAt, tenant.Slug, tenant.Name), ct);

                logger?.LogInformation(
                    "Lembrete de devolução publicado para locação {Id} (tenant {Slug})",
                    booking.Id, tenant.Slug);
            }

            var overdueRentals = await bookingRepo.FindAsync(
                b => b.Kind == BookingKind.Rental
                  && b.RentalStatus == RentalLifecycle.PickedUp
                  && b.EndsAt >= overdueMin && b.EndsAt <= overdueMax, ct);

            foreach (var booking in overdueRentals)
            {
                var itemName    = booking.Services.FirstOrDefault()?.ServiceName ?? "item";
                var daysOverdue = Math.Max(1, (int)Math.Ceiling((now - booking.EndsAt).TotalDays));
                await bus.Publish(new RentalOverdueMessage(
                    booking.Id, booking.CustomerName, booking.CustomerEmail, booking.CustomerPhone,
                    itemName, booking.EndsAt, daysOverdue, tenant.Slug, tenant.Name), ct);

                logger?.LogInformation(
                    "Aviso de atraso publicado para locação {Id} (tenant {Slug})",
                    booking.Id, tenant.Slug);
            }
        }
    }
}
