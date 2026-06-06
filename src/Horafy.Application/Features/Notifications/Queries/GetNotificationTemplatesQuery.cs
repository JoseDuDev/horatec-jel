using Horafy.Domain.Entities.Notifications;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Notifications.Queries;

public sealed record GetNotificationTemplatesQuery
    : IRequest<Result<IReadOnlyList<NotificationTemplateResult>>>;

public sealed record NotificationTemplateResult(
    Guid                  Id,
    NotificationEventType EventType,
    NotificationChannel   Channel,
    string?               SubjectTemplate,
    string                BodyTemplate,
    bool                  IsActive);

internal sealed class GetNotificationTemplatesQueryHandler(
    INotificationTemplateRepository repository)
    : IRequestHandler<GetNotificationTemplatesQuery, Result<IReadOnlyList<NotificationTemplateResult>>>
{
    public async Task<Result<IReadOnlyList<NotificationTemplateResult>>> Handle(
        GetNotificationTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await repository.GetAllActiveAsync(cancellationToken);
        var result = templates
            .Select(t => new NotificationTemplateResult(
                t.Id, t.EventType, t.Channel,
                t.SubjectTemplate, t.BodyTemplate, t.IsActive))
            .ToList();
        return Result.Success<IReadOnlyList<NotificationTemplateResult>>(result);
    }
}
