using Horafy.Domain.Entities.Rentals;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IRentableItemRepository : IRepository<RentableItem>
{
    Task<IReadOnlyList<RentableItem>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RentableItem>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}
