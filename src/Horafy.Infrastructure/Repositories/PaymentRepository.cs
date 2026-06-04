using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class PaymentRepository(TenantDbContext context)
    : BaseRepository<Payment, TenantDbContext>(context), IPaymentRepository
{
    public async Task<Payment?> GetByPreferenceIdAsync(string preferenceId, CancellationToken ct = default) =>
        await DbSet.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PreferenceId == preferenceId, ct);

    public async Task<Payment?> GetByMpPaymentIdAsync(string mpPaymentId, CancellationToken ct = default) =>
        await DbSet.AsNoTracking()
            .FirstOrDefaultAsync(p => p.MpPaymentId == mpPaymentId, ct);

    public async Task<Payment?> GetByBookingIdAsync(Guid bookingId, CancellationToken ct = default) =>
        await DbSet.AsNoTracking()
            .FirstOrDefaultAsync(p => p.BookingId == bookingId, ct);

    public async Task<IReadOnlyList<Payment>> GetByPeriodAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) =>
        await DbSet.AsNoTracking()
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
}
