namespace Horafy.Application.Interfaces;

/// <summary>
/// Responsável por criar e migrar o schema isolado de um tenant no PostgreSQL.
/// </summary>
public interface ITenantSchemaService
{
    /// <summary>
    /// Cria o schema tenant_{slug} e todas as tabelas necessárias.
    /// Idempotente — pode ser chamado mais de uma vez sem efeitos colaterais.
    /// </summary>
    Task CreateSchemaAsync(string slug, CancellationToken cancellationToken = default);
}
