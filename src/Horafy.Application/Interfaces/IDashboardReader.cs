namespace Horafy.Application.Interfaces;

public interface IDashboardReader
{
    Task<DashboardStats> GetStatsAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}

public sealed record DashboardStats(
    DateOnly                        From,
    DateOnly                        To,
    int                             TotalBookings,
    int                             ConfirmedBookings,
    int                             CancelledBookings,
    int                             NoShowBookings,
    decimal                         CancellationRate,
    decimal                         TotalRevenue,
    IReadOnlyList<ServiceStatItem>  TopServices,
    IReadOnlyList<ResourceStatItem> TopResources,
    IReadOnlyList<DailyBookingItem> BookingsByDay);

public sealed record ServiceStatItem(string ServiceName, int BookingCount, decimal Revenue);
public sealed record ResourceStatItem(string ResourceName, int BookingCount);
public sealed record DailyBookingItem(DateOnly Date, int Count);
