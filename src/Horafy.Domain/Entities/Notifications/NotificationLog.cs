namespace Horafy.Domain.Entities.Notifications;

public sealed class NotificationLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string TenantSlug { get; private set; } = string.Empty;
    public NotificationEventType EventType { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset SentAt { get; private set; } = DateTimeOffset.UtcNow;

    private NotificationLog() { }

    public static NotificationLog Record(
        string tenantSlug,
        NotificationEventType eventType,
        NotificationChannel channel,
        string recipient,
        bool success,
        string? errorMessage = null) => new()
    {
        TenantSlug   = tenantSlug,
        EventType    = eventType,
        Channel      = channel,
        Recipient    = recipient,
        Success      = success,
        ErrorMessage = errorMessage
    };
}
