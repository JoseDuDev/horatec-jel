using Horafy.Domain.Entities.Notifications;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class NotificationTemplateRepository(TenantDbContext context)
    : BaseRepository<NotificationTemplate, TenantDbContext>(context),
      INotificationTemplateRepository
{
    public async Task<NotificationTemplate?> GetActiveAsync(
        NotificationEventType eventType,
        NotificationChannel channel,
        CancellationToken ct = default) =>
        await DbSet.AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.EventType == eventType &&
                t.Channel   == channel   &&
                t.IsActive, ct);

    public async Task<IReadOnlyList<NotificationTemplate>> GetAllActiveAsync(
        CancellationToken ct = default) =>
        await DbSet.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.EventType)
            .ThenBy(t => t.Channel)
            .ToListAsync(ct);
}
