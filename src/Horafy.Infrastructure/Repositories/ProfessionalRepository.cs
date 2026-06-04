using Horafy.Domain.Entities.Professionals;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class ProfessionalRepository(TenantDbContext context)
    : BaseRepository<Professional, TenantDbContext>(context), IProfessionalRepository
{
    public async Task<IReadOnlyList<Professional>> GetActiveAsync(
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
}
