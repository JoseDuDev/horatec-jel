using Horafy.Domain.Entities.Vouchers;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IVoucherRepository : IRepository<Voucher>
{
    Task<Voucher?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
}
