using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Entities.Base;
using Horafy.Domain.Entities.Resources;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IAvailabilityRepository
{
    Task<IReadOnlyList<BusinessHours>> GetBusinessHoursAsync(CancellationToken ct = default);
    Task<BusinessHours?> GetBusinessHoursByDayAsync(DayOfWeek day, CancellationToken ct = default);

    Task<IReadOnlyList<AvailabilityRule>> GetRulesByResourceAsync(Guid resourceId, CancellationToken ct = default);
    Task<AvailabilityRule?> GetRuleAsync(Guid resourceId, DayOfWeek day, CancellationToken ct = default);

    Task<AvailabilityException?> GetExceptionAsync(Guid resourceId, DateOnly date, CancellationToken ct = default);

    Task<IReadOnlyList<ResourceService>> GetResourceServicesAsync(Guid resourceId, CancellationToken ct = default);
    Task<IReadOnlyList<ResourceService>> GetServicesByResourcesAsync(IEnumerable<Guid> resourceIds, CancellationToken ct = default);
    Task<bool> ResourceServiceExistsAsync(Guid resourceId, Guid serviceId, CancellationToken ct = default);

    void Add<T>(T entity) where T : BaseEntity;
    void Update<T>(T entity) where T : BaseEntity;
    void Remove<T>(T entity) where T : BaseEntity;
}
