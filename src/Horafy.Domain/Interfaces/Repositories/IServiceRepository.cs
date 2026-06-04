using Horafy.Domain.Entities.Services;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IServiceRepository : IRepository<Service>
{
    Task<IReadOnlyList<Service>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);
}
