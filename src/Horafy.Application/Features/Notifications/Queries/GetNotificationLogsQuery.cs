using Horafy.Application.Interfaces;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Notifications.Queries;

public sealed record GetNotificationLogsQuery(
    DateTimeOffset? From      = null,
    DateTimeOffset? To        = null,
    bool?           Success   = null,
    int             PageNumber = 1,
    int             PageSize   = 50) : IRequest<Result<PagedResult<NotificationLogResult>>>;

internal sealed class GetNotificationLogsQueryHandler(
    INotificationLogReader  reader,
    ICurrentTenantService   tenantService)
    : IRequestHandler<GetNotificationLogsQuery, Result<PagedResult<NotificationLogResult>>>
{
    public async Task<Result<PagedResult<NotificationLogResult>>> Handle(
        GetNotificationLogsQuery request, CancellationToken cancellationToken)
    {
        if (!tenantService.HasTenant || tenantService.Slug is null)
            return Result.Failure<PagedResult<NotificationLogResult>>(Error.Unauthorized);

        var (items, total) = await reader.GetPagedAsync(
            tenantService.Slug,
            request.From,
            request.To,
            request.Success,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return Result.Success(
            PagedResult<NotificationLogResult>.Create(items, total, request.PageNumber, request.PageSize));
    }
}
