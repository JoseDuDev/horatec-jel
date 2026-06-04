using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Resources;

public sealed class Resource : BaseEntity
{
    private Resource() { }

    public string Name { get; private set; } = default!;
    public ResourceType Type { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Specialty { get; private set; }
    public string? Bio { get; private set; }
    public string? AvatarUrl { get; private set; }
    public Guid? UserId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static Resource Create(
        string name,
        ResourceType type,
        string? email = null,
        string? phone = null,
        string? specialty = null,
        string? bio = null,
        string? avatarUrl = null,
        Guid? userId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Resource
        {
            Name      = name.Trim(),
            Type      = type,
            Email     = email?.ToLowerInvariant().Trim(),
            Phone     = phone?.Trim(),
            Specialty = specialty?.Trim(),
            Bio       = bio?.Trim(),
            AvatarUrl = avatarUrl,
            UserId    = userId
        };
    }

    public void Update(string name, string? email, string? phone,
        string? specialty, string? bio, string? avatarUrl)
    {
        Name      = name.Trim();
        Email     = email?.ToLowerInvariant().Trim();
        Phone     = phone?.Trim();
        Specialty = specialty?.Trim();
        Bio       = bio?.Trim();
        AvatarUrl = avatarUrl;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()   { IsActive = true;  UpdatedAt = DateTimeOffset.UtcNow; }
    public void Deactivate() { IsActive = false; UpdatedAt = DateTimeOffset.UtcNow; }
}
