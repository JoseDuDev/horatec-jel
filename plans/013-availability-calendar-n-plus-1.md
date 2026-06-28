# Plan 013: Eliminar o N+1 do calendário/dias de disponibilidade (batch-load + SlotCalculator)

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan in
> `plans/README.md`.
>
> **Drift check (run first)**: `git diff --stat 2a9b001..HEAD -- src/Horafy.Application/Features/Availability/Queries`
> If any Availability query changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P1
- **Effort**: M
- **Risk**: MED
- **Depends on**: none (do this before, or independently of, 014/015/016)
- **Category**: perf
- **Planned at**: commit `2a9b001`, 2026-06-28

## Why this matters

`GetAvailabilityCalendarQuery` (month calendar) and `GetAvailableDaysQuery`
(date-range) each loop **day by day** and `await sender.Send(new GetAvailableSlotsQuery(...))`
for every day. Each `GetAvailableSlotsQuery` issues ~4–5 DB round-trips
(`GetRuleAsync`, `IsBlackoutAsync`, `GetExceptionAsync`, optional service lookup,
`GetByResourceAsync`). A 31-day month therefore costs **~124–155 sequential
queries** for a single calendar render — and this is on the public/anonymous
booking path (`GET /api/v1/availability/resources/{id}/availability-calendar`).

The fix batch-loads the inputs for the whole range **once** (rules for the
resource, exceptions in range, blackout dates, bookings in range, service
duration), then computes each day's slots in memory via a new pure
`SlotCalculator`. The same calculator is extracted from the existing single-day
handler so behavior stays identical (its current tests are the safety net).
Result: a month render goes from ~150 queries to ~4.

## Current state

- `src/Horafy.Application/Features/Availability/Queries/GetAvailableSlotsQuery.cs`
  — the single-day handler whose slot math we extract. Full current body:

```csharp
        // 1. Regra semanal do recurso
        var rule = await availabilityRepository.GetRuleAsync(
            request.ResourceId, request.Date.DayOfWeek, cancellationToken);
        if (rule is null)
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        // 2. Bloqueio global do tenant (fecha todos os recursos na data)
        if (await availabilityRepository.IsBlackoutAsync(request.Date, cancellationToken))
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        // 3. Verificar exceção para a data
        var exception = await availabilityRepository.GetExceptionAsync(
            request.ResourceId, request.Date, cancellationToken);
        if (exception?.IsBlocked is true)
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        var windowStart = exception?.CustomStart ?? rule.StartTime;
        var windowEnd   = exception?.CustomEnd   ?? rule.EndTime;

        int slotDuration = rule.SlotDurationMinutes;
        if (request.ServiceId.HasValue)
        {
            var service = await serviceRepository.GetByIdAsync(request.ServiceId.Value, cancellationToken);
            if (service is not null)
                slotDuration = service.DurationMinutes;
        }

        var step     = slotDuration + rule.BreakAfterMinutes;
        var allSlots = new List<DateTimeOffset>();
        var current  = windowStart;
        while (current.Add(TimeSpan.FromMinutes(slotDuration)) <= windowEnd)
        {
            var slotStart = new DateTimeOffset(request.Date.ToDateTime(current, DateTimeKind.Utc));
            allSlots.Add(slotStart);
            current = current.Add(TimeSpan.FromMinutes(step));
        }
        if (allSlots.Count == 0)
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        var dayStart = new DateTimeOffset(request.Date.ToDateTime(windowStart, DateTimeKind.Utc));
        var dayEnd   = new DateTimeOffset(request.Date.ToDateTime(windowEnd,   DateTimeKind.Utc));
        var existingBookings = await bookingRepository.GetByResourceAsync(
            request.ResourceId, dayStart, dayEnd, cancellationToken);

        var now = dateTimeProvider.UtcNow;
        var availableSlots = allSlots
            .Where(slot => slot > now)
            .Where(slot => !existingBookings.Any(b =>
                b.OverlapsWith(slot, slot.AddMinutes(slotDuration))))
            .ToList();

        return Result.Success<IReadOnlyList<DateTimeOffset>>(availableSlots);
```

  Constructor deps: `IAvailabilityRepository availabilityRepository, IServiceRepository serviceRepository, IBookingRepository bookingRepository, IDateTimeProvider dateTimeProvider`.

- `src/Horafy.Application/Features/Availability/Queries/GetAvailabilityCalendarQuery.cs`
  — currently injects `ISender` and loops `sender.Send` per day (lines 24-42).
  Returns `Result<IReadOnlyList<DayAvailability>>`; `DayAvailability(DateOnly Date, int AvailableSlotCount)`.

