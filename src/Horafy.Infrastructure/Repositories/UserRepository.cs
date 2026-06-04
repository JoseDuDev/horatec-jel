using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class UserRepository(HorafyDbContext context)
    : BaseRepository<User>(context), IUserRepository
{
    public async Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);

    public async Task<User?> GetByGoogleIdAsync(
        string googleId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.GoogleId == googleId, cancellationToken);

    public async Task<User?> GetByAppleIdAsync(
        string appleId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.AppleId == appleId, cancellationToken);

    public async Task<bool> ExistsByEmailAsync(
        string email,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AnyAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);

    public async Task<IReadOnlyList<User>> GetByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .ToListAsync(cancellationToken);
}
