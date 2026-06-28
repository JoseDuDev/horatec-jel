using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class CustomerListReader(TenantDbContext context) : ICustomerListReader
{
    public async Task<IReadOnlyList<CustomerExportRecord>> GetCustomersAsync(CancellationToken ct = default)
    {
        // Carrega as reservas com os itens (TotalAmount é derivado de Services, não é coluna)
        // e agrega por cliente em memória — mesmo padrão do RevenueReportReader.
        var bookings = await context.Set<Booking>()
            .AsNoTracking()
            .Include(b => b.Services)
            .ToListAsync(ct);

        return bookings
            .GroupBy(b => b.CustomerId)
            .Select(g =>
            {
                var latest     = g.OrderByDescending(b => b.ScheduledAt).First();
                var totalSpent = g
                    .Where(b => b.Status != BookingStatus.Cancelled)
                    .Sum(b => b.TotalAmount);

                return new CustomerExportRecord(
                    CustomerId:    g.Key,
                    Name:          latest.CustomerName,
                    Email:         latest.CustomerEmail,
                    Phone:         latest.CustomerPhone,
                    BookingCount:  g.Count(),
                    LastBookingAt: g.Max(b => b.ScheduledAt),
                    TotalSpent:    totalSpent);
            })
            .OrderBy(c => c.Name)
            .ToList();
    }
}
