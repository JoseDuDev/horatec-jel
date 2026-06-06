using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Notifications;

public sealed class NotificationTemplate : BaseEntity
{
    private NotificationTemplate() { }

    public NotificationEventType EventType       { get; private set; }
    public NotificationChannel   Channel         { get; private set; }
    public string?               SubjectTemplate { get; private set; }
    public string                BodyTemplate    { get; private set; } = default!;
    public bool                  IsActive        { get; private set; } = true;

    public static NotificationTemplate Create(
        NotificationEventType eventType,
        NotificationChannel   channel,
        string                bodyTemplate,
        string?               subjectTemplate = null) =>
        new()
        {
            EventType       = eventType,
            Channel         = channel,
            BodyTemplate    = bodyTemplate,
            SubjectTemplate = subjectTemplate
        };

    public void Update(string? subjectTemplate, string bodyTemplate)
    {
        SubjectTemplate = subjectTemplate;
        BodyTemplate    = bodyTemplate;
        UpdatedAt       = DateTimeOffset.UtcNow;
    }

    public void Deactivate() { IsActive = false; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Activate()   { IsActive = true;  UpdatedAt = DateTimeOffset.UtcNow; }
}
