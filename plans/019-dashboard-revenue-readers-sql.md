# Plan 019: Agregar DashboardReader e RevenueReportReader em SQL

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving on. If any
> STOP condition occurs, stop and report. When done, update the status row for
> this plan in `plans/README.md` — unless a reviewer dispatched you and told you
> they maintain the index.
>
> **Drift check (run first)**: `git diff --stat 74ded3f..HEAD -- src/Horafy.Infrastructure/Repositories/DashboardReader.cs src/Horafy.Infrastructure/Repositories/RevenueReportReader.cs`
> If either in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P3 (lowest of the deferred set — do 017/018 first)
- **Effort**: M
- **Risk**: MED
- **Depends on**: none (but read Plan 018 first — it establishes the same SQL-aggregation pattern and the Testcontainers harness)
- **Category**: perf
- **Planned at**: commit `74ded3f`, 2026-06-28

## Why this matters

`DashboardReader` and `RevenueReportReader` both `.Include(b => b.Services).ToListAsync()`
over a date range, then aggregate in memory. Unlike `CustomerListReader` (Plan 018),
these ARE bounded by a date range, so the load is not unbounded — but a wide window
(6–12 months) on a busy tenant still materializes thousands of booking rows × their
service rows just to produce a handful of summary numbers. Pushing the counts, sums,
and top-N into SQL reduces that to small result sets. This is the **lowest-priority**
deferred item; only worth doing if dashboards/reports get slow, or alongside 018 to
retire the load-then-group pattern entirely.

## Current state

### DashboardReader
`src/Horafy.Infrastructure/Repositories/DashboardReader.cs` — loads appointment
bookings in `[from,to]` with services, then computes in memory:

```csharp
        var bookings = await context.Set<Booking>()
            .AsNoTracking()
            .Include(b => b.Services)
            .Where(b => b.Kind == BookingKind.Appointment
                     && b.ScheduledAt >= fromDt
                     && b.ScheduledAt <= toDt)
            .ToListAsync(ct);

        var total     = bookings.Count;
        var confirmed = bookings.Count(b => b.Status == BookingStatus.Confirmed);
        var cancelled = bookings.Count(b => b.Status == BookingStatus.Cancelled);
        var noShow    = bookings.Count(b => b.Status == BookingStatus.NoShow);
        var cancelRate = total > 0 ? Math.Round((decimal)cancelled / total * 100, 1) : 0m;

        var revenue = bookings
            .Where(b => b.Status is not BookingStatus.Cancelled and not BookingStatus.NoShow)
            .Sum(b => b.TotalAmount);

        IReadOnlyList<ServiceStatItem> topServices = bookings
            .Where(b => b.Status != BookingStatus.Cancelled)
            .SelectMany(b => b.Services, (_, s) => new { s.ServiceName, s.Price })
            .GroupBy(x => x.ServiceName)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new ServiceStatItem(g.Key, g.Count(), g.Sum(x => x.Price)))
            .ToList();

        IReadOnlyList<ResourceStatItem> topResources = bookings
            .Where(b => b.Status != BookingStatus.Cancelled && !string.IsNullOrEmpty(b.ResourceName))
            .GroupBy(b => b.ResourceName)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new ResourceStatItem(g.Key, g.Count()))
            .ToList();

        IReadOnlyList<DailyBookingItem> byDay = bookings
            .GroupBy(b => DateOnly.FromDateTime(b.ScheduledAt.UtcDateTime))
            .OrderBy(g => g.Key)
            .Select(g => new DailyBookingItem(g.Key, g.Count()))
            .ToList();
```

### RevenueReportReader
`src/Horafy.Infrastructure/Repositories/RevenueReportReader.cs` — loads approved
`Payment`s in `[from,to]` (this part is already a bounded scalar/group aggregation
candidate), then loads the matching bookings with services for the per-service
breakdown:

