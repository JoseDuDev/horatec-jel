namespace Horafy.Application.Interfaces;

public interface IRevenueReportReader
{
    Task<RevenueReport> GetReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}

public sealed record RevenueReport(
    DateOnly                          From,
    DateOnly                          To,
    decimal                           TotalRevenue,
    int                               ApprovedPaymentsCount,
    IReadOnlyList<ServiceRevenueItem> ByService,
    IReadOnlyList<DailyRevenueItem>   ByDay);

public sealed record ServiceRevenueItem(string ServiceName, int BookingCount, decimal Revenue);
public sealed record DailyRevenueItem(DateOnly Date, decimal Revenue, int Count);
