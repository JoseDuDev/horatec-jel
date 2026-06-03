using Horafy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Horafy.Infrastructure.MultiTenancy;

/// <summary>
/// Responsável por criar e migrar o schema do tenant no PostgreSQL.
///
/// Fluxo:
///   1. Cria o schema se não existir:  CREATE SCHEMA IF NOT EXISTS tenant_{slug}
///   2. Aplica as migrations do EF Core no contexto do tenant
///
/// Chamado ao criar um novo tenant e no startup (para migrations pendentes).
/// </summary>
public sealed class TenantMigrationService(
    IDbContextFactory<HorafyDbContext> contextFactory,
    ILogger<TenantMigrationService> logger)
{
    public async Task MigrateTenantAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        // Valida o nome do schema para evitar SQL injection em DDL (CREATE SCHEMA não aceita parâmetros)
        // Permite apenas letras, números, underscores e hífens — nenhum caractere especial
        if (!System.Text.RegularExpressions.Regex.IsMatch(schemaName, @"^[a-z0-9_\-]+$"))
            throw new ArgumentException($"Nome de schema inválido: '{schemaName}'.", nameof(schemaName));

        logger.LogInformation("Iniciando migração para schema: {Schema}", schemaName);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Cria o schema no PostgreSQL se ainda não existir
        // schemaName foi validado acima — suprime o aviso EF1002 intencionalmente
#pragma warning disable EF1002
        await context.Database.ExecuteSqlRawAsync(
            $"CREATE SCHEMA IF NOT EXISTS \"{schemaName}\"",
            cancellationToken);
#pragma warning restore EF1002

        // Aplica migrations do EF Core no schema do tenant
        await context.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Migração concluída para schema: {Schema}", schemaName);
    }

    public async Task MigrateAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Busca todos os schemas de tenants via ADO.NET direto (evita limitações do EF SqlQueryRaw com string)
        var schemas = new List<string>();
        var connection = context.Database.GetDbConnection();

        await connection.OpenAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT schema_name FROM information_schema.schemata WHERE schema_name LIKE 'tenant_%'";

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            schemas.Add(reader.GetString(0));

        await connection.CloseAsync();

        logger.LogInformation("Iniciando migração em massa para {Count} tenants", schemas.Count);

        foreach (var schema in schemas)
        {
            try
            {
                await MigrateTenantAsync(schema, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha na migração do schema: {Schema}", schema);
            }
        }
    }
}