```csharp
        var payments = await context.Set<Payment>()
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Approved && p.CreatedAt >= fromDt && p.CreatedAt <= toDt)
            .ToListAsync(ct);

        var totalRevenue  = payments.Sum(p => p.Amount);
        var paymentCount  = payments.Count;
        IReadOnlyList<DailyRevenueItem> byDay = payments
            .GroupBy(p => DateOnly.FromDateTime(p.CreatedAt.UtcDateTime))
            .OrderBy(g => g.Key)
            .Select(g => new DailyRevenueItem(g.Key, g.Sum(p => p.Amount), g.Count()))
            .ToList();

        var bookingIds = payments.Select(p => p.BookingId).Distinct().ToList();
        IReadOnlyList<ServiceRevenueItem> byService = [];
        if (bookingIds.Count > 0)
        {
            var bookings = await context.Set<Booking>()
                .AsNoTracking().Include(b => b.Services)
                .Where(b => bookingIds.Contains(b.Id))
                .ToListAsync(ct);
            byService = bookings
                .SelectMany(b => b.Services, (_, s) => new { s.ServiceName, s.Price })
                .GroupBy(x => x.ServiceName)
                .OrderByDescending(g => g.Sum(x => x.Price))
                .Select(g => new ServiceRevenueItem(g.Key, g.Count(), g.Sum(x => x.Price)))
                .ToList();
        }
        return new RevenueReport(from, to, totalRevenue, paymentCount, byService, byDay);
```

- **Key constraint (same as Plan 018)**: `Booking.TotalAmount` is computed, not a
  column — sum `BookingService.Price` over the `Services` navigation in SQL instead.
- Result records: `DashboardStats(...)`, `ServiceStatItem(string ServiceName, int BookingCount, decimal Revenue)`,
  `ResourceStatItem(string ResourceName, int BookingCount)`, `DailyBookingItem(DateOnly Date, int Count)`
  in `src/Horafy.Application/Interfaces/IDashboardReader.cs`; `RevenueReport(...)`,
  `ServiceRevenueItem`, `DailyRevenueItem` in `IRevenueReportReader.cs`. Output
  contracts are FROZEN — only the query implementation changes.
- **Semantics to preserve**: revenue/topServices exclude cancelled (and revenue also
  excludes NoShow); topServices/topResources are the top 5 by booking count;
  `cancelRate` rounding; `byDay`/`byService` ordering exactly as above.
- Test infra: Testcontainers Postgres — see
  `tests/Horafy.Application.Tests/Rentals/RentalStockConcurrencyTests.cs` (the
  `PostgreSqlBuilder` + `TenantSchemaService.CreateSchemaAsync` + `TenantDbContext`
  with `SearchPath` wiring). The EF InMemory provider canNOT validate this SQL.

## Commands you will need

| Purpose | Command | Expected |
|---------|---------|----------|
| Build | `dotnet build Horafy.sln -c Debug --nologo` | `0 Erro(s)` |
| Test (this) | `dotnet test tests/Horafy.Application.Tests --nologo -v q --filter "FullyQualifiedName~ReaderSqlTests"` | all pass (Docker) |
| Full | `dotnet test Horafy.sln --nologo -v q` | all pass |

## Scope

**In scope**:
- `src/Horafy.Infrastructure/Repositories/DashboardReader.cs`
- `src/Horafy.Infrastructure/Repositories/RevenueReportReader.cs`
- `tests/Horafy.Application.Tests/Reports/DashboardReaderSqlTests.cs` (create)
- `tests/Horafy.Application.Tests/Reports/RevenueReportReaderSqlTests.cs` (create)

**Out of scope**: the reader interfaces/records; `CustomerListReader` (Plan 018);
controllers/handlers.

## Git workflow

- Branch: `advisor/019-dashboard-revenue-readers-sql`
- Conventional commits, e.g. `perf(reports): agrega dashboard e revenue em SQL`.
- Do NOT push or open a PR.

## Steps

### Step 1: DashboardReader — push aggregation to SQL

Replace the single load + in-memory aggregation with targeted SQL queries over the
same `[fromDt, toDt]` appointment predicate:
- Status counts (`total/confirmed/cancelled/noShow`): one grouped query
  `GroupBy(b => b.Status).Select(g => new { g.Key, Count = g.Count() })` then map in
  memory (≤ a handful of rows); compute `cancelRate` from those.