- `src/Horafy.Application/Features/Availability/Queries/GetAvailableDaysQuery.cs`
  — currently injects `ISender` and loops `sender.Send` per day (lines 33-53).
  Returns `Result<IReadOnlyList<DateOnly>>` (days with ≥1 free slot). Range is
  validated to ≤31 days.

- Repository methods available (already implemented — verify signatures in
  `src/Horafy.Domain/Interfaces/Repositories/IAvailabilityRepository.cs` and
  `IBookingRepository.cs`):
  - `IAvailabilityRepository.GetRulesByResourceAsync(Guid resourceId, CT)` → `IReadOnlyList<AvailabilityRule>` (one per DayOfWeek).
  - `IAvailabilityRepository.GetExceptionsByResourceAsync(Guid resourceId, DateOnly from, DateOnly to, CT)` → `IReadOnlyList<AvailabilityException>`.
  - `IAvailabilityRepository.GetBlackoutDatesAsync(int? year, CT)` → `IReadOnlyList<TenantBlackoutDate>` (has `.Date`).
  - `IBookingRepository.GetByResourceAsync(Guid resourceId, DateTimeOffset from, DateTimeOffset to, CT)` → `IReadOnlyList<Booking>`.
  - `IServiceRepository.GetByIdAsync(Guid, CT)` → `Service?` (has `.DurationMinutes`).
  - `Booking.ScheduledAt` (DateTimeOffset), `Booking.OverlapsWith(DateTimeOffset start, DateTimeOffset end)`.
  - `AvailabilityRule`: `DayOfWeek, StartTime, EndTime, SlotDurationMinutes, BreakAfterMinutes`.
  - `AvailabilityException`: `CustomStart, CustomEnd, IsBlocked`.

- Convention: handlers are auto-registered by MediatR assembly scan — changing a
  handler's constructor dependencies needs **no** DI edit (all deps above are
  already registered in `src/Horafy.Infrastructure/DependencyInjection.cs`).
- Existing tests to keep green: `tests/Horafy.Application.Tests/Availability/GetAvailableSlotsQueryHandlerTests.cs`
  (uses a mocked `IDateTimeProvider` clock; mocks repo methods with `It.IsAny`).

## Commands you will need

| Purpose | Command | Expected |
|---------|---------|----------|
| Build   | `dotnet build Horafy.sln -c Debug --nologo` | `0 Erro(s)` |
| Focused | `dotnet test tests/Horafy.Application.Tests --nologo -v q --filter "FullyQualifiedName~Availability"` | all pass |
| Full    | `dotnet test Horafy.sln --nologo -v q` | all pass |

## Scope

**In scope**:
- `src/Horafy.Application/Features/Availability/SlotCalculator.cs` (create)
- `src/Horafy.Application/Features/Availability/Queries/GetAvailableSlotsQuery.cs` (refactor to use calculator)
- `src/Horafy.Application/Features/Availability/Queries/GetAvailabilityCalendarQuery.cs` (rewrite handler)
- `src/Horafy.Application/Features/Availability/Queries/GetAvailableDaysQuery.cs` (rewrite handler)
- `tests/Horafy.Application.Tests/Availability/GetAvailabilityCalendarQueryHandlerTests.cs` (create)
- `tests/Horafy.Application.Tests/Availability/GetAvailableDaysQueryHandlerTests.cs` (create)

**Out of scope** (do NOT touch):
- The query/record/validator definitions and their public shapes
  (`GetAvailabilityCalendarQuery`, `DayAvailability`, `GetAvailableDaysQuery`) —
  only the handler classes change.
- `GetAvailableSlotsQuery`'s public record and result type — only the handler
  body changes; its output must stay byte-identical (the existing tests enforce this).
- Repository implementations and any DI registration.

## Git workflow

- Branch: `advisor/013-availability-calendar-n-plus-1`
- Conventional commits, e.g. `perf(availability): batch-load do calendário (remove N+1 por dia)`.
- Commit per step is fine. Do NOT push or open a PR unless instructed.

## Steps

### Step 1: Create the pure SlotCalculator

Create `src/Horafy.Application/Features/Availability/SlotCalculator.cs`. It must
reproduce the existing slot math exactly (guard checks + window generation +
past/overlap filtering), taking pre-loaded inputs:

