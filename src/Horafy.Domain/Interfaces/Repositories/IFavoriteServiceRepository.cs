using Horafy.Domain.Entities.Favorites;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IFavoriteServiceRepository : IRepository<FavoriteService>
{
    Task<FavoriteService?> GetAsync(
        Guid customerId, Guid serviceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FavoriteService>> GetByCustomerAsync(
        Guid customerId, CancellationToken cancellationToken = default);
}
