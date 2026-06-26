using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Integrations;

/// <summary>
/// Chave de API por tenant para integrações máquina-a-máquina (ex.: Atendefy).
/// Tabela global (schema public). A chave em texto puro é exibida UMA única vez na
/// criação; persistimos apenas o prefixo público (para lookup/exibição) e o hash
/// SHA-256 do segredo.
/// </summary>
public sealed class IntegrationApiKey : BaseEntity
{
    private IntegrationApiKey() { } // EF Core

    /// <summary>Tenant dono da chave (FK lógica para public.tenants).</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Rótulo amigável definido por quem criou (ex.: "Atendefy WhatsApp").</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Parte pública/lookup da chave (ex.: "htf_live_ab12cd34"). Único.</summary>
    public string KeyPrefix { get; private set; } = default!;

    /// <summary>Hash SHA-256 (hex) do segredo da chave.</summary>
    public string KeyHash { get; private set; } = default!;

    /// <summary>Escopos concedidos (csv), ex.: "catalog:read,bookings:write".</summary>
    public string Scopes { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? LastUsedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public static IntegrationApiKey Create(
        Guid tenantId, string name, string keyPrefix, string keyHash, string? scopes) => new()
    {
        TenantId  = tenantId,
        Name      = name,
        KeyPrefix = keyPrefix,
        KeyHash   = keyHash,
        Scopes    = scopes ?? string.Empty,
        IsActive  = true
    };

    public void MarkUsed() => LastUsedAt = DateTimeOffset.UtcNow;

    public void Revoke()
    {
        IsActive  = false;
        RevokedAt = DateTimeOffset.UtcNow;
    }
}
