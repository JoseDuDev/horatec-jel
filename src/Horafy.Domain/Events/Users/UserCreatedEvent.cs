using Horafy.Domain.Entities.Users;
using Horafy.Domain.Events.Base;

namespace Horafy.Domain.Events.Users;

public sealed record UserCreatedEvent(
    Guid UserId,
    string Email,
    UserRole Role) : DomainEvent;
