using Horafy.Domain.Entities.Payments;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IPaymentRepository : IRepository<Payment>
{
    Task<Payment?> GetByPreferenceIdAsync(string preferenceId, CancellationToken ct = default);
    Task<Payment?> GetByMpPaymentIdAsync(string mpPaymentId, CancellationToken ct = default);
    Task<Payment?> GetByBookingIdAsync(Guid bookingId, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> GetByPeriodAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
