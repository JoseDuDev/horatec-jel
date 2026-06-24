using Horafy.Domain.Entities.Tenants;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IPlanConfigurationRepository : IRepository<PlanConfiguration>
{
    Task<PlanConfiguration?> GetByPlanAsync(TenantPlan plan, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlanConfiguration>> GetAllConfigsAsync(CancellationToken cancellationToken = default);
}
