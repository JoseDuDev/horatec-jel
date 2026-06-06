using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Notifications;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Notifications.Commands;

public sealed record UpsertNotificationTemplateCommand(
    NotificationEventType EventType,
    NotificationChannel   Channel,
    string                BodyTemplate,
    string?               SubjectTemplate) : IRequest<Result>;

internal sealed class UpsertNotificationTemplateCommandHandler(
    INotificationTemplateRepository repository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<UpsertNotificationTemplateCommand, Result>
{
    public async Task<Result> Handle(
        UpsertNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BodyTemplate))
            return Result.Failure(new Error(
                "NotificationTemplate.EmptyBody", "O corpo do template não pode ser vazio.",
                ErrorType.Validation));

        var existing = await repository.GetActiveAsync(
            request.EventType, request.Channel, cancellationToken);

        if (existing is not null)
        {
            existing.Update(request.SubjectTemplate, request.BodyTemplate);
            repository.Update(existing);
        }
        else
        {
            var template = NotificationTemplate.Create(
                request.EventType, request.Channel,
                request.BodyTemplate, request.SubjectTemplate);
            repository.Add(template);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
