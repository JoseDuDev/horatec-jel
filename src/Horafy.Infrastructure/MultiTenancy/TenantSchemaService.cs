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
            id                UUID          NOT NULL DEFAULT gen_random_uuid(),
            name              VARCHAR(200)  NOT NULL,
            description       TEXT,
            duration_minutes  INT           NOT NULL,
            price             NUMERIC(10,2) NOT NULL,
            category          VARCHAR(100),
            is_active         BOOLEAN       NOT NULL DEFAULT TRUE,
            created_at        TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
            updated_at        TIMESTAMPTZ,
            created_by        VARCHAR(256),
            updated_by        VARCHAR(256),
            is_deleted        BOOLEAN       NOT NULL DEFAULT FALSE,
            deleted_at        TIMESTAMPTZ,
            deleted_by        VARCHAR(256),
            CONSTRAINT pk_services PRIMARY KEY (id)
        );

        CREATE INDEX IF NOT EXISTS ix_services_name
            ON {schema}.services (name);

        -- ── Recursos ──────────────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {schema}.resources (
            id          UUID         NOT NULL DEFAULT gen_random_uuid(),
            name        VARCHAR(150) NOT NULL,
            type        VARCHAR(32)  NOT NULL,
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
            CONSTRAINT pk_resources PRIMARY KEY (id)
        );

        CREATE INDEX IF NOT EXISTS ix_resources_type
            ON {schema}.resources (type);

        CREATE INDEX IF NOT EXISTS ix_resources_is_active
            ON {schema}.resources (is_active);

        -- ── Vínculo Recurso × Serviço ─────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {schema}.resource_services (
            id          UUID        NOT NULL DEFAULT gen_random_uuid(),
            resource_id UUID        NOT NULL,
            service_id  UUID        NOT NULL,
            created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_at  TIMESTAMPTZ,
            created_by  VARCHAR(256),
            updated_by  VARCHAR(256),
            is_deleted  BOOLEAN     NOT NULL DEFAULT FALSE,
            deleted_at  TIMESTAMPTZ,
            deleted_by  VARCHAR(256),
            CONSTRAINT pk_resource_services PRIMARY KEY (id),
            CONSTRAINT fk_resource_services_resources
                FOREIGN KEY (resource_id) REFERENCES {schema}.resources (id),
            CONSTRAINT fk_resource_services_services
                FOREIGN KEY (service_id) REFERENCES {schema}.services (id)
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ix_resource_services_resource_service
            ON {schema}.resource_services (resource_id, service_id)
            WHERE is_deleted = FALSE;

        -- ── Agendamentos ───────────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {schema}.bookings (
            id                   UUID         NOT NULL DEFAULT gen_random_uuid(),
            service_id           UUID         NOT NULL,
            resource_id          UUID         NOT NULL,
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
            CONSTRAINT fk_bookings_resources
                FOREIGN KEY (resource_id) REFERENCES {schema}.resources (id)
        );

        CREATE INDEX IF NOT EXISTS ix_bookings_resource_scheduled
            ON {schema}.bookings (resource_id, scheduled_at)
            WHERE is_deleted = FALSE;

        CREATE INDEX IF NOT EXISTS ix_bookings_customer_scheduled
            ON {schema}.bookings (customer_id, scheduled_at DESC);

        CREATE INDEX IF NOT EXISTS ix_bookings_status
            ON {schema}.bookings (status);

        -- ── Horários do Tenant ─────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {schema}.business_hours (
            id          UUID        NOT NULL DEFAULT gen_random_uuid(),
            day_of_week INT         NOT NULL,
            open_time   TIME        NOT NULL,
            close_time  TIME        NOT NULL,
            is_open     BOOLEAN     NOT NULL DEFAULT TRUE,
            created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_at  TIMESTAMPTZ,
            created_by  VARCHAR(256),
            updated_by  VARCHAR(256),
            is_deleted  BOOLEAN     NOT NULL DEFAULT FALSE,
            deleted_at  TIMESTAMPTZ,
            deleted_by  VARCHAR(256),
            CONSTRAINT pk_business_hours PRIMARY KEY (id)
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ix_business_hours_day
            ON {schema}.business_hours (day_of_week)
            WHERE is_deleted = FALSE;

        -- ── Regras de Disponibilidade ──────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {schema}.availability_rules (
            id                    UUID        NOT NULL DEFAULT gen_random_uuid(),
            resource_id           UUID        NOT NULL,
            day_of_week           INT         NOT NULL,
            start_time            TIME        NOT NULL,
            end_time              TIME        NOT NULL,
            slot_duration_minutes INT         NOT NULL,
            break_after_minutes   INT         NOT NULL DEFAULT 0,
            created_at            TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_at            TIMESTAMPTZ,
            created_by            VARCHAR(256),
            updated_by            VARCHAR(256),
            is_deleted            BOOLEAN     NOT NULL DEFAULT FALSE,
            deleted_at            TIMESTAMPTZ,
            deleted_by            VARCHAR(256),
            CONSTRAINT pk_availability_rules PRIMARY KEY (id),
            CONSTRAINT fk_availability_rules_resources
                FOREIGN KEY (resource_id) REFERENCES {schema}.resources (id)
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ix_availability_rules_resource_day
            ON {schema}.availability_rules (resource_id, day_of_week)
            WHERE is_deleted = FALSE;

        -- ── Exceções de Disponibilidade ────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {schema}.availability_exceptions (
            id           UUID        NOT NULL DEFAULT gen_random_uuid(),
            resource_id  UUID        NOT NULL,
            date         DATE        NOT NULL,
            is_blocked   BOOLEAN     NOT NULL DEFAULT FALSE,
            custom_start TIME,
            custom_end   TIME,
            reason       VARCHAR(500),
            created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_at   TIMESTAMPTZ,
            created_by   VARCHAR(256),
            updated_by   VARCHAR(256),
            is_deleted   BOOLEAN     NOT NULL DEFAULT FALSE,
            deleted_at   TIMESTAMPTZ,
            deleted_by   VARCHAR(256),
            CONSTRAINT pk_availability_exceptions PRIMARY KEY (id),
            CONSTRAINT fk_availability_exceptions_resources
                FOREIGN KEY (resource_id) REFERENCES {schema}.resources (id)
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ix_availability_exceptions_resource_date
            ON {schema}.availability_exceptions (resource_id, date)
            WHERE is_deleted = FALSE;
        """;
}
