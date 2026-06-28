# Plan 016: Cobrir os ramos não testados de confirmação e reembolso de pagamento

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md`.
>
> **Drift check (run first)**: `git diff --stat 2a9b001..HEAD -- src/Horafy.Application/Features/Payments/Commands`
> If either handler changed since this plan was written, compare the "Current
> state" excerpts against the live code before proceeding; on a mismatch, treat
> it as a STOP condition.

## Status

- **Priority**: P2
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: tests
- **Planned at**: commit `2a9b001`, 2026-06-28

## Why this matters

Payment is a money path. The MercadoPago webhook funnels into
`ConfirmPaymentCommand`, and refunds go through `RefundPaymentCommand`. Both
handlers have branches that are currently **unverified by tests**:
`ConfirmPaymentCommand` only has tests for the `Approved` status (not
`Rejected`/`Cancelled`), and `RefundPaymentCommand` only tests full refunds
(`Amount: null`), never a partial refund amount. A refactor that breaks the
reject path or ignores the partial amount would ship silently. This plan adds
the missing branch tests — pure test additions, no production code change.

## Current state

- `src/Horafy.Application/Features/Payments/Commands/ConfirmPaymentCommand.cs`
  — the relevant branch (lines 27-30):

```csharp
        if (mpStatus.Status == PaymentStatus.Approved)
            payment.Approve(request.MpPaymentId);
        else if (mpStatus.Status is PaymentStatus.Rejected or PaymentStatus.Cancelled)
            payment.Reject(request.MpPaymentId);

        paymentRepository.Update(payment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
```

- `src/Horafy.Application/Features/Payments/Commands/RefundPaymentCommand.cs`
  — partial-refund line (lines 23-24):

```csharp
        var refundAmount = request.Amount ?? payment.Amount;
        var refundResult = await gateway.RefundAsync(payment.MpPaymentId!, refundAmount, cancellationToken);
```

- Existing tests (the pattern to copy exactly):
  - `tests/Horafy.Application.Tests/Payments/ConfirmPaymentCommandHandlerTests.cs`
    — mocks `IPaymentRepository`, `IPaymentGateway`, `ITenantUnitOfWork`. Helper
    `MakePendingPayment(Guid)` → `Payment.Create(bookingId, "pref_123", PaymentMethod.Pix, 100m, 0m)`.
    The gateway returns `new PaymentStatusResult("mp_999", "pref_123", PaymentStatus.Approved, DateTimeOffset.UtcNow)`.
  - `tests/Horafy.Application.Tests/Payments/RefundPaymentCommandHandlerTests.cs`
    — helper `MakeApprovedPayment()` → `Payment.Create(...); p.Approve("mp_1");`.
    Gateway: `_gateway.Setup(g => g.RefundAsync("mp_1", 100m, default)).ReturnsAsync(new RefundResult(true, null));`

- `Payment` exposes `Status` (enum `PaymentStatus`) and `Amount`. `PaymentStatus`
  values include `Approved`, `Rejected`, `Cancelled`, `Refunded`, `Pending`
  (see `src/Horafy.Domain/Entities/Payments/PaymentStatus.cs`). The `Reject`
  path sets `Status` to `Rejected`.

## Commands you will need

| Purpose | Command | Expected |
|---------|---------|----------|
| Build   | `dotnet build Horafy.sln -c Debug --nologo` | `0 Erro(s)` |
| Focused | `dotnet test tests/Horafy.Application.Tests --nologo -v q --filter "FullyQualifiedName~Payments"` | all pass |

## Scope

**In scope** (modify only these test files — NO production code):
- `tests/Horafy.Application.Tests/Payments/ConfirmPaymentCommandHandlerTests.cs`
- `tests/Horafy.Application.Tests/Payments/RefundPaymentCommandHandlerTests.cs`

**Out of scope** (do NOT touch):
- Any file under `src/` — if a test reveals a real bug, STOP and report it; do
  not fix production code in this plan.

## Git workflow

- Branch: `advisor/016-payment-branch-tests`
- Conventional commits, e.g. `test(payments): cobrir rejeição/cancelamento e reembolso parcial`.
- Do NOT push or open a PR unless instructed.

## Steps

### Step 1: Add reject/cancel tests to ConfirmPaymentCommandHandlerTests

Add two `[Fact]`s. First verify the exact transition method name by reading
`src/Horafy.Domain/Entities/Payments/Payment.cs` (`Reject` sets `Status` to
`Rejected`). Pattern (mirror `Handle_PendingPayment_ApprovesAndReturnsSuccess`):

```csharp
[Theory]
[InlineData(PaymentStatus.Rejected)]
[InlineData(PaymentStatus.Cancelled)]
public async Task Handle_RejectedOrCancelled_RejectsPayment(PaymentStatus mpStatus)
{
    var payment = MakePendingPayment(Guid.NewGuid());
    _paymentRepo.Setup(r => r.GetByMpPaymentIdAsync("mp_999", default)).ReturnsAsync((Payment?)null);
    _paymentRepo.Setup(r => r.GetByPreferenceIdAsync("pref_123", default)).ReturnsAsync(payment);
    _gateway.Setup(g => g.GetPaymentStatusAsync("mp_999", default))
        .ReturnsAsync(new PaymentStatusResult("mp_999", "pref_123", mpStatus, DateTimeOffset.UtcNow));

    var result = await MakeHandler().Handle(new ConfirmPaymentCommand("mp_999"), default);

    result.IsSuccess.Should().BeTrue();
    payment.Status.Should().Be(PaymentStatus.Rejected);
    _paymentRepo.Verify(r => r.Update(payment), Times.Once);
    _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
}
```

**Verify**: `dotnet test tests/Horafy.Application.Tests --nologo -v q --filter "FullyQualifiedName~ConfirmPaymentCommandHandlerTests"` → all pass (5 tests total).

### Step 2: Add a partial-refund test to RefundPaymentCommandHandlerTests

Add one `[Fact]` (mirror `Handle_ApprovedPayment_RefundsAndReturnsSuccess`, but
pass a specific amount and assert the gateway received it):

```csharp
[Fact]
public async Task Handle_PartialRefund_PassesRequestedAmountToGateway()
{
    var payment = MakeApprovedPayment(); // amount 100m
    _paymentRepo.Setup(r => r.GetByIdAsync(payment.Id, default)).ReturnsAsync(payment);
    _gateway.Setup(g => g.RefundAsync("mp_1", 50m, default))
        .ReturnsAsync(new RefundResult(true, null));

    var result = await MakeHandler().Handle(
        new RefundPaymentCommand(payment.Id, Amount: 50m), default);

    result.IsSuccess.Should().BeTrue();
    _gateway.Verify(g => g.RefundAsync("mp_1", 50m, default), Times.Once);
    _gateway.Verify(g => g.RefundAsync("mp_1", 100m, default), Times.Never);
}
```

**Verify**: `dotnet test tests/Horafy.Application.Tests --nologo -v q --filter "FullyQualifiedName~RefundPaymentCommandHandlerTests"` → all pass (5 tests total).

## Test plan

- ConfirmPayment: 2 new cases (Rejected, Cancelled) → both reach `payment.Reject`
  and persist. Asserts `Status == Rejected` for both.
- RefundPayment: 1 new case → partial amount `50m` is the value passed to
  `IPaymentGateway.RefundAsync`, proving `request.Amount ?? payment.Amount` honors
  the request amount.
- Pattern source: the existing tests in the same two files.

## Done criteria

ALL must hold:

- [ ] `dotnet build Horafy.sln -c Debug --nologo` exits `0 Erro(s)`
- [ ] `dotnet test tests/Horafy.Application.Tests --filter "FullyQualifiedName~Payments"` — all pass, with 3 new tests
- [ ] No files under `src/` modified (`git status` shows only the two test files)
- [ ] `plans/README.md` status row for 016 updated

## STOP conditions

Stop and report back if:

- `Payment.Reject(...)` does not set `Status` to `Rejected` (the assertion would
  fail) — that is a real bug; report it instead of weakening the test.
- The partial-refund test fails because the handler passes `payment.Amount`
  instead of `request.Amount` — that is a real bug; report it.
- `PaymentStatusResult` or `RefundResult` constructors differ from the excerpts.

## Maintenance notes

- If MercadoPago adds an `InProcess`/`Pending` webhook status that should NOT
  reject the payment, add a test pinning that behavior (today the handler leaves
  such statuses untouched — neither approve nor reject).
- Reviewer: confirm these are test-only changes (no `src/` diff).
