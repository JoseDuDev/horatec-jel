# Plan 018: Agregar o CustomerListReader em SQL (eliminar a carga ilimitada do tenant)

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving on. If any
> STOP condition occurs, stop and report — do not improvise. When done, update
> the status row for this plan in `plans/README.md` — unless a reviewer
> dispatched you and told you they maintain the index.
>
> **Drift check (run first)**: `git diff --stat 74ded3f..HEAD -- src/Horafy.Infrastructure/Repositories/CustomerListReader.cs src/Horafy.Application/Interfaces/ICustomerListReader.cs`
> If either in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P3
- **Effort**: M
- **Risk**: MED
- **Depends on**: none (independent of 017/019)
- **Category**: perf
- **Planned at**: commit `74ded3f`, 2026-06-28

## Why this matters

`CustomerListReader.GetCustomersAsync` loads **every booking of the tenant**
(`context.Set<Booking>().Include(b => b.Services).ToListAsync()`) with **no date or
size bound**, then groups/aggregates in memory. It backs the admin customer export
(`GET /api/v1/reports/customers[/export]`). For a tenant with tens of thousands of
bookings this materializes the full history (booking rows × their service rows)
into memory on every export. Pushing the aggregation into SQL collapses that to a
few small result sets (≈ one row per customer). This is the only report reader with
**no bound at all** (Dashboard/Revenue are date-ranged — see Plan 019), so it has
the clearest scaling cliff.

## Current state

- `src/Horafy.Infrastructure/Repositories/CustomerListReader.cs` — full current body:

```csharp
internal sealed class CustomerListReader(TenantDbContext context) : ICustomerListReader
{
    public async Task<IReadOnlyList<CustomerExportRecord>> GetCustomersAsync(CancellationToken ct = default)
    {
        var bookings = await context.Set<Booking>()
            .AsNoTracking()
            .Include(b => b.Services)
            .ToListAsync(ct);

        return bookings
            .GroupBy(b => b.CustomerId)
            .Select(g =>
            {
                var latest     = g.OrderByDescending(b => b.ScheduledAt).First();
                var totalSpent = g
                    .Where(b => b.Status != BookingStatus.Cancelled)
                    .Sum(b => b.TotalAmount);

                return new CustomerExportRecord(
                    CustomerId:    g.Key,
                    Name:          latest.CustomerName,
                    Email:         latest.CustomerEmail,
                    Phone:         latest.CustomerPhone,
                    BookingCount:  g.Count(),
                    LastBookingAt: g.Max(b => b.ScheduledAt),
                    TotalSpent:    totalSpent);
            })
            .OrderBy(c => c.Name)
            .ToList();
    }
}
```

- The result record (in `src/Horafy.Application/Interfaces/ICustomerListReader.cs`):
  `CustomerExportRecord(Guid CustomerId, string Name, string Email, string? Phone, int BookingCount, DateTimeOffset? LastBookingAt, decimal TotalSpent)`.
- **Key constraint**: `Booking.TotalAmount` is a **computed property** (`=> _services.Sum(s => s.Price)`), NOT a mapped column. It cannot appear in a SQL query. To compute spend in SQL you must sum `BookingService.Price` over the booking's `Services` navigation. `BookingService` has `BookingId`, `ServiceName`, `Price`, `DurationMinutes` (collection navigation `Booking.Services`, configured in `TenantDbContext.OnModelCreating`).
- `Booking` mapped columns used here: `CustomerId`, `CustomerName`, `CustomerEmail`, `CustomerPhone`, `ScheduledAt`, `Status` (string-converted enum). A global query filter excludes soft-deleted rows automatically.
- **Semantics to preserve exactly**:
  - `BookingCount` = count of ALL the customer's bookings (cancelled included).
  - `LastBookingAt` = max `ScheduledAt` across ALL the customer's bookings.
  - `TotalSpent` = sum of service prices over the customer's **non-cancelled** bookings (cancelled excluded). A customer with only cancelled bookings → `TotalSpent == 0`.
  - `Name`/`Email`/`Phone` = the values from the customer's **latest** booking (max `ScheduledAt`).
  - Ordered by `Name` ascending.
