using Horafy.Application.Interfaces;
using Horafy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Horafy.Infrastructure.MultiTenancy;

/// <summary>
/// Cria o schema isolado tenant_{slug} no PostgreSQL com todas as tabelas necessárias.
/// Usa SQL raw para criar as tabelas de forma idempotente (IF NOT EXISTS).
/// </summary>
internal sealed class TenantSchemaService(
    HorafyDbContext globalContext,
    IConfiguration configuration,
    ILogger<TenantSchemaService> logger) : ITenantSchemaService
{
    public async Task CreateSchemaAsync(string slug, CancellationToken cancellationToken = default)
    {
        var schemaName = $"tenant_{slug}";
        logger.LogInformation("Provisionando schema {Schema}...", schemaName);

        var sql = BuildSchemaScript(schemaName);

        await globalContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);

        logger.LogInformation("Schema {Schema} provisionado com sucesso.", schemaName);
    }

    private static string BuildSchemaScript(string schema) => $"""
        -- ── Schema ────────────────────────────────────────────────────────
        CREATE SCHEMA IF NOT EXISTS {schema};

        -- ── Serviços ───────────────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {schema}.services (
            id                UUID        NOT NULL DEFAULT gen_random_uuid(),
            name              VARCHAR(200) NOT NULL,
            description       TEXT,
            duration_minutes  INT          NOT NULL,
            price             NUMERIC(10,2) NOT NULL,
            category          VARCHAR(100),
            is_active         BOOLEAN      NOT NULL DEFAULT TRUE,
            created_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
            updated_at        TIMESTAMPTZ,
            created_by        VARCHAR(256),
            updated_by        VARCHAR(256),
            is_deleted        BOOLEAN      NOT NULL DEFAULT FALSE,
            deleted_at        TIMESTAMPTZ,
            deleted_by        VARCHAR(256),
            CONSTRAINT pk_services PRIMARY KEY (id)
        );

        CREATE INDEX IF NOT EXISTS ix_services_name
            ON {schema}.services (name);

        -- ── Profissionais ──────────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {schema}.professionals (
            id          UUID         NOT NULL DEFAULT gen_random_uuid(),
            name        VARCHAR(150) NOT NULL,
            email       VARCHAR(256),
            phone       VARCHAR(20),
            specialty   VARCHAR(100),
            bio         VARCHAR(500),
            avatar_url  VARCHAR(500),
            user_id     UUID,
            is_active   BOOLEAN      NOT NULL DEFAULT TRUE,
            created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
            updated_at  TIMESTAMPTZ,
            created_by  VARCHAR(256),
            updated_by  VARCHAR(256),
            is_deleted  BOOLEAN      NOT NULL DEFAULT FALSE,
            deleted_at  TIMESTAMPTZ,
            deleted_by  VARCHAR(256),
            CONSTRAINT pk_professionals PRIMARY KEY (id)
        );

        -- ── Agendamentos ───────────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {schema}.bookings (
            id                   UUID         NOT NULL DEFAULT gen_random_uuid(),
            service_id           UUID         NOT NULL,
            professional_id      UUID         NOT NULL,
            customer_id          UUID         NOT NULL,
            customer_name        VARCHAR(150) NOT NULL,
            customer_email       VARCHAR(256) NOT NULL,
            scheduled_at         TIMESTAMPTZ  NOT NULL,
            ends_at              TIMESTAMPTZ  NOT NULL,
            duration_minutes     INT          NOT NULL,
            notes                VARCHAR(1000),
            status               VARCHAR(32)  NOT NULL DEFAULT 'Pending',
            cancellation_reason  VARCHAR(500),
            confirmed_at         TIMESTAMPTZ,
            cancelled_at         TIMESTAMPTZ,
            completed_at         TIMESTAMPTZ,
            created_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
            updated_at           TIMESTAMPTZ,
            created_by           VARCHAR(256),
            updated_by           VARCHAR(256),
            is_deleted           BOOLEAN      NOT NULL DEFAULT FALSE,
            deleted_at           TIMESTAMPTZ,
            deleted_by           VARCHAR(256),
            CONSTRAINT pk_bookings PRIMARY KEY (id),
            CONSTRAINT fk_bookings_services
                FOREIGN KEY (service_id) REFERENCES {schema}.services (id),
            CONSTRAINT fk_bookings_professionals
                FOREIGN KEY (professional_id) REFERENCES {schema}.professionals (id)
        );

        CREATE INDEX IF NOT EXISTS ix_bookings_professional_scheduled
            ON {schema}.bookings (professional_id, scheduled_at)
            WHERE is_deleted = FALSE;

        CREATE INDEX IF NOT EXISTS ix_bookings_customer_scheduled
            ON {schema}.bookings (customer_id, scheduled_at DESC);

        CREATE INDEX IF NOT EXISTS ix_bookings_status
            ON {schema}.bookings (status);
        """;
}
