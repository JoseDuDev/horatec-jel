# Plan 017: Índices compostos para os padrões de consulta de bookings (job de lembrete + status/data)

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving on. If any
> STOP condition occurs, stop and report. When done, update the status row for
> this plan in `plans/README.md` — unless a reviewer dispatched you and told you
> they maintain the index.
>
> **Drift check (run first)**: `git diff --stat 74ded3f..HEAD -- src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingEntityConfiguration.cs src/Horafy.Infrastructure/Messaging/Jobs/BookingReminderJob.cs`
> If either in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P2
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: perf
- **Planned at**: commit `74ded3f`, 2026-06-28

## Why this matters

`BookingReminderJob` runs **every hour** and scans the `bookings` table with two
recurring predicate shapes: `Status == Confirmed && ScheduledAt ∈ [min,max)` (appointment
reminders) and `Kind == Rental && RentalStatus == PickedUp && EndsAt ∈ [min,max)`
(rental return/overdue). The dashboard and revenue readers also filter by
`ScheduledAt`/`Kind` + status over a date range. The current indexes are
`(ResourceId, ScheduledAt)`, `(CustomerId, ScheduledAt)`, and `Status` alone —
none of which serves the reminder predicates well, so on a large tenant these
become repeated index-scan-then-filter or seq scans. Two composite indexes turn
the hourly reminder scans and the date+status reports into index range scans.
Adding read-only indexes is low risk (no write-path logic change).

## Current state

- `src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingEntityConfiguration.cs`
  — current indexes (end of `Configure`):

```csharp
        builder.HasIndex(b => b.ExternalId)
            .IsUnique()
            .HasDatabaseName("ix_bookings_external_id")
            .HasFilter("external_id IS NOT NULL");

        builder.HasIndex(b => b.RecurrenceGroupId)
            .HasDatabaseName("ix_bookings_recurrence_group")
            .HasFilter("recurrence_group_id IS NOT NULL");

        builder.HasIndex(b => new { b.ResourceId, b.ScheduledAt })
            .HasDatabaseName("ix_bookings_resource_scheduled");

        builder.HasIndex(b => new { b.CustomerId, b.ScheduledAt })
            .HasDatabaseName("ix_bookings_customer_scheduled");

        builder.HasIndex(b => b.Status)
            .HasDatabaseName("ix_bookings_status");
```

- `Booking` columns referenced: `Status` (string-converted enum), `ScheduledAt`
  (timestamptz), `Kind` (string-converted enum, default `Appointment`),
  `RentalStatus` (string-converted enum, nullable), `EndsAt` (timestamptz). All
  already mapped.
- The reminder predicates live in `src/Horafy.Infrastructure/Messaging/Jobs/BookingReminderJob.cs`.
- Migrations for tenant-schema entities go in `src/Horafy.Infrastructure/Migrations`
  with `--context TenantDbContext`. The public-schema context is `HorafyDbContext`
  (different folder) — do NOT use it here. Example existing tenant migration:
  `src/Horafy.Infrastructure/Migrations/20260628161600_Sprint7ReviewsAndBlackout.cs`.

## Commands you will need

| Purpose | Command | Expected |
|---------|---------|----------|
| Build | `dotnet build Horafy.sln -c Debug --nologo` | `0 Erro(s)` |
| Add migration | `dotnet ef migrations add AddBookingReminderIndexes --context TenantDbContext --project src/Horafy.Infrastructure --startup-project src/Horafy.API --no-build` | `Done.` |
| Tests | `dotnet test Horafy.sln --nologo -v q` | all pass |

## Scope

**In scope**:
- `src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingEntityConfiguration.cs`
- the generated migration files under `src/Horafy.Infrastructure/Migrations/` (created by the `ef` command)
- the generated `TenantDbContextModelSnapshot.cs` update (the `ef` command edits it)

**Out of scope** (do NOT touch):
- `HorafyDbContext` and anything under `src/Horafy.Infrastructure/Persistence/Migrations/` (public schema).
- Query/handler code — this plan only adds indexes; it changes no behavior.
- Dropping the existing `ix_bookings_status` (see Maintenance notes — left as a follow-up).

