using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class CustomerListReader(TenantDbContext context) : ICustomerListReader
{
    public async Task<IReadOnlyList<CustomerExportRecord>> GetCustomersAsync(CancellationToken ct = default)
    {
        // (1) Contagem e último agendamento por cliente (todos os bookings).
        var stats = await context.Set<Booking>()
            .AsNoTracking()
            .GroupBy(b => b.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                Count      = g.Count(),
                Last       = g.Max(b => b.ScheduledAt),
            })
            .ToListAsync(ct);

        // (2) Total gasto = soma dos preços dos serviços de bookings NÃO cancelados.
        var spent = await context.Set<Booking>()
            .AsNoTracking()
            .Where(b => b.Status != BookingStatus.Cancelled)
            .SelectMany(b => b.Services, (b, s) => new { b.CustomerId, s.Price })
            .GroupBy(x => x.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(x => x.Price) })
            .ToListAsync(ct);
        var spentByCustomer = spent.ToDictionary(x => x.CustomerId, x => x.Total);

        // (3) Contato do agendamento mais recente por cliente (argmax via subconsulta correlata).
        var latest = await context.Set<Booking>()
            .AsNoTracking()
            .Where(b => b.ScheduledAt == context.Set<Booking>()
                .Where(x => x.CustomerId == b.CustomerId)
                .Max(x => x.ScheduledAt))
            .Select(b => new { b.CustomerId, b.CustomerName, b.CustomerEmail, b.CustomerPhone, b.ScheduledAt })
            .ToListAsync(ct);
        // Empates no mesmo ScheduledAt: mantém o primeiro determinístico por cliente.
        var contactByCustomer = latest
            .GroupBy(b => b.CustomerId)
            .ToDictionary(g => g.Key, g => g.First());

        return stats
            .Select(s =>
            {
                contactByCustomer.TryGetValue(s.CustomerId, out var c);
                spentByCustomer.TryGetValue(s.CustomerId, out var total);
                return new CustomerExportRecord(
                    CustomerId:    s.CustomerId,
                    Name:          c?.CustomerName  ?? string.Empty,
                    Email:         c?.CustomerEmail ?? string.Empty,
                    Phone:         c?.CustomerPhone,
                    BookingCount:  s.Count,
                    LastBookingAt: s.Last,
                    TotalSpent:    total);
            })
            .OrderBy(c => c.Name)
            .ToList();
    }
}
