using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

public sealed class PlanConfigurationRepository(HorafyDbContext context)
    : BaseRepository<PlanConfiguration, HorafyDbContext>(context), IPlanConfigurationRepository
{
    public async Task<PlanConfiguration?> GetByPlanAsync(
        TenantPlan plan, CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking().FirstOrDefaultAsync(p => p.Plan == plan, cancellationToken);

    public async Task<IReadOnlyList<PlanConfiguration>> GetAllConfigsAsync(
        CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking().ToListAsync(cancellationToken);
}
