using Horafy.Domain.Entities.Reviews;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class ReviewRepository(TenantDbContext context)
    : BaseRepository<Review, TenantDbContext>(context), IReviewRepository
{
    public async Task<Review?> GetByBookingAsync(
        Guid bookingId, CancellationToken cancellationToken = default) =>
        await DbSet
            .FirstOrDefaultAsync(r => r.BookingId == bookingId, cancellationToken);

    public async Task<IReadOnlyList<Review>> GetByResourceAsync(
        Guid resourceId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(r => r.ResourceId == resourceId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Review> Items, int TotalCount)> GetByResourcePagedAsync(
        Guid resourceId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .AsNoTracking()
            .Where(r => r.ResourceId == resourceId);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<(double AverageStars, int Count)> GetRatingSummaryAsync(
        Guid resourceId, CancellationToken cancellationToken = default)
    {
        var stats = await DbSet
            .AsNoTracking()
            .Where(r => r.ResourceId == resourceId)
            .GroupBy(_ => 1)
            .Select(g => new { Avg = g.Average(r => (double)r.Stars), Count = g.Count() })
            .FirstOrDefaultAsync(cancellationToken);

        return stats is null ? (0d, 0) : (Math.Round(stats.Avg, 2), stats.Count);
    }
}
