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

        var bookings = await context.Set<Booking>()
            .AsNoTracking()
            .Include(b => b.Services)
            .Where(b => b.Kind == BookingKind.Appointment
                     && b.ScheduledAt >= fromDt
                     && b.ScheduledAt <= toDt)
            .ToListAsync(ct);

        var total     = bookings.Count;
        var confirmed = bookings.Count(b => b.Status == BookingStatus.Confirmed);
        var cancelled = bookings.Count(b => b.Status == BookingStatus.Cancelled);
        var noShow    = bookings.Count(b => b.Status == BookingStatus.NoShow);
        var cancelRate = total > 0 ? Math.Round((decimal)cancelled / total * 100, 1) : 0m;

        var revenue = bookings
            .Where(b => b.Status is not BookingStatus.Cancelled and not BookingStatus.NoShow)
            .Sum(b => b.TotalAmount);

        IReadOnlyList<ServiceStatItem> topServices = bookings
            .Where(b => b.Status != BookingStatus.Cancelled)
            .SelectMany(b => b.Services, (_, s) => new { s.ServiceName, s.Price })
            .GroupBy(x => x.ServiceName)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new ServiceStatItem(g.Key, g.Count(), g.Sum(x => x.Price)))
            .ToList();

        IReadOnlyList<ResourceStatItem> topResources = bookings
            .Where(b => b.Status != BookingStatus.Cancelled && !string.IsNullOrEmpty(b.ResourceName))
            .GroupBy(b => b.ResourceName)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new ResourceStatItem(g.Key, g.Count()))
            .ToList();

        IReadOnlyList<DailyBookingItem> byDay = bookings
            .GroupBy(b => DateOnly.FromDateTime(b.ScheduledAt.UtcDateTime))
            .OrderBy(g => g.Key)
            .Select(g => new DailyBookingItem(g.Key, g.Count()))
            .ToList();

        return new DashboardStats(
            from, to, total, confirmed, cancelled, noShow, cancelRate,
            revenue, topServices, topResources, byDay);
    }
}
