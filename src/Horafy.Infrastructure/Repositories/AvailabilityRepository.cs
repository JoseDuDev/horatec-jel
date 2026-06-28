using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Entities.Base;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class AvailabilityRepository(TenantDbContext context) : IAvailabilityRepository
{
    public async Task<IReadOnlyList<BusinessHours>> GetBusinessHoursAsync(
        CancellationToken ct = default) =>
        await context.Set<BusinessHours>().AsNoTracking().ToListAsync(ct);

    public async Task<BusinessHours?> GetBusinessHoursByDayAsync(
        DayOfWeek day, CancellationToken ct = default) =>
        await context.Set<BusinessHours>()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.DayOfWeek == day, ct);

    public async Task<IReadOnlyList<AvailabilityRule>> GetRulesByResourceAsync(
        Guid resourceId, CancellationToken ct = default) =>
        await context.Set<AvailabilityRule>()
            .AsNoTracking()
            .Where(r => r.ResourceId == resourceId)
            .ToListAsync(ct);

    public async Task<AvailabilityRule?> GetRuleAsync(
        Guid resourceId, DayOfWeek day, CancellationToken ct = default) =>
        await context.Set<AvailabilityRule>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ResourceId == resourceId && r.DayOfWeek == day, ct);

    public async Task<IReadOnlyList<Holiday>> GetHolidaysAsync(
        int? year = null, CancellationToken ct = default) =>
        await context.Set<Holiday>()
            .AsNoTracking()
            .Where(h => year == null || h.Date.Year == year)
            .OrderBy(h => h.Date)
            .ToListAsync(ct);

    public async Task<Holiday?> GetHolidayAsync(Guid id, CancellationToken ct = default) =>
        await context.Set<Holiday>()
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == id, ct);

    public async Task<AvailabilityException?> GetExceptionAsync(
        Guid resourceId, DateOnly date, CancellationToken ct = default) =>
        await context.Set<AvailabilityException>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ResourceId == resourceId && e.Date == date, ct);

    public async Task<IReadOnlyList<AvailabilityException>> GetExceptionsByResourceAsync(
        Guid resourceId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        await context.Set<AvailabilityException>()
            .AsNoTracking()
            .Where(e => e.ResourceId == resourceId && e.Date >= from && e.Date <= to)
            .OrderBy(e => e.Date)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ResourceService>> GetResourceServicesAsync(
        Guid resourceId, CancellationToken ct = default) =>
        await context.Set<ResourceService>()
            .AsNoTracking()
            .Where(rs => rs.ResourceId == resourceId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ResourceService>> GetServicesByResourcesAsync(
        IEnumerable<Guid> resourceIds, CancellationToken ct = default) =>
        await context.Set<ResourceService>()
            .AsNoTracking()
            .Where(rs => resourceIds.Contains(rs.ResourceId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ResourceService>> GetResourcesByServiceAsync(
        Guid serviceId, CancellationToken ct = default) =>
        await context.Set<ResourceService>()
            .AsNoTracking()
            .Where(rs => rs.ServiceId == serviceId)
            .ToListAsync(ct);

    public async Task<bool> ResourceServiceExistsAsync(
        Guid resourceId, Guid serviceId, CancellationToken ct = default) =>
        await context.Set<ResourceService>()
            .AnyAsync(rs => rs.ResourceId == resourceId && rs.ServiceId == serviceId, ct);

    public void Add<T>(T entity) where T : BaseEntity    => context.Set<T>().Add(entity);
    public void Update<T>(T entity) where T : BaseEntity => context.Set<T>().Update(entity);
    public void Remove<T>(T entity) where T : BaseEntity => context.Set<T>().Remove(entity);
}