## Git workflow

- Branch: `advisor/017-booking-composite-indexes`
- Conventional commits, e.g. `perf(db): índices compostos para queries de lembrete e status/data`.
- Do NOT push or open a PR.

## Steps

### Step 1: Add the two composite indexes to the configuration

Append to the end of `BookingEntityConfiguration.Configure`:

```csharp
        // Lembretes de agendamento e relatórios filtram por Status + intervalo de ScheduledAt.
        builder.HasIndex(b => new { b.Status, b.ScheduledAt })
            .HasDatabaseName("ix_bookings_status_scheduled");

        // Lembretes de locação filtram por Kind + RentalStatus + intervalo de EndsAt.
        builder.HasIndex(b => new { b.Kind, b.RentalStatus, b.EndsAt })
            .HasDatabaseName("ix_bookings_kind_rentalstatus_ends");
```

**Verify**: `dotnet build Horafy.sln -c Debug --nologo` → `0 Erro(s)`.

### Step 2: Generate the tenant migration

Run:
```
dotnet ef migrations add AddBookingReminderIndexes --context TenantDbContext --project src/Horafy.Infrastructure --startup-project src/Horafy.API --no-build
```

**Verify**: command prints `Done.`; a new file
`src/Horafy.Infrastructure/Migrations/<timestamp>_AddBookingReminderIndexes.cs`
exists and its `Up` contains exactly two `CreateIndex` calls for
`ix_bookings_status_scheduled` (columns `status, scheduled_at`) and
`ix_bookings_kind_rentalstatus_ends` (columns `kind, rental_status, ends_at`),
and NOTHING else (no unexpected table/column changes). If the migration contains
anything beyond those two indexes, STOP and report.

### Step 3: Build and run the suite

**Verify**:
- `dotnet build Horafy.sln -c Debug --nologo` → `0 Erro(s)`
- `dotnet test Horafy.sln --nologo -v q` → all pass (no test depends on index
  presence, so the count is unchanged; this just confirms nothing broke).

## Test plan

- No new tests. Indexes are a non-functional change; correctness is unchanged.
- The migration content check in Step 2 is the substantive verification: the `Up`
  must contain only the two intended `CreateIndex` calls.

## Done criteria

ALL must hold:

- [ ] `dotnet build Horafy.sln -c Debug --nologo` exits `0 Erro(s)`
- [ ] A migration `*_AddBookingReminderIndexes.cs` exists under `src/Horafy.Infrastructure/Migrations/` whose `Up` creates exactly the two named indexes and nothing else
- [ ] `dotnet test Horafy.sln --nologo -v q` — all pass
- [ ] Only in-scope files changed (`git status` shows the config + the new migration pair + the tenant snapshot)
- [ ] `plans/README.md` status row updated (unless your reviewer maintains it)

## STOP conditions

Stop and report back if:

- The `ef migrations add` output diff includes table creates/drops or column
  changes beyond the two indexes (means the model drifted — do not ship a
  migration that does more than intended).
- `EndsAt` or `RentalStatus` turn out not to be mapped columns (the build/migration
  would fail) — report it.
- The `dotnet ef` tool is unavailable or a different version errors — report the
  exact error; do not hand-write the migration.

## Maintenance notes

- After this lands, `ix_bookings_status` (Status alone) is redundant — the new
  `(Status, ScheduledAt)` covers Status-prefix lookups. Dropping it is a safe
  follow-up (separate migration) once you've confirmed no query relies on a
  Status-only index name; left out of this plan to keep it purely additive/low-risk.
- If reminder volume grows, consider a partial index `WHERE kind = 'Rental'` on the
  rental composite to shrink it; measure with `EXPLAIN ANALYZE` first.
- Reviewer: confirm the migration is in the **tenant** folder (`.../Infrastructure/Migrations`),
  not the public `.../Persistence/Migrations`.
