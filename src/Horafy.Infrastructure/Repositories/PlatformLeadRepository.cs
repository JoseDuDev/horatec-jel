using Horafy.Domain.Entities.Leads;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

public sealed class PlatformLeadRepository(HorafyDbContext context)
    : BaseRepository<PlatformLead, HorafyDbContext>(context), IPlatformLeadRepository
{
    public async Task<bool> RecentPhoneExistsAsync(
        string phone,
        DateTimeOffset since,
        CancellationToken cancellationToken = default) =>
        await DbSet.AnyAsync(
            l => l.Phone == phone && l.CreatedAt >= since,
            cancellationToken);
}
