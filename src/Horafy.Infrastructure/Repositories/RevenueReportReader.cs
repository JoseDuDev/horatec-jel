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

        // Predicado-base: pagamentos aprovados no intervalo. Total/contagem/byDay são
        // agregados diretamente no SQL em vez de carregar todos os pagamentos.
        var approved = context.Set<Payment>()
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Approved
                     && p.CreatedAt >= fromDt
                     && p.CreatedAt <= toDt);

        var totalRevenue = await approved.SumAsync(p => p.Amount, ct);
        var paymentCount = await approved.CountAsync(ct);

        var byDayRaw = await approved
            .GroupBy(p => DateOnly.FromDateTime(p.CreatedAt.UtcDateTime))
            .Select(g => new { Date = g.Key, Revenue = g.Sum(p => p.Amount), Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync(ct);

        IReadOnlyList<DailyRevenueItem> byDay = byDayRaw
            .Select(x => new DailyRevenueItem(x.Date, x.Revenue, x.Count))
            .ToList();

        // Breakdown por serviço: junta os serviços das reservas distintas referenciadas
        // pelos pagamentos aprovados (subquery DISTINCT evita contar serviços duas vezes
        // quando uma reserva tem mais de um pagamento aprovado) e agrega no SQL.
        var bookingIds = approved.Select(p => p.BookingId).Distinct();

        var byServiceRaw = await context.Set<Booking>()
            .AsNoTracking()
            .Where(b => bookingIds.Contains(b.Id))
            .SelectMany(b => b.Services, (_, s) => new { s.ServiceName, s.Price })
            .GroupBy(x => x.ServiceName)
            .Select(g => new { ServiceName = g.Key, Count = g.Count(), Revenue = g.Sum(x => x.Price) })
            .OrderByDescending(x => x.Revenue)
            .ToListAsync(ct);

        IReadOnlyList<ServiceRevenueItem> byService = byServiceRaw
            .Select(x => new ServiceRevenueItem(x.ServiceName, x.Count, x.Revenue))
            .ToList();

        return new RevenueReport(from, to, totalRevenue, paymentCount, byService, byDay);
    }
}
