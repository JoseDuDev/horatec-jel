namespace Horafy.Domain.Entities.Users;

/// <summary>
/// Define o papel do usuário na plataforma.
/// PlatformAdmin → acesso total à plataforma.
/// TenantOwner / TenantAdmin / TenantStaff → escopos dentro de um tenant.
/// Customer → cliente final que agenda/reserva.
/// </summary>
public enum UserRole
{
    PlatformAdmin = 0,
    TenantOwner   = 1,
    TenantAdmin   = 2,
    TenantStaff   = 3,
    Customer      = 4
}
