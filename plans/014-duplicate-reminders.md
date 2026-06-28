# Plan 014: Eliminar lembretes duplicados (janelas alinhadas à execução horária)

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan in
> `plans/README.md`.
>
> **Drift check (run first)**: `git diff --stat 2a9b001..HEAD -- src/Horafy.Infrastructure/Messaging/Jobs/BookingReminderJob.cs`
> If the file changed since this plan was written, compare the "Current state"
> excerpts against the live code before proceeding; on a mismatch, treat it as
> a STOP condition.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: bug
- **Planned at**: commit `2a9b001`, 2026-06-28

## Why this matters

`BookingReminderJob` runs hourly (Quartz cron `0 0 * * * ?`, registered in
`src/Horafy.Infrastructure/DependencyInjection.cs`). The appointment reminder
windows are **wider than one hour** — the first reminder uses
`[now+(H-2), now+(H+2)]` (4h wide) and the second `[now+(H-1), now+(H+1)]`
(2h wide). A booking whose `ScheduledAt` sits inside that window matches on
**multiple consecutive hourly runs**, so the customer receives the same
WhatsApp/email reminder 3–4 times. The rental return/overdue windows have the
same shape with inclusive bounds at both ends (boundary double-fire). There is
**no persisted "reminder already sent" flag** on `Booking` to deduplicate.

The fix makes every reminder window exactly **one hour wide and half-open**
(`[now+H, now+H+1)`), equal to the job's period — so each booking falls into
exactly one hourly run and is reminded once.

## Current state

- `src/Horafy.Infrastructure/Messaging/Jobs/BookingReminderJob.cs` — the
  appointment reminder block (inside the `if (reminders.Enabled)` body):

```csharp
                if (reminders.FirstReminderHours > 0)
                {
                    var min = now.AddHours(reminders.FirstReminderHours - 2);
                    var max = now.AddHours(reminders.FirstReminderHours + 2);
                    var first = await bookingRepo.FindAsync(
                        b => b.Status == BookingStatus.Confirmed &&
                             b.ScheduledAt >= min && b.ScheduledAt <= max, ct);
                    pending.AddRange(first.Select(b => (b, true)));
                }

                if (reminders.SecondReminderHours > 0)
                {
                    var min = now.AddHours(reminders.SecondReminderHours - 1);
                    var max = now.AddHours(reminders.SecondReminderHours + 1);
                    var second = await bookingRepo.FindAsync(
                        b => b.Status == BookingStatus.Confirmed &&
                             b.ScheduledAt >= min && b.ScheduledAt <= max, ct);
                    pending.AddRange(second.Select(b => (b, false)));
                }
```

- The rental reminder windows further down the same method:

```csharp
            var returnMin  = now.AddHours(23);
            var returnMax  = now.AddHours(24);
            var overdueMin = now.AddHours(-24);
            var overdueMax = now.AddHours(-23);

            var returnReminders = await bookingRepo.FindAsync(
                b => b.Kind == BookingKind.Rental
                  && b.RentalStatus == RentalLifecycle.PickedUp
                  && b.EndsAt >= returnMin && b.EndsAt <= returnMax, ct);
            ...
            var overdueRentals = await bookingRepo.FindAsync(
                b => b.Kind == BookingKind.Rental
                  && b.RentalStatus == RentalLifecycle.PickedUp
                  && b.EndsAt >= overdueMin && b.EndsAt <= overdueMax, ct);
```

- The job runs hourly. `ExecuteAsync(DateTimeOffset now, CancellationToken ct)`
  takes `now` as a parameter (tests call it with a fixed value).
- `Booking` has **no** `ReminderSentAt`/`FirstReminderSentAt` field
  (`src/Horafy.Domain/Entities/Bookings/Booking.cs`) — confirmed; do not assume one.

## Commands you will need

| Purpose | Command | Expected |
|---------|---------|----------|
| Build   | `dotnet build Horafy.sln -c Debug --nologo` | `0 Erro(s)` |
| Focused | `dotnet test tests/Horafy.Infrastructure.Tests --nologo -v q --filter "FullyQualifiedName~BookingReminderJobTests"` | all pass |
| Full    | `dotnet test Horafy.sln --nologo -v q` | all pass |

