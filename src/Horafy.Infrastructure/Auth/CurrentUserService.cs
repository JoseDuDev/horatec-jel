using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Horafy.Infrastructure.Auth;

/// <summary>
/// Implementação de ICurrentUserService que lê as claims do JWT
/// injetado pelo middleware de autenticação do ASP.NET Core.
/// </summary>
internal sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated is true;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    public string? Email =>
        User?.FindFirstValue(ClaimTypes.Email);

    public UserRole? Role
    {
        get
        {
            var role = User?.FindFirstValue(ClaimTypes.Role);
            return role is not null && Enum.TryParse<UserRole>(role, out var r) ? r : null;
        }
    }

    public Guid? TenantId =>
        Guid.TryParse(User?.FindFirstValue("tenant_id"), out var id) ? id : null;

    public bool HasPermission(UserPermission permission)
    {
        var permissionsRaw = User?.FindFirstValue("permissions");
        if (string.IsNullOrEmpty(permissionsRaw)) return false;

        return permissionsRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Any(p => p == permission.ToString());
    }
}
