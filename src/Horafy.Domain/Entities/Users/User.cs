using Horafy.Domain.Entities.Base;
using Horafy.Domain.Events.Users;

namespace Horafy.Domain.Entities.Users;

/// <summary>
/// Agregado raiz que representa um usuário da plataforma.
///
/// Um usuário pode autenticar-se via Google, Apple ou e-mail/senha —
/// e pode ter múltiplos provedores vinculados à mesma conta.
///
/// Permissões são armazenadas como string delimitada por vírgula para
/// simplificar o mapeamento EF Core sem tabelas auxiliares na Sprint 2.
/// </summary>
public sealed class User : BaseEntity
{
    private User() { } // EF Core

    // ── Identidade ────────────────────────────────────────────────────
    public string Email { get; private set; } = default!;
    public string? Name { get; private set; }
    public string? AvatarUrl { get; private set; }
    public string? Phone { get; private set; }

    // ── Provedores OAuth ─────────────────────────────────────────────
    public string? GoogleId { get; private set; }
    public string? AppleId { get; private set; }

    // ── Autenticação local ────────────────────────────────────────────
    public string? PasswordHash { get; private set; }

    // ── Tenant / Role ─────────────────────────────────────────────────
    /// <summary>Null para PlatformAdmin; obrigatório para os demais roles.</summary>
    public Guid? TenantId { get; private set; }
    public UserRole Role { get; private set; }

    // ── Permissões (armazenadas como CSV; lidas como typed collection) ─
    private string _permissionsRaw = string.Empty;

    public IReadOnlyCollection<UserPermission> Permissions =>
        string.IsNullOrEmpty(_permissionsRaw)
            ? Array.Empty<UserPermission>()
            : _permissionsRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(Enum.Parse<UserPermission>)
                .ToArray();

    // ── Estado ────────────────────────────────────────────────────────
    public bool IsEmailVerified { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    // ── Factories ────────────────────────────────────────────────────
    public static User CreateWithGoogle(
        string email,
        string googleId,
        string? name,
        string? avatarUrl,
        Guid? tenantId,
        UserRole role)
    {
        var user = new User
        {
            Email           = email.ToLowerInvariant().Trim(),
            GoogleId        = googleId,
            Name            = name,
            AvatarUrl       = avatarUrl,
            TenantId        = tenantId,
            Role            = role,
            IsEmailVerified = true // Google já valida o e-mail
        };

        DefaultPermissions(user);
        user.RaiseDomainEvent(new UserCreatedEvent(user.Id, user.Email, user.Role));
        return user;
    }

    public static User CreateWithApple(
        string email,
        string appleId,
        string? name,
        Guid? tenantId,
        UserRole role)
    {
        var user = new User
        {
            Email           = email.ToLowerInvariant().Trim(),
            AppleId         = appleId,
            Name            = name,
            TenantId        = tenantId,
            Role            = role,
            IsEmailVerified = true // Apple já valida o e-mail
        };

        DefaultPermissions(user);
        user.RaiseDomainEvent(new UserCreatedEvent(user.Id, user.Email, user.Role));
        return user;
    }

    public static User CreateWithEmail(
        string email,
        string passwordHash,
        string? name,
        Guid? tenantId,
        UserRole role)
    {
        var user = new User
        {
            Email        = email.ToLowerInvariant().Trim(),
            PasswordHash = passwordHash,
            Name         = name,
            TenantId     = tenantId,
            Role         = role
        };

        DefaultPermissions(user);
        user.RaiseDomainEvent(new UserCreatedEvent(user.Id, user.Email, user.Role));
        return user;
    }

    // ── Comportamentos ───────────────────────────────────────────────
    public void LinkGoogle(string googleId)
    {
        GoogleId    = googleId;
        UpdatedAt   = DateTimeOffset.UtcNow;
    }

    public void LinkApple(string appleId)
    {
        AppleId   = appleId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        UpdatedAt    = DateTimeOffset.UtcNow;
    }

    public void VerifyEmail()
    {
        IsEmailVerified = true;
        UpdatedAt       = DateTimeOffset.UtcNow;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTimeOffset.UtcNow;
        UpdatedAt   = DateTimeOffset.UtcNow;
    }

    public void UpdateProfile(string? name, string? avatarUrl)
    {
        Name      = name;
        AvatarUrl = avatarUrl;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetPhone(string? phone)
    {
        if (phone is not null && phone.Length > 20)
            throw new ArgumentException("Telefone deve ter no máximo 20 caracteres.", nameof(phone));
        Phone     = phone?.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void GrantPermission(UserPermission permission)
    {
        var current = Permissions.ToList();
        if (current.Contains(permission)) return;

        current.Add(permission);
        _permissionsRaw = string.Join(',', current.Select(p => p.ToString()));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RevokePermission(UserPermission permission)
    {
        var current = Permissions.ToList();
        if (!current.Remove(permission)) return;

        _permissionsRaw = string.Join(',', current.Select(p => p.ToString()));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool HasPermission(UserPermission permission) =>
        Permissions.Contains(permission);

    // ── Helpers privados ─────────────────────────────────────────────
    /// <summary>
    /// Atribui permissões padrão com base no role ao criar o usuário.
    /// </summary>
    private static void DefaultPermissions(User user)
    {
        var permissions = user.Role switch
        {
            UserRole.PlatformAdmin => Enum.GetValues<UserPermission>(), // tudo
            UserRole.TenantOwner   =>
            [
                UserPermission.ManageTenant,  UserPermission.ViewTenant,
                UserPermission.ManageStaff,   UserPermission.ViewStaff,
                UserPermission.ManageServices,UserPermission.ViewServices,
                UserPermission.ManageBookings,UserPermission.ViewBookings,
                UserPermission.CreateBooking, UserPermission.CancelBooking,
                UserPermission.ViewReports,   UserPermission.ExportReports,
                UserPermission.ManageBilling, UserPermission.ViewBilling
            ],
            UserRole.TenantAdmin =>
            [
                UserPermission.ViewTenant,
                UserPermission.ManageStaff,   UserPermission.ViewStaff,
                UserPermission.ManageServices,UserPermission.ViewServices,
                UserPermission.ManageBookings,UserPermission.ViewBookings,
                UserPermission.CreateBooking, UserPermission.CancelBooking,
                UserPermission.ViewReports,   UserPermission.ViewBilling
            ],
            UserRole.TenantStaff =>
            [
                UserPermission.ViewServices,
                UserPermission.ViewBookings,
                UserPermission.CreateBooking,
                UserPermission.CancelBooking
            ],
            UserRole.Customer =>
            [
                UserPermission.CreateBooking,
                UserPermission.CancelBooking,
                UserPermission.ViewBookings
            ],
            _ => Array.Empty<UserPermission>()
        };

        user._permissionsRaw = string.Join(',', permissions.Select(p => p.ToString()));
    }
}