## Scope

**In scope**:
- `src/Horafy.Infrastructure/Messaging/Jobs/BookingReminderJob.cs`
- `tests/Horafy.Infrastructure.Tests/Notifications/BookingReminderJobTests.cs`

**Out of scope** (do NOT touch):
- `ReminderSettings` value object and its config/migration — the configured
  hours stay as-is; only the window math around them changes.
- The reminder message/consumer classes and templates.
- The Quartz cron registration in `DependencyInjection.cs` — the 1-hour cadence
  is the assumption this fix relies on; changing it would reintroduce the bug.

## Git workflow

- Branch: `advisor/014-duplicate-reminders`
- Conventional commits, e.g. `fix(reminders): janela de 1h alinhada à execução horária (sem duplicar)`.
- Do NOT push or open a PR unless instructed.

## Steps

### Step 1: Make the appointment windows 1h half-open

Replace the two appointment blocks so each window is `[now+H, now+H+1)`:

```csharp
                if (reminders.FirstReminderHours > 0)
                {
                    var min = now.AddHours(reminders.FirstReminderHours);
                    var max = now.AddHours(reminders.FirstReminderHours + 1);
                    var first = await bookingRepo.FindAsync(
                        b => b.Status == BookingStatus.Confirmed &&
                             b.ScheduledAt >= min && b.ScheduledAt < max, ct);
                    pending.AddRange(first.Select(b => (b, true)));
                }

                if (reminders.SecondReminderHours > 0)
                {
                    var min = now.AddHours(reminders.SecondReminderHours);
                    var max = now.AddHours(reminders.SecondReminderHours + 1);
                    var second = await bookingRepo.FindAsync(
                        b => b.Status == BookingStatus.Confirmed &&
                             b.ScheduledAt >= min && b.ScheduledAt < max, ct);
                    pending.AddRange(second.Select(b => (b, false)));
                }
```

(Note the upper bound changed from `<=` to `<`, and the offsets dropped the ±.)

**Verify**: `dotnet build Horafy.sln -c Debug --nologo` → `0 Erro(s)`.

### Step 2: Make the rental windows half-open too

Change the rental return/overdue comparisons from `<=` upper bounds to `<`:

```csharp
            var returnMin  = now.AddHours(23);
            var returnMax  = now.AddHours(24);
            var overdueMin = now.AddHours(-24);
            var overdueMax = now.AddHours(-23);

            var returnReminders = await bookingRepo.FindAsync(
                b => b.Kind == BookingKind.Rental
                  && b.RentalStatus == RentalLifecycle.PickedUp
                  && b.EndsAt >= returnMin && b.EndsAt < returnMax, ct);
            ...
            var overdueRentals = await bookingRepo.FindAsync(
                b => b.Kind == BookingKind.Rental
                  && b.RentalStatus == RentalLifecycle.PickedUp
                  && b.EndsAt >= overdueMin && b.EndsAt < overdueMax, ct);
```

(Only the two `<= returnMax` / `<= overdueMax` become `< returnMax` / `< overdueMax`.
Leave the `>=` lower bounds unchanged.)

**Verify**: `dotnet build Horafy.sln -c Debug --nologo` → `0 Erro(s)`.

### Step 3: Add a no-duplicate regression test

In `BookingReminderJobTests.cs`, add a `[Fact]` that runs the job on two
consecutive hourly ticks and asserts a booking is reminded exactly once. Use the
predicate-applying mock pattern already present in the file (see
`ExecuteAsync_CustomFirstReminderHours_PublishesInCustomWindow`, which compiles
the predicate via `pred.Compile()`):