```csharp
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Entities.Bookings;

namespace Horafy.Application.Features.Availability;

/// <summary>
/// Cálculo puro (sem I/O) dos horários livres de um recurso num dia, a partir de
/// dados já carregados. Compartilhado entre a query de slots de um dia e as
/// queries de calendário/intervalo (que carregam tudo em lote).
/// </summary>
public static class SlotCalculator
{
    public static IReadOnlyList<DateTimeOffset> ComputeAvailableSlots(
        DateOnly                date,
        AvailabilityRule?       rule,
        bool                    isBlackout,
        AvailabilityException?  exception,
        int?                    serviceDurationMinutes,
        IReadOnlyList<Booking>  dayBookings,
        DateTimeOffset          now)
    {
        if (rule is null) return Array.Empty<DateTimeOffset>();
        if (isBlackout) return Array.Empty<DateTimeOffset>();
        if (exception?.IsBlocked is true) return Array.Empty<DateTimeOffset>();

        var windowStart = exception?.CustomStart ?? rule.StartTime;
        var windowEnd   = exception?.CustomEnd   ?? rule.EndTime;

        var slotDuration = serviceDurationMinutes ?? rule.SlotDurationMinutes;
        var step         = slotDuration + rule.BreakAfterMinutes;

        var allSlots = new List<DateTimeOffset>();
        var current  = windowStart;
        while (current.Add(TimeSpan.FromMinutes(slotDuration)) <= windowEnd)
        {
            allSlots.Add(new DateTimeOffset(date.ToDateTime(current, DateTimeKind.Utc)));
            current = current.Add(TimeSpan.FromMinutes(step));
        }
        if (allSlots.Count == 0) return Array.Empty<DateTimeOffset>();

        return allSlots
            .Where(slot => slot > now)
            .Where(slot => !dayBookings.Any(b =>
                b.OverlapsWith(slot, slot.AddMinutes(slotDuration))))
            .ToList();
    }
}
```

**Verify**: `dotnet build Horafy.sln -c Debug --nologo` → `0 Erro(s)`.

### Step 2: Refactor the single-day handler to delegate to SlotCalculator

In `GetAvailableSlotsQuery.cs`, keep the early returns (so the existing tests'
mock setup remains valid) but replace the window/booking math with the calculator.
The handler body becomes:

```csharp
        var rule = await availabilityRepository.GetRuleAsync(
            request.ResourceId, request.Date.DayOfWeek, cancellationToken);
        if (rule is null)
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        if (await availabilityRepository.IsBlackoutAsync(request.Date, cancellationToken))
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        var exception = await availabilityRepository.GetExceptionAsync(
            request.ResourceId, request.Date, cancellationToken);
        if (exception?.IsBlocked is true)
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        int? serviceDuration = null;
        if (request.ServiceId.HasValue)
        {
            var service = await serviceRepository.GetByIdAsync(request.ServiceId.Value, cancellationToken);
            serviceDuration = service?.DurationMinutes;
        }

        var dayStart = new DateTimeOffset(request.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var dayEnd   = new DateTimeOffset(request.Date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var bookings = await bookingRepository.GetByResourceAsync(
            request.ResourceId, dayStart, dayEnd, cancellationToken);

        var slots = SlotCalculator.ComputeAvailableSlots(
            request.Date, rule, isBlackout: false, exception, serviceDuration,
            bookings, dateTimeProvider.UtcNow);

        return Result.Success(slots);
```

Keep the handler's constructor and `using`s; add `using Horafy.Application.Features.Availability;` only if the namespace differs (the calculator is in `Horafy.Application.Features.Availability`, the query is in `Horafy.Application.Features.Availability.Queries`, so add the using).

**Verify**: `dotnet test tests/Horafy.Application.Tests --nologo -v q --filter "FullyQualifiedName~GetAvailableSlotsQueryHandlerTests"` → ALL existing slot tests pass unchanged. If any fail, the extraction changed behavior — STOP.

### Step 3: Rewrite GetAvailabilityCalendarQueryHandler to batch-load

Replace the handler class (keep the record/validator) with one that injects the
four repositories and computes in memory:

