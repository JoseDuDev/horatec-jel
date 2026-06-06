using Horafy.Domain.Entities.Reviews;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IReviewRepository : IRepository<Review>
{
    Task<Review?> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Review>> GetByResourceAsync(Guid resourceId, CancellationToken cancellationToken = default);
}
