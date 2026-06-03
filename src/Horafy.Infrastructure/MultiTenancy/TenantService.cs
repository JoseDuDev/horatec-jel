using Horafy.Application.Interfaces;

namespace Horafy.Infrastructure.MultiTenancy;

/// <summary>
/// Implementação scoped de ICurrentTenantService.
/// Uma instância por requisição HTTP; populada pelo TenantMiddleware
/// antes de qualquer handler ser executado.
/// </summary>
public sealed class TenantService : ICurrentTenantService
{
    public Guid? TenantId { get; private set; }
    public string? SchemaName { get; private set; }
    public string? Slug { get; private set; }

    public void SetTenant(Guid tenantId, string schemaName, string slug)
    {
        TenantId = tenantId;
        SchemaName = schemaName;
        Slug = slug;
    }
}
