using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Payments;
using Horafy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class RevenueReportReader(TenantDbContext context) : IRevenueReportReader
{
    public async Task<RevenueReport> GetReportAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var fromDt = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toDt   = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var payments = await context.Set<Payment>()
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Approved
                     && p.CreatedAt >= fromDt
                     && p.CreatedAt <= toDt)
            .ToListAsync(ct);

        var totalRevenue  = payments.Sum(p => p.Amount);
        var paymentCount  = payments.Count;

        IReadOnlyList<DailyRevenueItem> byDay = payments
            .GroupBy(p => DateOnly.FromDateTime(p.CreatedAt.UtcDateTime))
            .OrderBy(g => g.Key)
            .Select(g => new DailyRevenueItem(g.Key, g.Sum(p => p.Amount), g.Count()))
            .ToList();

        // Service breakdown: join payments → bookings → booking services
        var bookingIds = payments.Select(p => p.BookingId).Distinct().ToList();
        IReadOnlyList<ServiceRevenueItem> byService = [];

        if (bookingIds.Count > 0)
        {
            var bookings = await context.Set<Booking>()
                .AsNoTracking()
                .Include(b => b.Services)
                .Where(b => bookingIds.Contains(b.Id))
                .ToListAsync(ct);

            byService = bookings
                .SelectMany(b => b.Services, (_, s) => new { s.ServiceName, s.Price })
                .GroupBy(x => x.ServiceName)
                .OrderByDescending(g => g.Sum(x => x.Price))
                .Select(g => new ServiceRevenueItem(g.Key, g.Count(), g.Sum(x => x.Price)))
                .ToList();
        }

        return new RevenueReport(from, to, totalRevenue, paymentCount, byService, byDay);
    }
}