```csharp
internal sealed class GetAvailabilityCalendarQueryHandler(
    IAvailabilityRepository availabilityRepository,
    IServiceRepository serviceRepository,
    IBookingRepository bookingRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAvailabilityCalendarQuery, Result<IReadOnlyList<DayAvailability>>>
{
    public async Task<Result<IReadOnlyList<DayAvailability>>> Handle(
        GetAvailabilityCalendarQuery request, CancellationToken ct)
    {
        var daysInMonth = DateTime.DaysInMonth(request.Year, request.Month);
        var from = new DateOnly(request.Year, request.Month, 1);
        var to   = new DateOnly(request.Year, request.Month, daysInMonth);

        var rules = (await availabilityRepository.GetRulesByResourceAsync(request.ResourceId, ct))
            .GroupBy(r => r.DayOfWeek).ToDictionary(g => g.Key, g => g.First());
        var exceptions = (await availabilityRepository.GetExceptionsByResourceAsync(request.ResourceId, from, to, ct))
            .GroupBy(e => e.Date).ToDictionary(g => g.Key, g => g.First());
        var blackouts = (await availabilityRepository.GetBlackoutDatesAsync(request.Year, ct))
            .Select(b => b.Date).ToHashSet();

        int? serviceDuration = null;
        if (request.ServiceId.HasValue)
        {
            var service = await serviceRepository.GetByIdAsync(request.ServiceId.Value, ct);
            serviceDuration = service?.DurationMinutes;
        }

        var monthStart = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var monthEnd   = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var bookingsByDate = (await bookingRepository.GetByResourceAsync(request.ResourceId, monthStart, monthEnd, ct))
            .GroupBy(b => DateOnly.FromDateTime(b.ScheduledAt.UtcDateTime))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Booking>)g.ToList());

        var now    = dateTimeProvider.UtcNow;
        var result = new List<DayAvailability>(daysInMonth);
        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(request.Year, request.Month, day);
            rules.TryGetValue(date.DayOfWeek, out var rule);
            exceptions.TryGetValue(date, out var exception);
            bookingsByDate.TryGetValue(date, out var dayBookings);

            var slots = SlotCalculator.ComputeAvailableSlots(
                date, rule, blackouts.Contains(date), exception, serviceDuration,
                dayBookings ?? Array.Empty<Booking>(), now);

            result.Add(new DayAvailability(date, slots.Count));
        }

        return Result.Success<IReadOnlyList<DayAvailability>>(result);
    }
}
```

Update the file's `using`s: remove nothing needed by the record; add
`using Horafy.Application.Interfaces;`, `using Horafy.Domain.Entities.Bookings;`,
`using Horafy.Domain.Interfaces.Repositories;`. The `using MediatR;` stays
(needed by `IRequestHandler`); `ISender` is no longer used.

**Verify**: `dotnet build Horafy.sln -c Debug --nologo` → `0 Erro(s)`.

### Step 4: Rewrite GetAvailableDaysQueryHandler to batch-load

Same pattern; the range may cross a year boundary (≤31 days), so union blackouts
across each year in `[From.Year, To.Year]`:

```csharp
internal sealed class GetAvailableDaysQueryHandler(
    IAvailabilityRepository availabilityRepository,
    IServiceRepository serviceRepository,
    IBookingRepository bookingRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAvailableDaysQuery, Result<IReadOnlyList<DateOnly>>>
{
    public async Task<Result<IReadOnlyList<DateOnly>>> Handle(
        GetAvailableDaysQuery request, CancellationToken ct)
    {
        var rules = (await availabilityRepository.GetRulesByResourceAsync(request.ResourceId, ct))
            .GroupBy(r => r.DayOfWeek).ToDictionary(g => g.Key, g => g.First());
        var exceptions = (await availabilityRepository.GetExceptionsByResourceAsync(request.ResourceId, request.From, request.To, ct))
            .GroupBy(e => e.Date).ToDictionary(g => g.Key, g => g.First());

        var blackouts = new HashSet<DateOnly>();
        for (var year = request.From.Year; year <= request.To.Year; year++)
            foreach (var b in await availabilityRepository.GetBlackoutDatesAsync(year, ct))
                blackouts.Add(b.Date);

        int? serviceDuration = null;
        if (request.ServiceId.HasValue)
        {
            var service = await serviceRepository.GetByIdAsync(request.ServiceId.Value, ct);
            serviceDuration = service?.DurationMinutes;
        }

        var rangeStart = new DateTimeOffset(request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var rangeEnd   = new DateTimeOffset(request.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var bookingsByDate = (await bookingRepository.GetByResourceAsync(request.ResourceId, rangeStart, rangeEnd, ct))
            .GroupBy(b => DateOnly.FromDateTime(b.ScheduledAt.UtcDateTime))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Booking>)g.ToList());

        var now  = dateTimeProvider.UtcNow;
        var days = new List<DateOnly>();
        for (var date = request.From; date <= request.To; date = date.AddDays(1))
        {
            rules.TryGetValue(date.DayOfWeek, out var rule);
            exceptions.TryGetValue(date, out var exception);
            bookingsByDate.TryGetValue(date, out var dayBookings);

            var slots = SlotCalculator.ComputeAvailableSlots(
                date, rule, blackouts.Contains(date), exception, serviceDuration,
                dayBookings ?? Array.Empty<Booking>(), now);

            if (slots.Count > 0) days.Add(date);
        }

        return Result.Success<IReadOnlyList<DateOnly>>(days);
    }
}
```

