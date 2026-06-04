using Horafy.Domain.Entities.Resources;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IResourceRepository : IRepository<Resource>
{
    Task<IReadOnlyList<Resource>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Resource>> GetByTypeAsync(ResourceType type, CancellationToken cancellationToken = default);
}
