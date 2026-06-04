using Horafy.Domain.Entities.Bookings;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IWaitlistRepository : IRepository<WaitlistEntry>
{
    Task<IReadOnlyList<WaitlistEntry>> GetByServiceResourceDateAsync(
        Guid serviceId,
        Guid resourceId,
        DateOnly preferredDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WaitlistEntry>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsActiveAsync(
        Guid serviceId,
        Guid resourceId,
        Guid customerId,
        DateOnly preferredDate,
        CancellationToken cancellationToken = default);
}