```csharp
[Fact]
public async Task ExecuteAsync_TwoConsecutiveHourlyRuns_PublishesReminderOnce()
{
    var now     = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
    var booking = MakeConfirmedBookingAt(now.AddHours(24).AddMinutes(30)); // 24.5h away
    var tenant  = Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop);
    tenant.UpdateReminderSettings(enabled: true, firstReminderHours: 24, secondReminderHours: 0);

    var list = new List<Booking> { booking };
    _bookingRepo.Setup(r => r.FindAsync(
        It.IsAny<System.Linq.Expressions.Expression<Func<Booking, bool>>>(), default))
        .ReturnsAsync((System.Linq.Expressions.Expression<Func<Booking, bool>> pred, CancellationToken _) =>
            list.Where(pred.Compile()).ToList());
    _resourceRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
        .ReturnsAsync(Resource.Create("Ana", ResourceType.Professional));
    _tenantRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Tenant> { tenant });

    var job = MakeJob();
    await job.ExecuteAsync(now, default);                 // window [24h,25h) → 24.5h matches
    await job.ExecuteAsync(now.AddHours(1), default);     // window [25h,26h) → 24.5h excluded

    _bus.Verify(b => b.Publish(
        It.Is<BookingReminderMessage>(m => m.BookingId == booking.Id), default),
        Times.Once);
}
```

**Verify**: `dotnet test tests/Horafy.Infrastructure.Tests --nologo -v q --filter "FullyQualifiedName~BookingReminderJobTests"` → all pass (existing tests + this new one).

## Test plan

- New test `ExecuteAsync_TwoConsecutiveHourlyRuns_PublishesReminderOnce`
  (above) is the regression guard: a 24.5h-away booking, run at `now` and
  `now+1h`, must publish exactly once.
- Existing tests must stay green:
  - `ExecuteAsync_BookingIn24Hours_PublishesOneDayBeforeReminder` (blanket mock,
    asserts `Times.AtLeastOnce`) — still passes.
  - `ExecuteAsync_CustomFirstReminderHours_PublishesInCustomWindow` (booking at
    exactly `now+48`, H=48; new window `[48,49)` includes 48 via `>=`) — passes.
  - `ExecuteAsync_RentalDueIn24Hours...` (rental at `now+23.5`; window `[23,24)`
    includes 23.5) and `ExecuteAsync_RentalOneDayOverdue...` (at `now-23.5`;
    window `[-24,-23)` includes -23.5) — pass.
- Pattern source: the same test file.

## Done criteria

ALL must hold:

- [ ] `dotnet build Horafy.sln -c Debug --nologo` exits `0 Erro(s)`
- [ ] `dotnet test Horafy.sln --nologo -v q` — all pass, incl. the new no-duplicate test
- [ ] Every reminder window in `BookingReminderJob.cs` is half-open (`>= min && < max`) and exactly 1h wide
- [ ] No `<=` upper bound remains on any `ScheduledAt`/`EndsAt` window in the file (`grep -n "<= returnMax\|<= overdueMax\|ScheduledAt <= max" src/Horafy.Infrastructure/Messaging/Jobs/BookingReminderJob.cs` returns nothing)
- [ ] No files outside the in-scope list modified (`git status`)
- [ ] `plans/README.md` status row for 014 updated

## STOP conditions

Stop and report back if:

- The window code does not match the "Current state" excerpts (drift).
- Making the windows 1h causes a legitimate existing test to fail in a way that
  reveals the booking should still be reminded (re-read the test's timing before
  weakening anything).
- You find the Quartz cron is NOT hourly (`DependencyInjection.cs` — the trigger
  `WithCronSchedule("0 0 * * * ?")`); a different cadence invalidates the 1h
  window assumption — STOP and report.

## Maintenance notes

- This fix assumes the job runs **exactly hourly and reliably**. If a job run is
  missed (process downtime spanning an hour), that hour's reminders are skipped
  (acceptable for advisory reminders). If at-least-once delivery becomes a
  requirement, replace the time-window dedup with a **persisted flag** on
  `Booking` (`FirstReminderSentAt` / `SecondReminderSentAt` nullable columns +
  a `MarkReminderSent` method + a tenant-schema migration), filter
  `b.FirstReminderSentAt == null` in the query, and persist after publishing.
  That is the more robust design but requires a migration and tracked-entity
  persistence in the job (the repo's `FindAsync` uses `AsNoTracking`).
- If the cron period is ever changed, the window width must change to match it.
- Reviewer: confirm no window still uses `<=` on its upper bound.
