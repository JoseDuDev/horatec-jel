namespace Horafy.Application.Interfaces;

/// <summary>
/// Fornece o contexto do tenant atual para toda a camada de aplicação.
/// Implementado na Infrastructure via HttpContext + cache.
/// </summary>
public interface ICurrentTenantService
{
    /// <summary>
    /// ID do tenant resolvido da requisição atual. Null em contexto fora de tenant.
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>
    /// Nome do schema PostgreSQL do tenant atual.
    /// </summary>
    string? SchemaName { get; }

    /// <summary>
    /// Slug do tenant atual.
    /// </summary>
    string? Slug { get; }

    /// <summary>
    /// Define o tenant atual (chamado pelo middleware de resolução).
    /// </summary>
    void SetTenant(Guid tenantId, string schemaName, string slug);

    /// <summary>
    /// Verifica se existe um tenant resolvido para a requisição atual.
    /// </summary>
    bool HasTenant => TenantId.HasValue;
}
