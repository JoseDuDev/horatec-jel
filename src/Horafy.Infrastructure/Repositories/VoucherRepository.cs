using Horafy.Domain.Entities.Vouchers;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class VoucherRepository(TenantDbContext context)
    : BaseRepository<Voucher, TenantDbContext>(context), IVoucherRepository
{
    public async Task<Voucher?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        await DbSet.FirstOrDefaultAsync(v => v.Code == code, ct);

    public async Task<bool> CodeExistsAsync(string code, CancellationToken ct = default) =>
        await DbSet.AnyAsync(v => v.Code == code, ct);
}
