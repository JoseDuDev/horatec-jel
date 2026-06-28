# Plan 015: Corrigir o Location header do POST /reviews (aponta para recurso errado)

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md`.
>
> **Drift check (run first)**: `git diff --stat 2a9b001..HEAD -- src/Horafy.API/Controllers/V1/ReviewsController.cs src/Horafy.Application/Features/Reviews/Commands/CreateReviewCommand.cs`
> If either in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P2
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: bug
- **Planned at**: commit `2a9b001`, 2026-06-28

## Why this matters

`POST /api/v1/reviews` returns HTTP 201 with a `Location` header built from
`CreatedAtAction(nameof(GetByResource), new { resourceId = result.Value })`.
But `result.Value` is the **review id** returned by the command handler, while
`GetByResource` expects a **resourceId** (the professional/room being reviewed).
A client that follows the `Location` requests
`/api/v1/reviews/resources/{reviewId}` and gets an empty list (the review id is
never a valid resource id). The fix makes the command also return the booking's
`ResourceId` so the controller can emit a correct `Location`.

## Current state

- `src/Horafy.Application/Features/Reviews/Commands/CreateReviewCommand.cs` —
  command + handler. Today it returns `Result<Guid>` (the new review id). The
  handler already has the resource id in scope (variable `resourceId`, from
  `booking.ResourceId`). Excerpt:

```csharp
// line 11-14
public sealed record CreateReviewCommand(
    Guid    BookingId,
    int     Stars,
    string? Comment) : IRequest<Result<Guid>>;

// inside Handle, line 54-67
        if (booking.ResourceId is not { } resourceId)
            return Result.Failure<Guid>(new Error(
                "Review.RentalNotReviewable",
                "Locações não podem ser avaliadas.",
                ErrorType.Validation));

        var review = Review.Create(
            request.BookingId, resourceId,
            currentUserService.UserId.Value, request.Stars, request.Comment);

        reviewRepository.Add(review);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(review.Id);
```

- `src/Horafy.API/Controllers/V1/ReviewsController.cs` — controller. The bug is
  at lines 24-30:

```csharp
        var result = await Sender.Send(
            new CreateReviewCommand(request.BookingId, request.Stars, request.Comment), ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetByResource),
                new { resourceId = result.Value }, result.Value)   // <-- result.Value is the REVIEW id
            : ToActionResult(result);
```

- `GetByResource` action signature (same file): `GetByResource(Guid resourceId, int pageNumber = 1, int pageSize = 20, ...)`.

- Convention: handlers return `Result<T>` (`src/Horafy.Shared/Result.cs`).
  Result records for command outputs are plain `sealed record`s declared next to
  the command (see `ResourceReviewsResult` in
  `src/Horafy.Application/Features/Reviews/Queries/GetResourceReviewsQuery.cs`).

## Commands you will need

| Purpose   | Command                                                                 | Expected on success |
|-----------|-------------------------------------------------------------------------|---------------------|
| Build     | `dotnet build Horafy.sln -c Debug --nologo`                             | `0 Erro(s)`         |
| Tests     | `dotnet test Horafy.sln --nologo -v q`                                   | all pass            |
| Focused   | `dotnet test tests/Horafy.Application.Tests --nologo -v q --filter "FullyQualifiedName~Reviews"` | all pass |

## Scope

**In scope** (the only files you should modify):
- `src/Horafy.Application/Features/Reviews/Commands/CreateReviewCommand.cs`
- `src/Horafy.API/Controllers/V1/ReviewsController.cs`
- `tests/Horafy.Application.Tests/Reviews/CreateReviewCommandTests.cs` (add one assertion/test)

**Out of scope** (do NOT touch):
- `GetResourceReviewsQuery.cs` / `ReviewResult` shape — clients depend on it.
- The `ReplyToReviewCommand` and the `Reply` endpoint.
- Any database/migration change — this is a pure in-memory contract change.

## Git workflow

- Branch: `advisor/015-review-location-header`
- Conventional commits, e.g. `fix(reviews): corrigir Location header do POST /reviews`.
- Do NOT push or open a PR unless the operator instructs it.

## Steps

### Step 1: Make the command return review id + resource id

In `CreateReviewCommand.cs`:

1. Add a result record next to the command:
   ```csharp
   public sealed record CreateReviewResult(Guid ReviewId, Guid ResourceId);
   ```
2. Change the command to `IRequest<Result<CreateReviewResult>>`.
3. Change the handler class to `IRequestHandler<CreateReviewCommand, Result<CreateReviewResult>>`
   and its `Handle` return type to `Task<Result<CreateReviewResult>>`.
4. Replace every `Result.Failure<Guid>(...)` in the handler with
   `Result.Failure<CreateReviewResult>(...)` (there are several — the unauthorized,
   booking-not-found, not-your-booking, not-completed, already-reviewed, and
   rental-not-reviewable branches).
5. Change the final success line from `return Result.Success(review.Id);` to:
   ```csharp
   return Result.Success(new CreateReviewResult(review.Id, resourceId));
   ```

**Verify**: `dotnet build Horafy.sln -c Debug --nologo` → `0 Erro(s)` (the controller will still compile because `result.Value` becomes `CreateReviewResult`, but the `Location` is still wrong until Step 2 — that's fine for build).

### Step 2: Emit a correct Location header

In `ReviewsController.cs`, the `Create` action, replace the success branch:

```csharp
return result.IsSuccess
    ? CreatedAtAction(nameof(GetByResource),
        new { resourceId = result.Value.ResourceId }, result.Value.ReviewId)
    : ToActionResult(result);
