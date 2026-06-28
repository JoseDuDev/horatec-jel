using Horafy.Domain.Entities.Reviews;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IReviewRepository : IRepository<Review>
{
    Task<Review?> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Review>> GetByResourceAsync(Guid resourceId, CancellationToken cancellationToken = default);

    /// <summary>Página de avaliações de um recurso, mais recentes primeiro.</summary>
    Task<(IReadOnlyList<Review> Items, int TotalCount)> GetByResourcePagedAsync(
        Guid resourceId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Média de estrelas e total de avaliações de um recurso.</summary>
    Task<(double AverageStars, int Count)> GetRatingSummaryAsync(
        Guid resourceId, CancellationToken cancellationToken = default);
}
