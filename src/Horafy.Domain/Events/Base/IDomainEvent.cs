using MediatR;

namespace Horafy.Domain.Events.Base;

/// <summary>
/// Marker interface para Domain Events.
/// Implementa INotification do MediatR para que os handlers
/// possam ser registrados via DI e despachados via IPublisher.
/// </summary>
public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}
