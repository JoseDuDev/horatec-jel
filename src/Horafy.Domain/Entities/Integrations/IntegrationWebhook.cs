using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Integrations;

/// <summary>
/// Endpoint de webhook de saída por tenant (write-back para integrações como o Atendefy).
/// Tabela global (schema public). Um endpoint ativo por tenant. O <see cref="Secret"/> é
/// um segredo compartilhado usado para assinar o payload (HMAC-SHA256).
/// </summary>
public sealed class IntegrationWebhook : BaseEntity
{
    private IntegrationWebhook() { } // EF Core

    public Guid TenantId { get; private set; }
    public string Url { get; private set; } = default!;
    public string Secret { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;

    public static IntegrationWebhook Create(Guid tenantId, string url, string secret) => new()
    {
        TenantId = tenantId,
        Url      = url.Trim(),
        Secret   = secret,
        IsActive = true
    };

    public void UpdateUrl(string url)
    {
        Url       = url.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RotateSecret(string secret)
    {
        Secret    = secret;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        IsActive  = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive  = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