Update `using`s as in Step 3.

**Verify**: `dotnet build Horafy.sln -c Debug --nologo` → `0 Erro(s)`.

### Step 5: Add handler tests for calendar and days

Create the two test files, modeled on
`tests/Horafy.Application.Tests/Availability/GetAvailableSlotsQueryHandlerTests.cs`
(same mocks: `IAvailabilityRepository`, `IServiceRepository`, `IBookingRepository`,
`IDateTimeProvider` with a fixed clock). Cover at minimum:

For `GetAvailabilityCalendarQueryHandlerTests`:
- A month where the resource has a rule on, say, Mondays only → only Mondays have
  `AvailableSlotCount > 0`; result length == days in month.
- A blackout date in the month → that date has `AvailableSlotCount == 0` even
  though its weekday has a rule.

For `GetAvailableDaysQueryHandlerTests`:
- A 7-day range with a rule on specific weekdays → only those weekdays returned.
- A day with an exception `IsBlocked` → excluded.

Use a fixed clock far in the past (e.g. `new DateTimeOffset(2020,1,1,0,0,0,TimeSpan.Zero)`)
so "slot in the future" filtering never removes the generated slots, OR set the
test dates in the future relative to the fixed clock — be explicit so slots are
not filtered out as past. Mock `GetRulesByResourceAsync`, `GetExceptionsByResourceAsync`,
`GetBlackoutDatesAsync`, `GetByResourceAsync` to return the prepared lists, and
`IsBlackoutAsync` is NOT used by these handlers (they use the blackout set).

**Verify**: `dotnet test tests/Horafy.Application.Tests --nologo -v q --filter "FullyQualifiedName~Availability"` → all pass.

## Test plan

- Keep `GetAvailableSlotsQueryHandlerTests` green (proves the SlotCalculator
  extraction is behavior-preserving) — this is the core safety net.
- New `GetAvailabilityCalendarQueryHandlerTests`: rule-only-some-weekdays, and
  blackout-date-zeroes-a-day.
- New `GetAvailableDaysQueryHandlerTests`: rule-filters-days, and
  blocked-exception-excludes-a-day.
- Pattern source: `GetAvailableSlotsQueryHandlerTests.cs` (mock + fixed clock).

## Done criteria

ALL must hold:

- [ ] `dotnet build Horafy.sln -c Debug --nologo` exits `0 Erro(s)`
- [ ] `dotnet test Horafy.sln --nologo -v q` — all pass, incl. the 2 new test classes
- [ ] `grep -n "ISender" src/Horafy.Application/Features/Availability/Queries/GetAvailabilityCalendarQuery.cs src/Horafy.Application/Features/Availability/Queries/GetAvailableDaysQuery.cs` returns nothing (no more per-day MediatR dispatch)
- [ ] `GetAvailableSlotsQueryHandlerTests` still passes with zero edits to that test file
- [ ] No files outside the in-scope list modified (`git status`)
- [ ] `plans/README.md` status row for 013 updated

## STOP conditions

Stop and report back if:

- Any excerpt in "Current state" does not match the live code (drift).
- After Step 2, any existing `GetAvailableSlotsQueryHandlerTests` test fails —
  the extraction changed behavior; do not edit the test to make it pass.
- A referenced repository method has a different signature than documented
  (e.g. `GetExceptionsByResourceAsync` takes different params) — STOP and report.
- You discover bookings are loaded by a field other than `ScheduledAt` such that
  grouping by `ScheduledAt` date would miss bookings the per-day version included.

## Maintenance notes

- Cross-midnight bookings: both the old per-day code and this version key
  bookings by `ScheduledAt`'s date; a booking starting the previous day that
  overlaps into early slots is not considered. This preserves prior behavior — if
  it ever needs fixing, widen the booking load by one day on each side and pass
  the union to `SlotCalculator` for boundary days.
- If pagination or a longer range is ever added to `GetAvailableDaysQuery`
  (currently capped at 31 days), the single batched `GetByResourceAsync` load may
  need chunking.
- Reviewer: confirm `GetAvailableSlotsQueryHandlerTests` is unchanged and green —
  it is the proof the shared `SlotCalculator` matches the original math.
