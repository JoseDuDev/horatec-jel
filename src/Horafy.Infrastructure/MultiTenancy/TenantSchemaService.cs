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

    private static string BuildSchemaScript(string schema)
    {
        // Identifiers with hyphens require double-quoting in PostgreSQL DDL
        var s = $"\"{schema}\"";
        return $"""
        -- ── Schema ────────────────────────────────────────────────────────
        CREATE SCHEMA IF NOT EXISTS {s};

        -- ── Serviços ───────────────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.services (
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
            ON {s}.services (name);

        -- ── Itens de Locação ───────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.rentable_items (
            id                UUID          NOT NULL DEFAULT gen_random_uuid(),
            name              VARCHAR(200)  NOT NULL,
            description       TEXT,
            category          VARCHAR(100),
            quantity          INT           NOT NULL DEFAULT 1,
            daily_rate        NUMERIC(10,2) NOT NULL DEFAULT 0,
            security_deposit  NUMERIC(10,2) NOT NULL DEFAULT 0,
            buffer_days       INT           NOT NULL DEFAULT 0,
            image_url         VARCHAR(2000),
            is_active         BOOLEAN       NOT NULL DEFAULT TRUE,
            created_at        TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
            updated_at        TIMESTAMPTZ,
            created_by        VARCHAR(256),
            updated_by        VARCHAR(256),
            is_deleted        BOOLEAN       NOT NULL DEFAULT FALSE,
            deleted_at        TIMESTAMPTZ,
            deleted_by        VARCHAR(256),
            CONSTRAINT pk_rentable_items PRIMARY KEY (id)
        );

        CREATE INDEX IF NOT EXISTS ix_rentable_items_name
            ON {s}.rentable_items (name);

        CREATE INDEX IF NOT EXISTS ix_rentable_items_is_active
            ON {s}.rentable_items (is_active);

        -- ── Recursos ──────────────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.resources (
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
            ON {s}.resources (type);

        CREATE INDEX IF NOT EXISTS ix_resources_is_active
            ON {s}.resources (is_active);

        -- ── Vínculo Recurso × Serviço ─────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.resource_services (
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
                FOREIGN KEY (resource_id) REFERENCES {s}.resources (id),
            CONSTRAINT fk_resource_services_services
                FOREIGN KEY (service_id) REFERENCES {s}.services (id)
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ix_resource_services_resource_service
            ON {s}.resource_services (resource_id, service_id)
            WHERE is_deleted = FALSE;

        -- ── Agendamentos ───────────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.bookings (
            id                   UUID         NOT NULL DEFAULT gen_random_uuid(),
            service_id           UUID,                       -- nulo em locações (Kind = Rental)
            resource_id          UUID,                       -- nulo em locações (Kind = Rental)
            customer_id          UUID         NOT NULL,
            customer_name        VARCHAR(150) NOT NULL,
            customer_email       VARCHAR(256) NOT NULL,
            customer_phone       VARCHAR(20),
            resource_name        VARCHAR(150) NOT NULL DEFAULT '',
            scheduled_at         TIMESTAMPTZ  NOT NULL,
            ends_at              TIMESTAMPTZ  NOT NULL,
            duration_minutes     INT          NOT NULL,
            notes                VARCHAR(1000),
            status               VARCHAR(32)  NOT NULL DEFAULT 'Pending',
            cancellation_reason  VARCHAR(500),
            confirmed_at         TIMESTAMPTZ,
            cancelled_at         TIMESTAMPTZ,
            completed_at         TIMESTAMPTZ,
            recurrence_group_id  UUID,
            expires_at           TIMESTAMPTZ,
            created_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
            updated_at           TIMESTAMPTZ,
            created_by           VARCHAR(256),
            updated_by           VARCHAR(256),
            is_deleted           BOOLEAN      NOT NULL DEFAULT FALSE,
            deleted_at           TIMESTAMPTZ,
            deleted_by           VARCHAR(256),
            CONSTRAINT pk_bookings PRIMARY KEY (id),
            CONSTRAINT fk_bookings_services
                FOREIGN KEY (service_id) REFERENCES {s}.services (id),
            CONSTRAINT fk_bookings_resources
                FOREIGN KEY (resource_id) REFERENCES {s}.resources (id)
        );

        CREATE INDEX IF NOT EXISTS ix_bookings_resource_scheduled
            ON {s}.bookings (resource_id, scheduled_at)
            WHERE is_deleted = FALSE;

        CREATE INDEX IF NOT EXISTS ix_bookings_customer_scheduled
            ON {s}.bookings (customer_id, scheduled_at DESC);

        CREATE INDEX IF NOT EXISTS ix_bookings_status
            ON {s}.bookings (status);

        CREATE INDEX IF NOT EXISTS ix_bookings_recurrence_group
            ON {s}.bookings (recurrence_group_id)
            WHERE recurrence_group_id IS NOT NULL;

        -- ── Serviços por Agendamento ───────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.booking_services (
            id               UUID         NOT NULL DEFAULT gen_random_uuid(),
            booking_id       UUID          NOT NULL,
            service_id       UUID          NOT NULL,
            service_name     VARCHAR(200)  NOT NULL,
            duration_minutes INT           NOT NULL,
            price            NUMERIC(10,2) NOT NULL DEFAULT 0,
            CONSTRAINT pk_booking_services PRIMARY KEY (id),
            CONSTRAINT fk_booking_services_booking
                FOREIGN KEY (booking_id) REFERENCES {s}.bookings (id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS ix_booking_services_booking
            ON {s}.booking_services (booking_id);

        -- ── Pagamentos ─────────────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.payments (
            id               UUID          NOT NULL DEFAULT gen_random_uuid(),
            booking_id       UUID          NOT NULL,
            preference_id    VARCHAR(100)  NOT NULL,
            mp_payment_id    VARCHAR(100),
            method           VARCHAR(32)   NOT NULL,
            status           VARCHAR(32)   NOT NULL DEFAULT 'Pending',
            amount           NUMERIC(10,2) NOT NULL,
            deposit_amount   NUMERIC(10,2) NOT NULL DEFAULT 0,
            payment_url      VARCHAR(500),
            paid_at          TIMESTAMPTZ,
            expires_at       TIMESTAMPTZ,
            created_at       TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
            updated_at       TIMESTAMPTZ,
            created_by       VARCHAR(256),
            updated_by       VARCHAR(256),
            is_deleted       BOOLEAN       NOT NULL DEFAULT FALSE,
            deleted_at       TIMESTAMPTZ,
            deleted_by       VARCHAR(256),
            CONSTRAINT pk_payments PRIMARY KEY (id),
            CONSTRAINT fk_payments_bookings
                FOREIGN KEY (booking_id) REFERENCES {s}.bookings (id)
        );

        CREATE UNIQUE INDEX IF NOT EXISTS uq_payments_mp_payment_id
            ON {s}.payments (mp_payment_id)
            WHERE mp_payment_id IS NOT NULL;

        CREATE INDEX IF NOT EXISTS ix_payments_booking_id
            ON {s}.payments (booking_id);

        CREATE INDEX IF NOT EXISTS ix_payments_status_created
            ON {s}.payments (status, created_at DESC)
            WHERE is_deleted = FALSE;

        -- ── Coluna de status de pagamento nos agendamentos ─────────────────
        ALTER TABLE {s}.bookings
            ADD COLUMN IF NOT EXISTS payment_status VARCHAR(32) NOT NULL DEFAULT 'NotRequired';

        -- ── Nome do recurso (snapshot) e preço por serviço (snapshot) ──────
        ALTER TABLE {s}.bookings
            ADD COLUMN IF NOT EXISTS resource_name VARCHAR(150) NOT NULL DEFAULT '';

        ALTER TABLE {s}.booking_services
            ADD COLUMN IF NOT EXISTS price NUMERIC(10,2) NOT NULL DEFAULT 0;

        -- ── Modo da reserva: Appointment (agendamento) ou Rental (locação) ──
        ALTER TABLE {s}.bookings
            ADD COLUMN IF NOT EXISTS kind VARCHAR(32) NOT NULL DEFAULT 'Appointment';

        -- Locações não têm serviço/recurso — torna as colunas anuláveis (idempotente).
        ALTER TABLE {s}.bookings ALTER COLUMN service_id  DROP NOT NULL;
        ALTER TABLE {s}.bookings ALTER COLUMN resource_id DROP NOT NULL;

        -- ── Vínculo da linha com item de locação (snapshot de unidades) ────
        ALTER TABLE {s}.booking_services
            ADD COLUMN IF NOT EXISTS rentable_item_id UUID;

        ALTER TABLE {s}.booking_services
            ADD COLUMN IF NOT EXISTS quantity INT NOT NULL DEFAULT 1;

        CREATE INDEX IF NOT EXISTS ix_booking_services_rentable_item
            ON {s}.booking_services (rentable_item_id)
            WHERE rentable_item_id IS NOT NULL;

        -- ── Fila de Espera ─────────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.waitlist_entries (
            id             UUID         NOT NULL DEFAULT gen_random_uuid(),
            service_id     UUID         NOT NULL,
            resource_id    UUID         NOT NULL,
            customer_id    UUID         NOT NULL,
            customer_name  VARCHAR(150) NOT NULL,
            customer_email VARCHAR(256) NOT NULL,
            preferred_date DATE         NOT NULL,
            status         VARCHAR(32)  NOT NULL DEFAULT 'Waiting',
            created_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
            updated_at     TIMESTAMPTZ,
            created_by     VARCHAR(256),
            updated_by     VARCHAR(256),
            is_deleted     BOOLEAN      NOT NULL DEFAULT FALSE,
            deleted_at     TIMESTAMPTZ,
            deleted_by     VARCHAR(256),
            CONSTRAINT pk_waitlist_entries PRIMARY KEY (id),
            CONSTRAINT fk_waitlist_entries_services
                FOREIGN KEY (service_id) REFERENCES {s}.services (id),
            CONSTRAINT fk_waitlist_entries_resources
                FOREIGN KEY (resource_id) REFERENCES {s}.resources (id)
        );

        CREATE INDEX IF NOT EXISTS ix_waitlist_service_resource_date
            ON {s}.waitlist_entries (service_id, resource_id, preferred_date)
            WHERE is_deleted = FALSE AND status = 'Waiting';

        CREATE INDEX IF NOT EXISTS ix_waitlist_customer
            ON {s}.waitlist_entries (customer_id)
            WHERE is_deleted = FALSE;

        -- ── Horários do Tenant ─────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.business_hours (
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
            ON {s}.business_hours (day_of_week)
            WHERE is_deleted = FALSE;

        -- ── Regras de Disponibilidade ──────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.availability_rules (
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
                FOREIGN KEY (resource_id) REFERENCES {s}.resources (id)
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ix_availability_rules_resource_day
            ON {s}.availability_rules (resource_id, day_of_week)
            WHERE is_deleted = FALSE;

        -- ── Exceções de Disponibilidade ────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.availability_exceptions (
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
                FOREIGN KEY (resource_id) REFERENCES {s}.resources (id)
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ix_availability_exceptions_resource_date
            ON {s}.availability_exceptions (resource_id, date)
            WHERE is_deleted = FALSE;

        -- ── Templates de Notificação ──────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.notification_templates (
            id               UUID         NOT NULL DEFAULT gen_random_uuid(),
            event_type       VARCHAR(50)  NOT NULL,
            channel          VARCHAR(20)  NOT NULL,
            subject_template VARCHAR(300),
            body_template    TEXT         NOT NULL,
            is_active        BOOLEAN      NOT NULL DEFAULT TRUE,
            created_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
            updated_at       TIMESTAMPTZ,
            created_by       VARCHAR(256),
            updated_by       VARCHAR(256),
            is_deleted       BOOLEAN      NOT NULL DEFAULT FALSE,
            deleted_at       TIMESTAMPTZ,
            deleted_by       VARCHAR(256),
            CONSTRAINT pk_notification_templates PRIMARY KEY (id)
        );

        CREATE INDEX IF NOT EXISTS ix_notification_templates_event_channel
            ON {s}.notification_templates (event_type, channel)
            WHERE is_active = TRUE AND is_deleted = FALSE;

        -- ── Avaliações ─────────────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.reviews (
            id           UUID         NOT NULL DEFAULT gen_random_uuid(),
            booking_id   UUID         NOT NULL,
            resource_id  UUID         NOT NULL,
            customer_id  UUID         NOT NULL,
            stars        SMALLINT     NOT NULL CHECK (stars BETWEEN 1 AND 5),
            comment      VARCHAR(1000),
            created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
            updated_at   TIMESTAMPTZ,
            created_by   VARCHAR(256),
            updated_by   VARCHAR(256),
            is_deleted   BOOLEAN      NOT NULL DEFAULT FALSE,
            deleted_at   TIMESTAMPTZ,
            deleted_by   VARCHAR(256),
            CONSTRAINT pk_reviews PRIMARY KEY (id),
            CONSTRAINT fk_reviews_bookings
                FOREIGN KEY (booking_id) REFERENCES {s}.bookings (id),
            CONSTRAINT uq_reviews_booking UNIQUE (booking_id)
        );

        CREATE INDEX IF NOT EXISTS ix_reviews_resource
            ON {s}.reviews (resource_id)
            WHERE is_deleted = FALSE;

        CREATE INDEX IF NOT EXISTS ix_reviews_customer
            ON {s}.reviews (customer_id)
            WHERE is_deleted = FALSE;

        -- ── Favoritos ──────────────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.favorite_services (
            id          UUID        NOT NULL DEFAULT gen_random_uuid(),
            customer_id UUID        NOT NULL,
            service_id  UUID        NOT NULL,
            created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_at  TIMESTAMPTZ,
            created_by  VARCHAR(256),
            updated_by  VARCHAR(256),
            is_deleted  BOOLEAN     NOT NULL DEFAULT FALSE,
            deleted_at  TIMESTAMPTZ,
            deleted_by  VARCHAR(256),
            CONSTRAINT pk_favorite_services PRIMARY KEY (id),
            CONSTRAINT fk_favorite_services_services
                FOREIGN KEY (service_id) REFERENCES {s}.services (id)
        );

        CREATE UNIQUE INDEX IF NOT EXISTS uq_favorite_services_customer_service
            ON {s}.favorite_services (customer_id, service_id)
            WHERE is_deleted = FALSE;

        CREATE INDEX IF NOT EXISTS ix_favorite_services_customer
            ON {s}.favorite_services (customer_id)
            WHERE is_deleted = FALSE;

        -- ── Carteira (Wallet) ──────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.wallets (
            id          UUID          NOT NULL DEFAULT gen_random_uuid(),
            user_id     UUID          NOT NULL,
            balance     NUMERIC(12,2) NOT NULL DEFAULT 0,
            created_at  TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
            updated_at  TIMESTAMPTZ,
            created_by  VARCHAR(256),
            updated_by  VARCHAR(256),
            is_deleted  BOOLEAN       NOT NULL DEFAULT FALSE,
            deleted_at  TIMESTAMPTZ,
            deleted_by  VARCHAR(256),
            CONSTRAINT pk_wallets PRIMARY KEY (id)
        );

        CREATE UNIQUE INDEX IF NOT EXISTS uq_wallets_user_id
            ON {s}.wallets (user_id);

        -- ── Transações da Carteira ─────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.wallet_transactions (
            id          UUID          NOT NULL DEFAULT gen_random_uuid(),
            wallet_id   UUID          NOT NULL,
            type        VARCHAR(32)   NOT NULL,
            amount      NUMERIC(12,2) NOT NULL,
            description VARCHAR(255)  NOT NULL,
            booking_id  UUID,
            created_at  TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
            updated_at  TIMESTAMPTZ,
            created_by  VARCHAR(256),
            updated_by  VARCHAR(256),
            is_deleted  BOOLEAN       NOT NULL DEFAULT FALSE,
            deleted_at  TIMESTAMPTZ,
            deleted_by  VARCHAR(256),
            CONSTRAINT pk_wallet_transactions PRIMARY KEY (id),
            CONSTRAINT fk_wallet_transactions_wallets
                FOREIGN KEY (wallet_id) REFERENCES {s}.wallets (id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS ix_wallet_transactions_wallet_id
            ON {s}.wallet_transactions (wallet_id);

        CREATE INDEX IF NOT EXISTS ix_wallet_transactions_booking_id
            ON {s}.wallet_transactions (booking_id)
            WHERE booking_id IS NOT NULL;

        -- ── Vouchers ───────────────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.vouchers (
            id             UUID          NOT NULL DEFAULT gen_random_uuid(),
            code           VARCHAR(50)   NOT NULL,
            discount_type  VARCHAR(16)   NOT NULL,
            discount_value NUMERIC(10,2) NOT NULL,
            description    VARCHAR(255),
            expires_at     TIMESTAMPTZ,
            max_uses       INT,
            used_count     INT           NOT NULL DEFAULT 0,
            is_active      BOOLEAN       NOT NULL DEFAULT TRUE,
            created_at     TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
            updated_at     TIMESTAMPTZ,
            created_by     VARCHAR(256),
            updated_by     VARCHAR(256),
            is_deleted     BOOLEAN       NOT NULL DEFAULT FALSE,
            deleted_at     TIMESTAMPTZ,
            deleted_by     VARCHAR(256),
            CONSTRAINT pk_vouchers PRIMARY KEY (id)
        );

        CREATE UNIQUE INDEX IF NOT EXISTS uq_vouchers_code
            ON {s}.vouchers (code);

        -- ── Colunas de desconto nos pagamentos ─────────────────────────────
        ALTER TABLE {s}.payments
            ADD COLUMN IF NOT EXISTS voucher_code VARCHAR(50);
        ALTER TABLE {s}.payments
            ADD COLUMN IF NOT EXISTS voucher_discount_amount NUMERIC(10,2) NOT NULL DEFAULT 0;
        ALTER TABLE {s}.payments
            ADD COLUMN IF NOT EXISTS wallet_amount NUMERIC(10,2) NOT NULL DEFAULT 0;
        """;
    }
}