- Test infra: integration tests against a real Postgres use **Testcontainers** —
  see `tests/Horafy.Application.Tests/Rentals/RentalStockConcurrencyTests.cs`
  (spins `PostgreSqlBuilder`, provisions the tenant schema via
  `TenantSchemaService.CreateSchemaAsync(slug)`, builds a `TenantDbContext` with
  `SearchPath = "tenant_{slug},public"`). The EF **InMemory** provider canNOT
  validate this SQL — you MUST use Testcontainers (requires Docker).

## Commands you will need

| Purpose | Command | Expected |
|---------|---------|----------|
| Build | `dotnet build Horafy.sln -c Debug --nologo` | `0 Erro(s)` |
| Test (this) | `dotnet test tests/Horafy.Application.Tests --nologo -v q --filter "FullyQualifiedName~CustomerListReader"` | all pass (requires Docker) |
| Full | `dotnet test Horafy.sln --nologo -v q` | all pass |

## Scope

**In scope**:
- `src/Horafy.Infrastructure/Repositories/CustomerListReader.cs`
- `tests/Horafy.Application.Tests/Reports/CustomerListReaderTests.cs` (create — Testcontainers integration test)

**Out of scope** (do NOT touch):
- `ICustomerListReader` / `CustomerExportRecord` shape — the output contract is frozen.
- `DashboardReader` / `RevenueReportReader` — covered by Plan 019.
- The controller/query handler (`GetCustomersReportQuery`, `ReportsController`).

## Git workflow

- Branch: `advisor/018-customer-reader-sql-aggregation`
- Conventional commits, e.g. `perf(reports): agrega CustomerListReader em SQL`.
- Do NOT push or open a PR.

## Steps

### Step 1: Rewrite the reader as three SQL queries + in-memory merge

Replace the body of `GetCustomersAsync`. Use three translatable queries instead of
loading all bookings:

```csharp
    public async Task<IReadOnlyList<CustomerExportRecord>> GetCustomersAsync(CancellationToken ct = default)
    {
        // (1) Contagem e último agendamento por cliente (todos os bookings).
        var stats = await context.Set<Booking>()
            .AsNoTracking()
            .GroupBy(b => b.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                Count      = g.Count(),
                Last       = g.Max(b => b.ScheduledAt),
            })
            .ToListAsync(ct);

        // (2) Total gasto = soma dos preços dos serviços de bookings NÃO cancelados.
        var spent = await context.Set<Booking>()
            .AsNoTracking()
            .Where(b => b.Status != BookingStatus.Cancelled)
            .SelectMany(b => b.Services, (b, s) => new { b.CustomerId, s.Price })
            .GroupBy(x => x.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(x => x.Price) })
            .ToListAsync(ct);
        var spentByCustomer = spent.ToDictionary(x => x.CustomerId, x => x.Total);

        // (3) Contato do agendamento mais recente por cliente (argmax via subconsulta correlata).
        var latest = await context.Set<Booking>()
            .AsNoTracking()
            .Where(b => b.ScheduledAt == context.Set<Booking>()
                .Where(x => x.CustomerId == b.CustomerId)
                .Max(x => x.ScheduledAt))
            .Select(b => new { b.CustomerId, b.CustomerName, b.CustomerEmail, b.CustomerPhone, b.ScheduledAt })
            .ToListAsync(ct);
        // Empates no mesmo ScheduledAt: mantém o primeiro determinístico por cliente.
        var contactByCustomer = latest
            .GroupBy(b => b.CustomerId)
            .ToDictionary(g => g.Key, g => g.First());

        return stats
            .Select(s =>
            {
                contactByCustomer.TryGetValue(s.CustomerId, out var c);
                spentByCustomer.TryGetValue(s.CustomerId, out var total);
                return new CustomerExportRecord(
                    CustomerId:    s.CustomerId,
                    Name:          c?.CustomerName  ?? string.Empty,
                    Email:         c?.CustomerEmail ?? string.Empty,
                    Phone:         c?.CustomerPhone,
                    BookingCount:  s.Count,
                    LastBookingAt: s.Last,
                    TotalSpent:    total);
            })
            .OrderBy(c => c.Name)
            .ToList();
    }
```

Notes for the executor:
- Keep the `using`s already in the file; you should not need new ones.
- `LastBookingAt` is `DateTimeOffset?` in the record but `Max(b => b.ScheduledAt)`
  yields `DateTimeOffset` — assign directly (implicit conversion to nullable is fine).

