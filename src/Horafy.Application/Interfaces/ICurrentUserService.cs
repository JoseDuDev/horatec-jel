using Horafy.Domain.Entities.Users;

namespace Horafy.Application.Interfaces;

/// <summary>
/// Fornece o contexto do usuário autenticado na requisição atual.
/// Implementado na Infrastructure via claims do JWT.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    UserRole? Role { get; }
    Guid? TenantId { get; }
    bool IsAuthenticated { get; }
    bool HasPermission(UserPermission permission);
}
