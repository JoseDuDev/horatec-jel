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
}