**Verify (translation)**: `dotnet build Horafy.sln -c Debug --nologo` → `0 Erro(s)`.
The real translation check happens in Step 2's test (build alone does not prove the
LINQ translates to SQL).

### Step 2: Add a Testcontainers parity test

Create `tests/Horafy.Application.Tests/Reports/CustomerListReaderTests.cs`, modeled
structurally on `RentalStockConcurrencyTests` (the `IAsyncLifetime` +
`PostgreSqlBuilder` + `TenantSchemaService.CreateSchemaAsync` + `NewTenantContext`
wiring). Seed, then assert the reader's output matches the documented semantics.

Seed at minimum:
- Customer A: 2 non-cancelled bookings (services summing to, say, 50 and 30) + 1
  cancelled booking (service 100). Latest booking carries name "Ana B" / email /
  phone. Expect: Count=3, TotalSpent=80 (cancelled 100 excluded), LastBookingAt =
  the latest, Name="Ana B".
- Customer B: 1 cancelled booking only (service 40). Expect: Count=1, TotalSpent=0,
  contact from that booking.
Assert the result is ordered by Name, has 2 records, and each field equals the
expected values above.

Use `Booking.Create(...)` / `Booking.Cancel(...)` (or the domain methods the other
tests use) to build bookings; set distinct `ScheduledAt` values so "latest" is
unambiguous. Mark the test class so it only runs with Docker available, matching
how `RentalStockConcurrencyTests` is treated in this repo (same project, same
`IAsyncLifetime` lifecycle — no special skip attribute is used there; follow that
precedent).

**Verify**: `dotnet test tests/Horafy.Application.Tests --nologo -v q --filter "FullyQualifiedName~CustomerListReaderTests"` → all pass. If a query throws an
`InvalidOperationException` about not being translatable, that is a real
translation failure — go to STOP conditions.

### Step 3: Full suite

**Verify**: `dotnet test Horafy.sln --nologo -v q` → all pass (the existing
`GetCustomersReportQueryTests` mock-level test still passes; the new integration
test passes).

## Test plan

- New `CustomerListReaderTests` (Testcontainers) is the parity guard: it pins
  Count/LastBookingAt/TotalSpent (cancelled-excluded)/latest-contact/ordering
  against a known seed — the exact semantics the in-memory version produced.
- Pattern source: `tests/Horafy.Application.Tests/Rentals/RentalStockConcurrencyTests.cs`.

## Done criteria

ALL must hold:

- [ ] `dotnet build Horafy.sln -c Debug --nologo` exits `0 Erro(s)`
- [ ] `CustomerListReader.GetCustomersAsync` contains no `.Include(b => b.Services)` and no `.ToListAsync()` that materializes all bookings before grouping (`grep -n "Include(b => b.Services)" src/Horafy.Infrastructure/Repositories/CustomerListReader.cs` returns nothing)
- [ ] `dotnet test Horafy.sln --nologo -v q` — all pass, including the new `CustomerListReaderTests`
- [ ] Only in-scope files changed (`git status`)
- [ ] `plans/README.md` status row updated (unless your reviewer maintains it)

## STOP conditions

Stop and report back (do not improvise) if:

- Any of the three queries throws a translation error (EF `InvalidOperationException`
  / "could not be translated") at test time. Report which query. Do NOT fall back
  to loading all rows in memory (that defeats the plan); the reviewer will decide
  whether to use `FromSql` raw SQL or to keep the current implementation.
- Docker is unavailable so the Testcontainers test cannot run — report that the
  parity test could not be executed; do not mark the plan done on build-only.
- The seed test reveals the SQL result differs from the documented semantics
  (e.g. TotalSpent includes cancelled) — that means the query is wrong; report the
  discrepancy rather than adjusting the test to match a wrong result.

## Maintenance notes

- The `argmax` query (3) uses a correlated subquery; on very large tenants a window
  function (`ROW_NUMBER() OVER (PARTITION BY customer_id ORDER BY scheduled_at DESC)`)
  via raw SQL may be faster — revisit only if EXPLAIN shows it as a bottleneck.
- If the customer export later needs pagination or a date filter, thread it through
  all three queries consistently.
- Reviewer: confirm the parity test actually asserts TotalSpent EXCLUDES cancelled
  bookings and Name comes from the latest booking — the two easiest things to get
  wrong in the SQL rewrite.