```

**Verify**: `dotnet build Horafy.sln -c Debug --nologo` → `0 Erro(s)`.

### Step 3: Update/extend the handler tests

In `tests/Horafy.Application.Tests/Reviews/CreateReviewCommandTests.cs`, the
existing tests only assert `result.IsSuccess` / `result.Error.Code`, so they keep
compiling and passing. Add ONE assertion to `Handle_ValidReview_CreatesAndSaves`
after `result.IsSuccess.Should().BeTrue();`:

```csharp
result.Value.ReviewId.Should().NotBeEmpty();
result.Value.ResourceId.Should().Be(resourceId);
```

**Verify**: `dotnet test tests/Horafy.Application.Tests --nologo -v q --filter "FullyQualifiedName~CreateReviewCommandTests"` → all pass.

## Test plan

- Extend `CreateReviewCommandTests.Handle_ValidReview_CreatesAndSaves` to assert
  the returned `ResourceResult.ResourceId` equals the booking's resource id —
  this is the regression guard for the Location bug.
- No new controller/integration test is required (no existing controller test
  harness in this repo); the handler-level assertion is sufficient.
- Run the full Reviews slice: `dotnet test tests/Horafy.Application.Tests --filter "FullyQualifiedName~Reviews"` → all pass.

## Done criteria

ALL must hold:

- [ ] `dotnet build Horafy.sln -c Debug --nologo` exits with `0 Erro(s)`
- [ ] `dotnet test Horafy.sln --nologo -v q` — all pass; `CreateReviewCommandTests` asserts `result.Value.ResourceId`
- [ ] `ReviewsController.Create` passes `result.Value.ResourceId` (not the review id) as `resourceId`
- [ ] No files outside the in-scope list are modified (`git status`)
- [ ] `plans/README.md` status row for 015 updated

## STOP conditions

Stop and report back (do not improvise) if:

- The `CreateReviewCommand.cs` or `ReviewsController.cs` code does not match the
  "Current state" excerpts (codebase drifted).
- The build surfaces other callers of `CreateReviewCommand` that consume
  `Result<Guid>` and now break — there should be none besides the controller; if
  there are, list them and stop.

## Maintenance notes

- If a `GET /reviews/{reviewId}` single-review endpoint is ever added, prefer
  pointing the `Location` at it (`CreatedAtAction(nameof(GetById), new { reviewId = result.Value.ReviewId })`)
  and revert this resource-id workaround.
- Reviewer should confirm the 201 response body is unchanged (still the review id
  GUID) so existing clients that read the body keep working.
