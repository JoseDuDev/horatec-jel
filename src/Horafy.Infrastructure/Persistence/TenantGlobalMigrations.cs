using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Horafy.Infrastructure.Persistence;

/// <summary>
/// DDL idempotente aplicada a <b>todos</b> os schemas tenant_* no startup.
///
/// Existe porque o <c>TenantSchemaService</c> só roda quando um tenant é criado:
/// tabela nova acrescentada lá chega nos clientes novos e nunca nos antigos. Este é
/// o equivalente do <see cref="GlobalMigrations"/> (que cuida do schema public) para
/// o schema de cada tenant.
///
/// Tudo aqui precisa ser IF NOT EXISTS / ADD COLUMN IF NOT EXISTS — roda a cada boot.
/// </summary>
public static class TenantGlobalMigrations
{
    private static readonly Regex SchemaPattern = new(@"^[a-z0-9_\-]+$", RegexOptions.Compiled);

    public static async Task RunAsync(HorafyDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var schemas = await ReadTenantSchemasAsync(db, ct);

        logger.LogInformation("Running tenant migrations for {Count} tenants...", schemas.Count);

        foreach (var schema in schemas)
        {
            if (!SchemaPattern.IsMatch(schema))
            {
                logger.LogWarning("Schema com nome inesperado ignorado: {Schema}", schema);
                continue;
            }

            try
            {
                // schema validado acima — suprime EF1002 intencionalmente, DDL não aceita parâmetro
#pragma warning disable EF1002
                await db.Database.ExecuteSqlRawAsync(Script($"\"{schema}\""), ct);
#pragma warning restore EF1002
            }
            catch (Exception ex)
            {
                // Um tenant quebrado não pode impedir a API de subir para os outros.
                logger.LogError(ex, "Falha na migração de imagens do schema {Schema}", schema);
            }
        }

        logger.LogInformation("Tenant migrations complete.");
    }

    private static async Task<List<string>> ReadTenantSchemasAsync(
        HorafyDbContext db, CancellationToken ct)
    {
        var schemas    = new List<string>();
        var connection = db.Database.GetDbConnection();
        var wasClosed  = connection.State != System.Data.ConnectionState.Open;

        if (wasClosed) await connection.OpenAsync(ct);

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "SELECT schema_name FROM information_schema.schemata WHERE schema_name LIKE 'tenant_%'";

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                schemas.Add(reader.GetString(0));
        }
        finally
        {
            if (wasClosed) await connection.CloseAsync();
        }

        return schemas;
    }

    private static string Script(string s) => $"""
        -- ── Galeria de fotos do item de locação ────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.rentable_item_images (
            id               UUID          NOT NULL DEFAULT gen_random_uuid(),
            rentable_item_id UUID          NOT NULL,
            url              VARCHAR(2000) NOT NULL,
            sort_order       INT           NOT NULL DEFAULT 0,
            created_at       TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
            updated_at       TIMESTAMPTZ,
            created_by       VARCHAR(256),
            updated_by       VARCHAR(256),
            is_deleted       BOOLEAN       NOT NULL DEFAULT FALSE,
            deleted_at       TIMESTAMPTZ,
            deleted_by       VARCHAR(256),
            CONSTRAINT pk_rentable_item_images PRIMARY KEY (id),
            CONSTRAINT fk_rentable_item_images_rentable_items
                FOREIGN KEY (rentable_item_id) REFERENCES {s}.rentable_items (id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS ix_rentable_item_images_item_sort
            ON {s}.rentable_item_images (rentable_item_id, sort_order);

        -- ── Foto do serviço ─────────────────────────────────────────────────
        ALTER TABLE {s}.services
            ADD COLUMN IF NOT EXISTS image_url VARCHAR(2000);

        -- Itens que já tinham uma URL avulsa entram na galeria como capa, para que
        -- a vitrine tenha um caminho de leitura só. O NOT EXISTS deixa repetível.
        INSERT INTO {s}.rentable_item_images (id, rentable_item_id, url, sort_order, created_at, is_deleted)
        SELECT gen_random_uuid(), i.id, i.image_url, 0, NOW(), FALSE
        FROM {s}.rentable_items i
        WHERE i.image_url IS NOT NULL
          AND i.image_url <> ''
          AND NOT EXISTS (
              SELECT 1 FROM {s}.rentable_item_images g
              WHERE g.rentable_item_id = i.id
          );
        """;
}
