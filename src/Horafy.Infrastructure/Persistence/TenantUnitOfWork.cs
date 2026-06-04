using Horafy.Application.Interfaces;

namespace Horafy.Infrastructure.Persistence;

internal sealed class TenantUnitOfWork(TenantDbContext context) : ITenantUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