- `revenue`: `Where(status not Cancelled and not NoShow).SelectMany(b => b.Services, (b,s) => s.Price).Sum()`.
- `topServices`: `Where(status != Cancelled).SelectMany(b => b.Services, (b,s) => new { s.ServiceName, s.Price }).GroupBy(x => x.ServiceName).Select(g => new { g.Key, Count = g.Count(), Revenue = g.Sum(x => x.Price) }).OrderByDescending(x => x.Count).Take(5)`.
- `topResources`: `Where(status != Cancelled && ResourceName != "").GroupBy(b => b.ResourceName).Select(g => new { g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).Take(5)`.
- `byDay`: `GroupBy(b => b.ScheduledAt.Date ...).Select(count)` — Postgres can group by
  the date of a timestamptz; if EF won't translate `DateOnly.FromDateTime(...)`, group by
  a date expression it accepts (e.g. project the day in SQL) — if neither translates,
  see STOP conditions.

Keep all `[fromDt,toDt]` + `Kind == Appointment` filters identical to today.

**Verify**: `dotnet build Horafy.sln -c Debug --nologo` → `0 Erro(s)`. Translation is
proven by Step 3's test, not the build.

### Step 2: RevenueReportReader — push aggregation to SQL

- Total/count/byDay: aggregate directly over `Payment` (approved, in range) in SQL
  (`Sum(Amount)`, `Count()`, and a per-day group) instead of loading all payments.
- byService: join payments → bookings → services in SQL and group by `ServiceName`.
  A correct equivalent: select booking services where the booking's id is among the
  approved-payment booking ids AND group/sum in SQL. Preserve the `OrderByDescending(Sum(Price))`
  ordering and the `(ServiceName, Count, Revenue)` shape.

**Verify**: `dotnet build Horafy.sln -c Debug --nologo` → `0 Erro(s)`.

### Step 3: Testcontainers parity tests for both readers

Create the two test files modeled on `RentalStockConcurrencyTests` (same
`IAsyncLifetime` + Postgres container + schema provisioning + `TenantDbContext`).
Seed a small, known dataset spanning a few days with a mix of statuses (Confirmed,
Cancelled, NoShow) and 2–3 services per booking, plus approved/non-approved payments.
Assert each reader returns the documented numbers (status counts, revenue with the
right exclusions, top-N ordering, byDay/byService). The seed is the oracle — pick
values where the expected aggregates are obvious.

**Verify**: `dotnet test tests/Horafy.Application.Tests --nologo -v q --filter "FullyQualifiedName~ReaderSqlTests"` → all pass.

## Test plan

- `DashboardReaderSqlTests` + `RevenueReportReaderSqlTests`: seed-and-assert parity
  for each aggregate, covering the cancelled/NoShow exclusions and top-5 ordering.
- Pattern source: `RentalStockConcurrencyTests.cs`.

## Done criteria

ALL must hold:

- [ ] `dotnet build Horafy.sln -c Debug --nologo` exits `0 Erro(s)`
- [ ] Neither reader calls `.Include(b => b.Services)` to materialize all bookings before aggregating (`grep -n "Include(b => b.Services)" src/Horafy.Infrastructure/Repositories/DashboardReader.cs src/Horafy.Infrastructure/Repositories/RevenueReportReader.cs` returns nothing)
- [ ] `dotnet test Horafy.sln --nologo -v q` — all pass, incl. the two new test classes
- [ ] Only in-scope files changed (`git status`)
- [ ] `plans/README.md` status row updated (unless your reviewer maintains it)

## STOP conditions

Stop and report back if:

- Any aggregation throws an EF translation error (report which one). Do NOT revert to
  loading all rows in memory — the reviewer decides on raw SQL vs. keeping current.
- `DateOnly.FromDateTime(b.ScheduledAt.UtcDateTime)` (or the `byDay` grouping) cannot
  be translated and there is no clean SQL day-grouping equivalent — report it; the
  per-day grouping may need a raw-SQL date_trunc.
- Docker is unavailable so parity tests cannot run — report; do not mark done on
  build-only.
- A parity assertion fails because the SQL result differs from the documented
  semantics — report the discrepancy; do not adjust the expected values to match a
  wrong query.

## Maintenance notes

- This is the lowest-value of the three deferred perf items (these readers are
  date-bounded). If only one of the two readers translates cleanly, ship that one and
  re-defer the other rather than forcing raw SQL under time pressure.
- The status-counts query and the revenue/top-N queries each hit the bookings table;
  if that becomes the cost, a single SQL view or a `date_trunc` grouped query could
  serve multiple metrics — out of scope here.
- Reviewer: the easiest regressions to miss are the NoShow exclusion in `revenue`
  (topServices excludes only Cancelled, revenue excludes Cancelled AND NoShow) and
  the top-5 truncation.
