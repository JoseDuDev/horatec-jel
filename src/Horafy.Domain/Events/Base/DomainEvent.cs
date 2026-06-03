namespace Horafy.Domain.Events.Base;

/// <summary>
/// Classe base abstrata para Domain Events com valores padrão.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    protected DomainEvent()
    {
        EventId = Guid.NewGuid();
        OccurredAt = DateTimeOffset.UtcNow;
    }

    public Guid EventId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}
