using Horafy.Domain.Entities.Professionals;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IProfessionalRepository : IRepository<Professional>
{
    Task<IReadOnlyList<Professional>> GetActiveAsync(CancellationToken cancellationToken = default);
}
