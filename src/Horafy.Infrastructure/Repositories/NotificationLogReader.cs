using Horafy.Application.Interfaces;
using Horafy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class NotificationLogReader(HorafyDbContext db) : INotificationLogReader
{
    public async Task<(IReadOnlyList<NotificationLogResult> Items, int TotalCount)> GetPagedAsync(
        string tenantSlug,
        DateTimeOffset? from,
        DateTimeOffset? to,
        bool? success,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = db.NotificationLogs
            .AsNoTracking()
            .Where(n => n.TenantSlug == tenantSlug);

        if (from.HasValue) query = query.Where(n => n.SentAt >= from.Value);
        if (to.HasValue)   query = query.Where(n => n.SentAt <= to.Value);
        if (success.HasValue) query = query.Where(n => n.Success == success.Value);

        query = query.OrderByDescending(n => n.SentAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationLogResult(
                n.Id, n.EventType, n.Channel, n.Recipient, n.Success, n.ErrorMessage, n.SentAt))
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
