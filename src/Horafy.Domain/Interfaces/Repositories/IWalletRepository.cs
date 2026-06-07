using Horafy.Domain.Entities.Wallet;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IWalletRepository : IRepository<Wallet>
{
    Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
