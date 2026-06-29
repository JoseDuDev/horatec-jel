using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class DashboardReader(TenantDbContext context) : IDashboardReader
{
    public async Task<DashboardStats> GetStatsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var fromDt = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toDt   = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        // Predicado-base compartilhado: agendamentos no intervalo. As agregações abaixo
        // são empurradas para o SQL (counts, sums, top-N) em vez de materializar todas as
        // reservas + serviços em memória.
        var appointments = context.Set<Booking>()
            .AsNoTracking()
            .Where(b => b.Kind == BookingKind.Appointment
                     && b.ScheduledAt >= fromDt
                     && b.ScheduledAt <= toDt);

        // Contagens por status numa única query agrupada → mapeadas em memória.
        var statusCounts = await appointments
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var total     = statusCounts.Sum(x => x.Count);
        var confirmed = statusCounts.Where(x => x.Status == BookingStatus.Confirmed).Sum(x => x.Count);
        var cancelled = statusCounts.Where(x => x.Status == BookingStatus.Cancelled).Sum(x => x.Count);
        var noShow    = statusCounts.Where(x => x.Status == BookingStatus.NoShow).Sum(x => x.Count);
        var cancelRate = total > 0 ? Math.Round((decimal)cancelled / total * 100, 1) : 0m;

        // Receita = soma dos preços (snapshot) dos serviços, excluindo Cancelled E NoShow.
        var revenue = await appointments
            .Where(b => b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.NoShow)
            .SelectMany(b => b.Services, (_, s) => s.Price)
            .SumAsync(ct);

        // Top 5 serviços por número de reservas (exclui apenas Cancelled).
        var topServicesRaw = await appointments
            .Where(b => b.Status != BookingStatus.Cancelled)
            .SelectMany(b => b.Services, (_, s) => new { s.ServiceName, s.Price })
            .GroupBy(x => x.ServiceName)
            .Select(g => new { ServiceName = g.Key, Count = g.Count(), Revenue = g.Sum(x => x.Price) })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync(ct);

        IReadOnlyList<ServiceStatItem> topServices = topServicesRaw
            .Select(x => new ServiceStatItem(x.ServiceName, x.Count, x.Revenue))
            .ToList();

        // Top 5 recursos por número de reservas (exclui Cancelled e nomes vazios).
        var topResourcesRaw = await appointments
            .Where(b => b.Status != BookingStatus.Cancelled && !string.IsNullOrEmpty(b.ResourceName))
            .GroupBy(b => b.ResourceName)
            .Select(g => new { ResourceName = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync(ct);

        IReadOnlyList<ResourceStatItem> topResources = topResourcesRaw
            .Select(x => new ResourceStatItem(x.ResourceName, x.Count))
            .ToList();

        // Reservas por dia (data UTC do agendamento), ordenadas crescentemente.
        var byDayRaw = await appointments
            .GroupBy(b => DateOnly.FromDateTime(b.ScheduledAt.UtcDateTime))
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync(ct);

        IReadOnlyList<DailyBookingItem> byDay = byDayRaw
            .Select(x => new DailyBookingItem(x.Date, x.Count))
            .ToList();

        return new DashboardStats(
            from, to, total, confirmed, cancelled, noShow, cancelRate,
            revenue, topServices, topResources, byDay);
    }
}
