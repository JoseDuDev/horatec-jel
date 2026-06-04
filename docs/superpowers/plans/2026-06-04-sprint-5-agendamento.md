# Sprint 5 — Agendamento (Booking) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Completar o módulo de Agendamento com fluxo completo (Complete/NoShow), política de cancelamento por tenant, recorrência de agendamentos, fila de espera e suporte a múltiplos serviços por agendamento.

**Architecture:** Todas as entidades de tenant (Booking, WaitlistEntry, BookingService) vivem no schema `tenant_{slug}` gerenciado pelo `TenantSchemaService` via SQL DDL idempotente — sem EF migrations para schemas de tenant. A `CancellationPolicy` é owned entity no `Tenant` (schema `public`), requer uma EF migration. Domain events existentes são usados para integração entre módulos (BookingCancelledEvent → promoção de waitlist).

**Tech Stack:** .NET 8, EF Core 8, PostgreSQL 16, MediatR, FluentValidation, xUnit, FluentAssertions, Moq

---

## File Map

### Criar
- `src/Horafy.Domain/Entities/Tenants/CancellationPolicy.cs` — value object com MinCancellationHours, CancellationFeePercent, AllowCustomerCancellation
- `src/Horafy.Domain/Entities/Bookings/RecurrenceFrequency.cs` — enum Weekly/Biweekly/Monthly
- `src/Horafy.Domain/Entities/Bookings/WaitlistEntry.cs` — entidade de fila de espera
- `src/Horafy.Domain/Entities/Bookings/WaitlistStatus.cs` — enum Waiting/Notified/Cancelled
- `src/Horafy.Domain/Entities/Bookings/BookingService.cs` — linha de serviço por agendamento (multi-serviço)
- `src/Horafy.Domain/Events/Bookings/WaitlistPromotedEvent.cs` — evento ao promover entrada da fila
- `src/Horafy.Domain/Interfaces/Repositories/IWaitlistRepository.cs`
- `src/Horafy.Application/Features/Bookings/Commands/CompleteBookingCommand.cs`
- `src/Horafy.Application/Features/Bookings/Commands/NoShowBookingCommand.cs`
- `src/Horafy.Application/Features/Bookings/Commands/CreateRecurringBookingCommand.cs`
- `src/Horafy.Application/Features/Bookings/Queries/GetMyBookingsQuery.cs`
- `src/Horafy.Application/Features/Waitlist/WaitlistErrors.cs`
- `src/Horafy.Application/Features/Waitlist/Commands/JoinWaitlistCommand.cs`
- `src/Horafy.Application/Features/Waitlist/Commands/LeaveWaitlistCommand.cs`
- `src/Horafy.Application/Features/Waitlist/Queries/GetMyWaitlistQuery.cs`
- `src/Horafy.Application/Features/Waitlist/EventHandlers/BookingCancelledEventHandler.cs`
- `src/Horafy.Infrastructure/Persistence/TenantConfigurations/WaitlistEntryEntityConfiguration.cs`
- `src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingServiceEntityConfiguration.cs`
- `src/Horafy.Infrastructure/Repositories/WaitlistRepository.cs`
- `src/Horafy.API/Controllers/V1/WaitlistController.cs`
- `tests/Horafy.Application.Tests/Bookings/CompleteBookingCommandHandlerTests.cs`
- `tests/Horafy.Application.Tests/Bookings/GetMyBookingsQueryHandlerTests.cs`
- `tests/Horafy.Application.Tests/Bookings/CancelBookingCommandHandlerTests.cs`
- `tests/Horafy.Application.Tests/Bookings/CreateRecurringBookingCommandHandlerTests.cs`
- `tests/Horafy.Application.Tests/Waitlist/JoinWaitlistCommandHandlerTests.cs`
- `tests/Horafy.Application.Tests/Waitlist/BookingCancelledEventHandlerTests.cs`

### Modificar
- `src/Horafy.Domain/Entities/Bookings/Booking.cs` — add RecurrenceGroupId, ExpiresAt, Services collection
- `src/Horafy.Domain/Entities/Tenants/Tenant.cs` — add CancellationPolicy owned entity
- `src/Horafy.Domain/Interfaces/Repositories/IBookingRepository.cs` — add GetByRecurrenceGroupAsync
- `src/Horafy.Application/Features/Bookings/Commands/CancelBookingCommand.cs` — validar CancellationPolicy
- `src/Horafy.Application/Features/Bookings/Commands/CreateBookingCommand.cs` — aceitar múltiplos serviços
- `src/Horafy.Application/Features/Bookings/BookingErrors.cs` — add CancellationWindowClosed, CancellationNotAllowed
- `src/Horafy.Application/Features/Bookings/Queries/GetBookingsQuery.cs` — add RecurrenceGroupId no DTO
- `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs` — add colunas e tabelas novas
- `src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingEntityConfiguration.cs` — map RecurrenceGroupId, ExpiresAt, Services
- `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs` — add WaitlistEntry, BookingService DbSets
- `src/Horafy.Infrastructure/Persistence/Configurations/TenantEntityConfiguration.cs` — add CancellationPolicy OwnsOne
- `src/Horafy.Infrastructure/Repositories/BookingRepository.cs` — add GetByRecurrenceGroupAsync
- `src/Horafy.Infrastructure/DependencyInjection.cs` — register IWaitlistRepository
- `src/Horafy.API/Controllers/V1/BookingsController.cs` — add complete, no-show, recurring, my endpoints
- `tests/Horafy.Application.Tests/Bookings/CreateBookingCommandHandlerTests.cs` — adapt para multi-serviço

---

## Task 1: CompleteBooking e NoShowBooking Commands

**Files:**
- Create: `src/Horafy.Application/Features/Bookings/Commands/CompleteBookingCommand.cs`
- Create: `src/Horafy.Application/Features/Bookings/Commands/NoShowBookingCommand.cs`
- Modify: `src/Horafy.API/Controllers/V1/BookingsController.cs`
- Create: `tests/Horafy.Application.Tests/Bookings/CompleteBookingCommandHandlerTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// tests/Horafy.Application.Tests/Bookings/CompleteBookingCommandHandlerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Bookings.Commands;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Bookings;

public class CompleteBookingCommandHandlerTests
{
    private readonly Mock<IBookingRepository>  _bookingRepo = new();
    private readonly Mock<ITenantUnitOfWork>   _unitOfWork  = new();

    private CompleteBookingCommandHandler MakeHandler() =>
        new(_bookingRepo.Object, _unitOfWork.Object);

    private static Booking MakeConfirmedBooking()
    {
        var b = Booking.Create(
            new[] { (Service.Create("Corte", 60, 50m).Id, "Corte", 60) },
            Resource.Create("João", ResourceType.Professional).Id,
            Guid.NewGuid(), "Cliente", "cliente@test.com",
            DateTimeOffset.UtcNow.AddHours(2));
        b.Confirm();
        return b;
    }

    [Fact]
    public async Task Handle_ConfirmedBooking_ReturnsSuccess()
    {
        var booking = MakeConfirmedBooking();
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);

        var result = await MakeHandler().Handle(new CompleteBookingCommand(booking.Id), default);

        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Completed);
    }

    [Fact]
    public async Task Handle_BookingNotFound_ReturnsError()
    {
        _bookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
                    .ReturnsAsync((Booking?)null);

        var result = await MakeHandler().Handle(new CompleteBookingCommand(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.NotFound");
    }

    [Fact]
    public async Task Handle_PendingBooking_ReturnsInvalidOperation()
    {
        var booking = Booking.Create(
            new[] { (Service.Create("Corte", 60, 50m).Id, "Corte", 60) },
            Resource.Create("João", ResourceType.Professional).Id,
            Guid.NewGuid(), "Cliente", "cliente@test.com",
            DateTimeOffset.UtcNow.AddHours(2));
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);

        var act = () => MakeHandler().Handle(new CompleteBookingCommand(booking.Id), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Verificar que os testes falham**

```
cd C:\Projetos\JEL\JEL\Horafy
dotnet test tests/Horafy.Application.Tests --filter "CompleteBookingCommandHandlerTests" --no-build 2>&1 | tail -10
```
Expected: FAIL com "type or namespace name 'CompleteBookingCommand' could not be found"

- [ ] **Step 3: Implementar CompleteBookingCommand**

```csharp
// src/Horafy.Application/Features/Bookings/Commands/CompleteBookingCommand.cs
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Commands;

public sealed record CompleteBookingCommand(Guid BookingId) : IRequest<Result>;

internal sealed class CompleteBookingCommandHandler(
    IBookingRepository bookingRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CompleteBookingCommand, Result>
{
    public async Task<Result> Handle(CompleteBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking is null) return Result.Failure(BookingErrors.NotFound);

        booking.Complete();
        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

```csharp
// src/Horafy.Application/Features/Bookings/Commands/NoShowBookingCommand.cs
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Commands;

public sealed record NoShowBookingCommand(Guid BookingId) : IRequest<Result>;

internal sealed class NoShowBookingCommandHandler(
    IBookingRepository bookingRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<NoShowBookingCommand, Result>
{
    public async Task<Result> Handle(NoShowBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking is null) return Result.Failure(BookingErrors.NotFound);

        booking.MarkNoShow();
        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 4: Adicionar endpoints no BookingsController**

Adicionar ao final de `BookingsController.cs`, antes do fechamento da classe:

```csharp
    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,TenantStaff,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CompleteBookingCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpPost("{id:guid}/no-show")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,TenantStaff,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> NoShow(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new NoShowBookingCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
```

- [ ] **Step 5: Rodar os testes**

```
dotnet test tests/Horafy.Application.Tests --filter "CompleteBookingCommandHandlerTests" -v minimal
```
Expected: 3 passed

- [ ] **Step 6: Build completo**

```
dotnet build Horafy.sln -c Debug
```
Expected: 0 error(s)

- [ ] **Step 7: Commit**

```
git add src/Horafy.Application/Features/Bookings/Commands/CompleteBookingCommand.cs
git add src/Horafy.Application/Features/Bookings/Commands/NoShowBookingCommand.cs
git add src/Horafy.API/Controllers/V1/BookingsController.cs
git add tests/Horafy.Application.Tests/Bookings/CompleteBookingCommandHandlerTests.cs
git commit -m "feat: add CompleteBooking and NoShowBooking commands and endpoints"
```

---

## Task 2: GetMyBookings Query

**Files:**
- Create: `src/Horafy.Application/Features/Bookings/Queries/GetMyBookingsQuery.cs`
- Modify: `src/Horafy.API/Controllers/V1/BookingsController.cs`
- Create: `tests/Horafy.Application.Tests/Bookings/GetMyBookingsQueryHandlerTests.cs`

- [ ] **Step 1: Escrever o teste que falha**

```csharp
// tests/Horafy.Application.Tests/Bookings/GetMyBookingsQueryHandlerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Bookings.Queries;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Bookings;

public class GetMyBookingsQueryHandlerTests
{
    private readonly Mock<IBookingRepository>  _bookingRepo = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetMyBookingsQueryHandler MakeHandler() =>
        new(_bookingRepo.Object, _currentUser.Object);

    [Fact]
    public async Task Handle_AuthenticatedUser_ReturnsOwnBookings()
    {
        var userId = Guid.NewGuid();
        var booking = Booking.Create(
            new[] { (Service.Create("Corte", 60, 50m).Id, "Corte", 60) },
            Resource.Create("João", ResourceType.Professional).Id,
            userId, "Cliente", "cliente@test.com",
            DateTimeOffset.UtcNow.AddHours(2));

        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _bookingRepo.Setup(r => r.GetByCustomerAsync(userId, default))
                    .ReturnsAsync(new List<Booking> { booking });

        var result = await MakeHandler().Handle(new GetMyBookingsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].CustomerId.Should().Be(userId);
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsUnauthorized()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(false);
        _currentUser.SetupGet(u => u.UserId).Returns((Guid?)null);

        var result = await MakeHandler().Handle(new GetMyBookingsQuery(), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(Horafy.Shared.ErrorType.Unauthorized);
    }
}
```

- [ ] **Step 2: Verificar que o teste falha**

```
dotnet test tests/Horafy.Application.Tests --filter "GetMyBookingsQueryHandlerTests" --no-build 2>&1 | tail -5
```
Expected: FAIL

- [ ] **Step 3: Implementar GetMyBookingsQuery**

```csharp
// src/Horafy.Application/Features/Bookings/Queries/GetMyBookingsQuery.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Queries;

public sealed record GetMyBookingsQuery : IRequest<Result<IReadOnlyList<BookingResult>>>;

internal sealed class GetMyBookingsQueryHandler(
    IBookingRepository bookingRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetMyBookingsQuery, Result<IReadOnlyList<BookingResult>>>
{
    public async Task<Result<IReadOnlyList<BookingResult>>> Handle(
        GetMyBookingsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
            return Result.Failure<IReadOnlyList<BookingResult>>(Error.Unauthorized);

        var bookings = await bookingRepository.GetByCustomerAsync(
            currentUser.UserId.Value, cancellationToken);

        var result = bookings.Select(b => new BookingResult(
            b.Id, b.ServiceId, b.ResourceId, b.CustomerId,
            b.CustomerName, b.CustomerEmail, b.ScheduledAt, b.EndsAt,
            b.DurationMinutes, b.Notes, b.Status, b.CancellationReason))
            .ToList();

        return Result.Success<IReadOnlyList<BookingResult>>(result);
    }
}
```

- [ ] **Step 4: Adicionar endpoint GET /bookings/my no BookingsController**

Adicionar antes do método `GetAll`:

```csharp
    [HttpGet("my")]
    [ProducesResponseType(typeof(IReadOnlyList<BookingResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetMyBookingsQuery(), cancellationToken));
```

- [ ] **Step 5: Rodar os testes**

```
dotnet test tests/Horafy.Application.Tests --filter "GetMyBookingsQueryHandlerTests" -v minimal
```
Expected: 2 passed

- [ ] **Step 6: Commit**

```
git add src/Horafy.Application/Features/Bookings/Queries/GetMyBookingsQuery.cs
git add src/Horafy.API/Controllers/V1/BookingsController.cs
git add tests/Horafy.Application.Tests/Bookings/GetMyBookingsQueryHandlerTests.cs
git commit -m "feat: add GetMyBookings query for customer-facing booking list"
```

---

## Task 3: CancellationPolicy — Owned Entity + Migration + Validação no Cancel

**Files:**
- Create: `src/Horafy.Domain/Entities/Tenants/CancellationPolicy.cs`
- Modify: `src/Horafy.Domain/Entities/Tenants/Tenant.cs`
- Modify: `src/Horafy.Infrastructure/Persistence/Configurations/TenantEntityConfiguration.cs`
- Modify: `src/Horafy.Application/Features/Bookings/Commands/CancelBookingCommand.cs`
- Modify: `src/Horafy.Application/Features/Bookings/BookingErrors.cs`
- Create: `tests/Horafy.Application.Tests/Bookings/CancelBookingCommandHandlerTests.cs`

- [ ] **Step 1: Criar CancellationPolicy value object**

```csharp
// src/Horafy.Domain/Entities/Tenants/CancellationPolicy.cs
namespace Horafy.Domain.Entities.Tenants;

public sealed class CancellationPolicy
{
    private CancellationPolicy() { }

    public int MinCancellationHours { get; private set; }
    public decimal CancellationFeePercent { get; private set; }
    public bool AllowCustomerCancellation { get; private set; } = true;

    public static readonly CancellationPolicy Default =
        new() { MinCancellationHours = 0, CancellationFeePercent = 0, AllowCustomerCancellation = true };

    public static CancellationPolicy Create(int minHours, decimal feePercent, bool allowCustomer)
    {
        if (minHours < 0) throw new ArgumentException("MinCancellationHours não pode ser negativo.", nameof(minHours));
        if (feePercent < 0 || feePercent > 100) throw new ArgumentException("CancellationFeePercent deve estar entre 0 e 100.", nameof(feePercent));
        return new() { MinCancellationHours = minHours, CancellationFeePercent = feePercent, AllowCustomerCancellation = allowCustomer };
    }

    public bool CanCancelAt(DateTimeOffset scheduledAt, DateTimeOffset now) =>
        (scheduledAt - now) >= TimeSpan.FromHours(MinCancellationHours);
}
```

- [ ] **Step 2: Adicionar CancellationPolicy ao Tenant**

No arquivo `src/Horafy.Domain/Entities/Tenants/Tenant.cs`, adicionar a propriedade e o método após `PlanRenewsAt`:

```csharp
    public CancellationPolicy CancellationPolicy { get; private set; } = CancellationPolicy.Default;
```

E o método após `UpgradePlan`:

```csharp
    public void UpdateCancellationPolicy(int minHours, decimal feePercent, bool allowCustomer)
    {
        CancellationPolicy = CancellationPolicy.Create(minHours, feePercent, allowCustomer);
        UpdatedAt = DateTimeOffset.UtcNow;
    }
```

- [ ] **Step 3: Mapear CancellationPolicy no EF**

Em `TenantEntityConfiguration.cs`, adicionar após o bloco `OwnsOne(t => t.Theme, ...)`:

```csharp
        builder.OwnsOne(t => t.CancellationPolicy, policyBuilder =>
        {
            policyBuilder.Property(p => p.MinCancellationHours)
                .HasDefaultValue(0);
            policyBuilder.Property(p => p.CancellationFeePercent)
                .HasColumnType("numeric(5,2)")
                .HasDefaultValue(0m);
            policyBuilder.Property(p => p.AllowCustomerCancellation)
                .HasDefaultValue(true);
        });
```

- [ ] **Step 4: Gerar e aplicar a migration**

```
dotnet ef migrations add AddCancellationPolicy --project src/Horafy.Infrastructure --startup-project src/Horafy.API
```

Verificar o arquivo gerado em `src/Horafy.Infrastructure/Persistence/Migrations/`. Deve conter `AddColumn` para `cancellation_policy_min_cancellation_hours`, `cancellation_policy_cancellation_fee_percent` e `cancellation_policy_allow_customer_cancellation` na tabela `tenants`.

```
dotnet ef database update --project src/Horafy.Infrastructure --startup-project src/Horafy.API
```

Expected: "Done."

- [ ] **Step 5: Adicionar erros de cancelamento**

Em `src/Horafy.Application/Features/Bookings/BookingErrors.cs`, adicionar:

```csharp
    public static readonly Error CancellationWindowClosed = new(
        "Booking.CancellationWindowClosed",
        "O prazo mínimo para cancelamento já passou.",
        ErrorType.Validation);

    public static readonly Error CancellationNotAllowed = new(
        "Booking.CancellationNotAllowed",
        "O cancelamento pelo cliente não é permitido neste estabelecimento.",
        ErrorType.Validation);
```

- [ ] **Step 6: Escrever testes que falham para CancelBooking com policy**

```csharp
// tests/Horafy.Application.Tests/Bookings/CancelBookingCommandHandlerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Bookings.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Bookings;

public class CancelBookingCommandHandlerTests
{
    private readonly Mock<IBookingRepository>    _bookingRepo   = new();
    private readonly Mock<ITenantRepository>     _tenantRepo    = new();
    private readonly Mock<ICurrentUserService>   _currentUser   = new();
    private readonly Mock<ICurrentTenantService> _currentTenant = new();
    private readonly Mock<ITenantUnitOfWork>     _unitOfWork    = new();

    private CancelBookingCommandHandler MakeHandler() =>
        new(_bookingRepo.Object, _tenantRepo.Object, _currentUser.Object, _currentTenant.Object, _unitOfWork.Object);

    private static Booking MakePendingBooking(Guid customerId) =>
        Booking.Create(
            new[] { (Service.Create("Corte", 60, 50m).Id, "Corte", 60) },
            Resource.Create("João", ResourceType.Professional).Id,
            customerId, "Cliente", "cliente@test.com",
            DateTimeOffset.UtcNow.AddHours(3));

    private static Tenant MakeTenantWithPolicy(int minHours = 0, bool allowCustomer = true)
    {
        var t = Tenant.Create("Test", "test-slug", TenantVertical.Barbershop);
        t.UpdateCancellationPolicy(minHours, 0, allowCustomer);
        return t;
    }

    [Fact]
    public async Task Handle_OwnerCancels_WithinPolicy_ReturnsSuccess()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var booking = MakePendingBooking(userId);
        var tenant = MakeTenantWithPolicy(minHours: 1);  // 1h minimum, booking is 3h away

        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _currentUser.SetupGet(u => u.Role).Returns(UserRole.Customer);
        _currentTenant.SetupGet(t => t.TenantId).Returns(tenantId);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        var result = await MakeHandler().Handle(new CancelBookingCommand(booking.Id, null), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CustomerCancels_PolicyWindowClosed_ReturnsError()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var booking = MakePendingBooking(userId);
        var tenant = MakeTenantWithPolicy(minHours: 48);  // 48h minimum, booking is only 3h away

        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _currentUser.SetupGet(u => u.Role).Returns(UserRole.Customer);
        _currentTenant.SetupGet(t => t.TenantId).Returns(tenantId);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        var result = await MakeHandler().Handle(new CancelBookingCommand(booking.Id, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.CancellationWindowClosed");
    }

    [Fact]
    public async Task Handle_CustomerCancels_NotAllowed_ReturnsError()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var booking = MakePendingBooking(userId);
        var tenant = MakeTenantWithPolicy(allowCustomer: false);

        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _currentUser.SetupGet(u => u.Role).Returns(UserRole.Customer);
        _currentTenant.SetupGet(t => t.TenantId).Returns(tenantId);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        var result = await MakeHandler().Handle(new CancelBookingCommand(booking.Id, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.CancellationNotAllowed");
    }

    [Fact]
    public async Task Handle_StaffCancels_IgnoresPolicy_ReturnsSuccess()
    {
        var tenantId = Guid.NewGuid();
        var booking = MakePendingBooking(Guid.NewGuid());
        var tenant = MakeTenantWithPolicy(minHours: 48, allowCustomer: false);

        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid()); // different user = not owner
        _currentUser.SetupGet(u => u.Role).Returns(UserRole.TenantAdmin);
        _currentTenant.SetupGet(t => t.TenantId).Returns(tenantId);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        var result = await MakeHandler().Handle(new CancelBookingCommand(booking.Id, "Staff override"), default);

        result.IsSuccess.Should().BeTrue();
    }
}
```

- [ ] **Step 7: Verificar que os testes falham**

```
dotnet test tests/Horafy.Application.Tests --filter "CancelBookingCommandHandlerTests" --no-build 2>&1 | tail -5
```
Expected: FAIL (CancelBookingCommandHandler constructor não aceita os novos parâmetros)

- [ ] **Step 8: Atualizar CancelBookingCommand para validar a policy**

```csharp
// src/Horafy.Application/Features/Bookings/Commands/CancelBookingCommand.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Commands;

public sealed record CancelBookingCommand(Guid BookingId, string? Reason) : IRequest<Result>;

internal sealed class CancelBookingCommandHandler(
    IBookingRepository bookingRepository,
    ITenantRepository tenantRepository,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CancelBookingCommand, Result>
{
    public async Task<Result> Handle(CancelBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking is null) return Result.Failure(BookingErrors.NotFound);

        var isOwner = booking.CustomerId == currentUser.UserId;
        var isStaff = currentUser.Role is
            UserRole.TenantOwner or UserRole.TenantAdmin or
            UserRole.TenantStaff or UserRole.PlatformAdmin;

        if (!isOwner && !isStaff) return Result.Failure(BookingErrors.NotOwner);

        // Staff bypassa a política de cancelamento
        if (isOwner && !isStaff && currentTenant.TenantId.HasValue)
        {
            var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
            if (tenant is not null)
            {
                if (!tenant.CancellationPolicy.AllowCustomerCancellation)
                    return Result.Failure(BookingErrors.CancellationNotAllowed);

                if (!tenant.CancellationPolicy.CanCancelAt(booking.ScheduledAt, DateTimeOffset.UtcNow))
                    return Result.Failure(BookingErrors.CancellationWindowClosed);
            }
        }

        booking.Cancel(request.Reason);
        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 9: Rodar os testes**

```
dotnet test tests/Horafy.Application.Tests --filter "CancelBookingCommandHandlerTests" -v minimal
```
Expected: 4 passed

- [ ] **Step 10: Build + commit**

```
dotnet build Horafy.sln -c Debug
```

```
git add src/Horafy.Domain/Entities/Tenants/CancellationPolicy.cs
git add src/Horafy.Domain/Entities/Tenants/Tenant.cs
git add src/Horafy.Infrastructure/Persistence/Configurations/TenantEntityConfiguration.cs
git add src/Horafy.Infrastructure/Persistence/Migrations/
git add src/Horafy.Application/Features/Bookings/Commands/CancelBookingCommand.cs
git add src/Horafy.Application/Features/Bookings/BookingErrors.cs
git add tests/Horafy.Application.Tests/Bookings/CancelBookingCommandHandlerTests.cs
git commit -m "feat: add CancellationPolicy owned entity and enforce on customer cancel"
```

---

## Task 4: RecurrenceGroupId + ExpiresAt no Booking (Schema + Entidade + Config)

**Files:**
- Modify: `src/Horafy.Domain/Entities/Bookings/Booking.cs`
- Create: `src/Horafy.Domain/Entities/Bookings/RecurrenceFrequency.cs`
- Modify: `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs`
- Modify: `src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingEntityConfiguration.cs`

- [ ] **Step 1: Criar RecurrenceFrequency enum**

```csharp
// src/Horafy.Domain/Entities/Bookings/RecurrenceFrequency.cs
namespace Horafy.Domain.Entities.Bookings;

public enum RecurrenceFrequency
{
    Weekly    = 0,
    Biweekly  = 1,
    Monthly   = 2
}
```

- [ ] **Step 2: Adicionar RecurrenceGroupId e ExpiresAt ao Booking**

Em `src/Horafy.Domain/Entities/Bookings/Booking.cs`, adicionar as propriedades após `CompletedAt`:

```csharp
    public Guid?           RecurrenceGroupId { get; private set; }
    public DateTimeOffset? ExpiresAt         { get; private set; }
```

E atualizar o factory `Create` para aceitar os novos parâmetros opcionais (adicionar ao final da assinatura):

```csharp
    public static Booking Create(
        Guid serviceId,
        Guid resourceId,
        Guid customerId,
        string customerName,
        string customerEmail,
        DateTimeOffset scheduledAt,
        int durationMinutes,
        string? notes = null,
        Guid? recurrenceGroupId = null,
        DateTimeOffset? expiresAt = null)
    {
        if (scheduledAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException("A data do agendamento deve ser futura.", nameof(scheduledAt));
        if (durationMinutes <= 0)
            throw new ArgumentException("Duração deve ser maior que zero.", nameof(durationMinutes));

        var booking = new Booking
        {
            ServiceId         = serviceId,
            ResourceId        = resourceId,
            CustomerId        = customerId,
            CustomerName      = customerName.Trim(),
            CustomerEmail     = customerEmail.ToLowerInvariant().Trim(),
            ScheduledAt       = scheduledAt,
            EndsAt            = scheduledAt.AddMinutes(durationMinutes),
            DurationMinutes   = durationMinutes,
            Notes             = notes?.Trim(),
            RecurrenceGroupId = recurrenceGroupId,
            ExpiresAt         = expiresAt
        };

        booking.RaiseDomainEvent(new BookingCreatedEvent(
            booking.Id, serviceId, resourceId, customerId, scheduledAt));

        return booking;
    }
```

Atualizar também `OverlapsWith` para ignorar agendamentos expirados:

```csharp
    public bool OverlapsWith(DateTimeOffset start, DateTimeOffset end) =>
        Status is not (BookingStatus.Cancelled or BookingStatus.NoShow)
        && (ExpiresAt is null || ExpiresAt > DateTimeOffset.UtcNow)
        && ScheduledAt < end
        && EndsAt > start;
```

- [ ] **Step 3: Adicionar colunas ao DDL do TenantSchemaService**

Em `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs`, na definição da tabela `bookings`, adicionar as colunas após `completed_at`:

```sql
            recurrence_group_id  UUID,
            expires_at           TIMESTAMPTZ,
```

A tabela `bookings` deve ficar:

```sql
        CREATE TABLE IF NOT EXISTS {s}.bookings (
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
```

Adicionar índice após os índices existentes de bookings:

```sql
        CREATE INDEX IF NOT EXISTS ix_bookings_recurrence_group
            ON {s}.bookings (recurrence_group_id)
            WHERE recurrence_group_id IS NOT NULL;
```

- [ ] **Step 4: Mapear novas colunas no BookingEntityConfiguration**

Em `src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingEntityConfiguration.cs`, adicionar após o mapeamento de `Status`:

```csharp
        builder.Property(b => b.RecurrenceGroupId);
        builder.Property(b => b.ExpiresAt);

        builder.HasIndex(b => b.RecurrenceGroupId)
            .HasDatabaseName("ix_bookings_recurrence_group")
            .HasFilter("recurrence_group_id IS NOT NULL");
```

- [ ] **Step 5: Build + commit**

```
dotnet build Horafy.sln -c Debug
```

```
git add src/Horafy.Domain/Entities/Bookings/Booking.cs
git add src/Horafy.Domain/Entities/Bookings/RecurrenceFrequency.cs
git add src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs
git add src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingEntityConfiguration.cs
git commit -m "feat: add RecurrenceGroupId, ExpiresAt to Booking entity and schema"
```

---

## Task 5: CreateRecurringBookingCommand

**Files:**
- Modify: `src/Horafy.Domain/Interfaces/Repositories/IBookingRepository.cs`
- Modify: `src/Horafy.Infrastructure/Repositories/BookingRepository.cs`
- Create: `src/Horafy.Application/Features/Bookings/Commands/CreateRecurringBookingCommand.cs`
- Modify: `src/Horafy.API/Controllers/V1/BookingsController.cs`
- Create: `tests/Horafy.Application.Tests/Bookings/CreateRecurringBookingCommandHandlerTests.cs`

- [ ] **Step 1: Adicionar GetByRecurrenceGroupAsync ao repositório**

Em `src/Horafy.Domain/Interfaces/Repositories/IBookingRepository.cs`, adicionar:

```csharp
    Task<IReadOnlyList<Booking>> GetByRecurrenceGroupAsync(
        Guid recurrenceGroupId,
        CancellationToken cancellationToken = default);
```

Em `src/Horafy.Infrastructure/Repositories/BookingRepository.cs`, adicionar:

```csharp
    public async Task<IReadOnlyList<Booking>> GetByRecurrenceGroupAsync(
        Guid recurrenceGroupId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(b => b.RecurrenceGroupId == recurrenceGroupId)
            .OrderBy(b => b.ScheduledAt)
            .ToListAsync(cancellationToken);
```

- [ ] **Step 2: Escrever testes que falham**

```csharp
// tests/Horafy.Application.Tests/Bookings/CreateRecurringBookingCommandHandlerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Bookings.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using System.Data;
using Xunit;

namespace Horafy.Application.Tests.Bookings;

public class CreateRecurringBookingCommandHandlerTests
{
    private readonly Mock<IServiceRepository>  _serviceRepo  = new();
    private readonly Mock<IResourceRepository> _resourceRepo = new();
    private readonly Mock<IBookingRepository>  _bookingRepo  = new();
    private readonly Mock<ICurrentUserService> _currentUser  = new();
    private readonly Mock<ITenantUnitOfWork>   _unitOfWork   = new();
    private readonly Mock<ITransaction>        _transaction  = new();

    private CreateRecurringBookingCommandHandler MakeHandler() =>
        new(_serviceRepo.Object, _resourceRepo.Object,
            _bookingRepo.Object, _currentUser.Object, _unitOfWork.Object);

    private void SetupDefaults(Service service, Resource resource, Guid userId)
    {
        _serviceRepo.Setup(r => r.GetByIdAsync(service.Id, default)).ReturnsAsync(service);
        _resourceRepo.Setup(r => r.GetByIdAsync(resource.Id, default)).ReturnsAsync(resource);
        _bookingRepo.Setup(r => r.HasConflictAsync(
            It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(), null, default)).ReturnsAsync(false);
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _currentUser.SetupGet(u => u.Email).Returns("cliente@test.com");
        _unitOfWork.Setup(u => u.BeginTransactionAsync(IsolationLevel.Serializable, default))
                   .ReturnsAsync(_transaction.Object);
    }

    [Fact]
    public async Task Handle_Weekly3Occurrences_Creates3Bookings()
    {
        var service  = Service.Create("Corte", 60, 50m);
        var resource = Resource.Create("João", ResourceType.Professional);
        var userId   = Guid.NewGuid();
        SetupDefaults(service, resource, userId);

        var cmd = new CreateRecurringBookingCommand(
            service.Id, resource.Id,
            DateTimeOffset.UtcNow.AddDays(1),
            RecurrenceFrequency.Weekly, OccurrenceCount: 3, Notes: null);

        var result = await MakeHandler().Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        _bookingRepo.Verify(r => r.Add(It.IsAny<Booking>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_ConflictOnSecondOccurrence_ReturnsConflictError()
    {
        var service  = Service.Create("Corte", 60, 50m);
        var resource = Resource.Create("João", ResourceType.Professional);
        var userId   = Guid.NewGuid();
        var first    = DateTimeOffset.UtcNow.AddDays(1);
        var second   = first.AddDays(7);

        _serviceRepo.Setup(r => r.GetByIdAsync(service.Id, default)).ReturnsAsync(service);
        _resourceRepo.Setup(r => r.GetByIdAsync(resource.Id, default)).ReturnsAsync(resource);
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _currentUser.SetupGet(u => u.Email).Returns("cliente@test.com");
        _unitOfWork.Setup(u => u.BeginTransactionAsync(IsolationLevel.Serializable, default))
                   .ReturnsAsync(_transaction.Object);

        _bookingRepo.Setup(r => r.HasConflictAsync(
            resource.Id, first, first.AddMinutes(60), null, default)).ReturnsAsync(false);
        _bookingRepo.Setup(r => r.HasConflictAsync(
            resource.Id, second, second.AddMinutes(60), null, default)).ReturnsAsync(true);

        var cmd = new CreateRecurringBookingCommand(
            service.Id, resource.Id, first,
            RecurrenceFrequency.Weekly, OccurrenceCount: 2, Notes: null);

        var result = await MakeHandler().Handle(cmd, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.Conflict");
    }
}
```

- [ ] **Step 3: Verificar que os testes falham**

```
dotnet test tests/Horafy.Application.Tests --filter "CreateRecurringBookingCommandHandlerTests" --no-build 2>&1 | tail -5
```

- [ ] **Step 4: Implementar CreateRecurringBookingCommand**

```csharp
// src/Horafy.Application/Features/Bookings/Commands/CreateRecurringBookingCommand.cs
using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;
using System.Data;

namespace Horafy.Application.Features.Bookings.Commands;

public sealed record CreateRecurringBookingCommand(
    Guid ServiceId,
    Guid ResourceId,
    DateTimeOffset FirstOccurrence,
    RecurrenceFrequency Frequency,
    int OccurrenceCount,
    string? Notes) : IRequest<Result<Guid>>;

public sealed class CreateRecurringBookingCommandValidator
    : AbstractValidator<CreateRecurringBookingCommand>
{
    public CreateRecurringBookingCommandValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.ResourceId).NotEmpty();
        RuleFor(x => x.FirstOccurrence).GreaterThan(DateTimeOffset.UtcNow)
            .WithMessage("A primeira ocorrência deve ser futura.");
        RuleFor(x => x.OccurrenceCount).InclusiveBetween(2, 52)
            .WithMessage("OccurrenceCount deve ser entre 2 e 52.");
    }
}

internal sealed class CreateRecurringBookingCommandHandler(
    IServiceRepository serviceRepository,
    IResourceRepository resourceRepository,
    IBookingRepository bookingRepository,
    ICurrentUserService currentUser,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CreateRecurringBookingCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateRecurringBookingCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
            return Result.Failure<Guid>(Error.Unauthorized);

        var service = await serviceRepository.GetByIdAsync(request.ServiceId, cancellationToken);
        if (service is null) return Result.Failure<Guid>(BookingErrors.ServiceNotFound);

        var resource = await resourceRepository.GetByIdAsync(request.ResourceId, cancellationToken);
        if (resource is null) return Result.Failure<Guid>(BookingErrors.ResourceNotFound);

        var occurrences = GenerateOccurrences(request.FirstOccurrence, request.Frequency, request.OccurrenceCount);

        // Verificar conflitos em todas as ocorrências antes de criar qualquer uma
        foreach (var date in occurrences)
        {
            var end = date.AddMinutes(service.DurationMinutes);
            var hasConflict = await bookingRepository.HasConflictAsync(
                request.ResourceId, date, end, cancellationToken: cancellationToken);
            if (hasConflict) return Result.Failure<Guid>(BookingErrors.Conflict);
        }

        var recurrenceGroupId = Guid.NewGuid();

        await using var tx = await unitOfWork.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        foreach (var date in occurrences)
        {
            var booking = Booking.Create(
                request.ServiceId,
                request.ResourceId,
                customerId:       currentUser.UserId.Value,
                customerName:     currentUser.Email ?? "Cliente",
                customerEmail:    currentUser.Email ?? string.Empty,
                scheduledAt:      date,
                durationMinutes:  service.DurationMinutes,
                notes:            request.Notes,
                recurrenceGroupId: recurrenceGroupId);

            bookingRepository.Add(booking);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return Result.Success(recurrenceGroupId);
    }

    private static IReadOnlyList<DateTimeOffset> GenerateOccurrences(
        DateTimeOffset first, RecurrenceFrequency frequency, int count)
    {
        var dates = new List<DateTimeOffset> { first };
        for (var i = 1; i < count; i++)
        {
            var prev = dates[^1];
            dates.Add(frequency switch
            {
                RecurrenceFrequency.Weekly   => prev.AddDays(7),
                RecurrenceFrequency.Biweekly => prev.AddDays(14),
                RecurrenceFrequency.Monthly  => prev.AddMonths(1),
                _ => throw new ArgumentOutOfRangeException(nameof(frequency))
            });
        }
        return dates;
    }
}
```

- [ ] **Step 5: Adicionar endpoint no BookingsController**

Adicionar request record e endpoint ao `BookingsController.cs`:

```csharp
    [HttpPost("recurring")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateRecurring(
        [FromBody] CreateRecurringBookingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new CreateRecurringBookingCommand(
                request.ServiceId, request.ResourceId, request.FirstOccurrence,
                request.Frequency, request.OccurrenceCount, request.Notes),
            cancellationToken);

        if (result.IsFailure) return ToActionResult(result);
        return Ok(result.Value);
    }
```

Adicionar record ao final do arquivo:

```csharp
public sealed record CreateRecurringBookingRequest(
    Guid ServiceId,
    Guid ResourceId,
    DateTimeOffset FirstOccurrence,
    RecurrenceFrequency Frequency,
    int OccurrenceCount,
    string? Notes);
```

- [ ] **Step 6: Rodar os testes**

```
dotnet test tests/Horafy.Application.Tests --filter "CreateRecurringBookingCommandHandlerTests" -v minimal
```
Expected: 2 passed

- [ ] **Step 7: Build + commit**

```
dotnet build Horafy.sln -c Debug

git add src/Horafy.Domain/Interfaces/Repositories/IBookingRepository.cs
git add src/Horafy.Infrastructure/Repositories/BookingRepository.cs
git add src/Horafy.Application/Features/Bookings/Commands/CreateRecurringBookingCommand.cs
git add src/Horafy.API/Controllers/V1/BookingsController.cs
git add tests/Horafy.Application.Tests/Bookings/CreateRecurringBookingCommandHandlerTests.cs
git commit -m "feat: add CreateRecurringBooking command with conflict-safe atomic creation"
```

---

## Task 6: WaitlistEntry — Entidade, Repositório, Schema e DI

**Files:**
- Create: `src/Horafy.Domain/Entities/Bookings/WaitlistEntry.cs`
- Create: `src/Horafy.Domain/Entities/Bookings/WaitlistStatus.cs`
- Create: `src/Horafy.Domain/Events/Bookings/WaitlistPromotedEvent.cs`
- Create: `src/Horafy.Domain/Interfaces/Repositories/IWaitlistRepository.cs`
- Create: `src/Horafy.Infrastructure/Persistence/TenantConfigurations/WaitlistEntryEntityConfiguration.cs`
- Create: `src/Horafy.Infrastructure/Repositories/WaitlistRepository.cs`
- Modify: `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs`
- Modify: `src/Horafy.Infrastructure/DependencyInjection.cs`
- Modify: `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs`

- [ ] **Step 1: Criar WaitlistStatus enum**

```csharp
// src/Horafy.Domain/Entities/Bookings/WaitlistStatus.cs
namespace Horafy.Domain.Entities.Bookings;

public enum WaitlistStatus
{
    Waiting    = 0,
    Notified   = 1,
    Cancelled  = 2
}
```

- [ ] **Step 2: Criar WaitlistEntry entity**

```csharp
// src/Horafy.Domain/Entities/Bookings/WaitlistEntry.cs
using Horafy.Domain.Entities.Base;
using Horafy.Domain.Events.Bookings;

namespace Horafy.Domain.Entities.Bookings;

public sealed class WaitlistEntry : BaseEntity
{
    private WaitlistEntry() { }

    public Guid   ServiceId     { get; private set; }
    public Guid   ResourceId    { get; private set; }
    public Guid   CustomerId    { get; private set; }
    public string CustomerName  { get; private set; } = default!;
    public string CustomerEmail { get; private set; } = default!;
    public DateOnly        PreferredDate { get; private set; }
    public WaitlistStatus  Status        { get; private set; } = WaitlistStatus.Waiting;

    public static WaitlistEntry Create(
        Guid serviceId,
        Guid resourceId,
        Guid customerId,
        string customerName,
        string customerEmail,
        DateOnly preferredDate)
    {
        return new WaitlistEntry
        {
            ServiceId     = serviceId,
            ResourceId    = resourceId,
            CustomerId    = customerId,
            CustomerName  = customerName.Trim(),
            CustomerEmail = customerEmail.ToLowerInvariant().Trim(),
            PreferredDate = preferredDate,
            Status        = WaitlistStatus.Waiting
        };
    }

    public void Promote()
    {
        if (Status != WaitlistStatus.Waiting)
            throw new InvalidOperationException($"Não é possível promover uma entrada no status {Status}.");

        Status = WaitlistStatus.Notified;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new WaitlistPromotedEvent(Id, CustomerId, ServiceId, ResourceId, PreferredDate));
    }

    public void Cancel()
    {
        Status = WaitlistStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 3: Criar WaitlistPromotedEvent**

```csharp
// src/Horafy.Domain/Events/Bookings/WaitlistPromotedEvent.cs
using Horafy.Domain.Events.Base;

namespace Horafy.Domain.Events.Bookings;

public sealed record WaitlistPromotedEvent(
    Guid WaitlistEntryId,
    Guid CustomerId,
    Guid ServiceId,
    Guid ResourceId,
    DateOnly PreferredDate) : DomainEvent;
```

- [ ] **Step 4: Criar IWaitlistRepository**

```csharp
// src/Horafy.Domain/Interfaces/Repositories/IWaitlistRepository.cs
using Horafy.Domain.Entities.Bookings;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IWaitlistRepository : IRepository<WaitlistEntry>
{
    Task<IReadOnlyList<WaitlistEntry>> GetByServiceResourceDateAsync(
        Guid serviceId,
        Guid resourceId,
        DateOnly preferredDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WaitlistEntry>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsActiveAsync(
        Guid serviceId,
        Guid resourceId,
        Guid customerId,
        DateOnly preferredDate,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Adicionar tabela waitlist_entries ao DDL**

Em `TenantSchemaService.cs`, adicionar após os índices de `bookings`:

```sql
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
```

- [ ] **Step 6: Criar WaitlistEntryEntityConfiguration**

```csharp
// src/Horafy.Infrastructure/Persistence/TenantConfigurations/WaitlistEntryEntityConfiguration.cs
using Horafy.Domain.Entities.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class WaitlistEntryEntityConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        builder.ToTable("waitlist_entries");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.CustomerName).IsRequired().HasMaxLength(150);
        builder.Property(w => w.CustomerEmail).IsRequired().HasMaxLength(256);

        builder.Property(w => w.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasIndex(w => new { w.ServiceId, w.ResourceId, w.PreferredDate })
            .HasDatabaseName("ix_waitlist_service_resource_date");

        builder.HasIndex(w => w.CustomerId)
            .HasDatabaseName("ix_waitlist_customer");
    }
}
```

- [ ] **Step 7: Criar WaitlistRepository**

```csharp
// src/Horafy.Infrastructure/Repositories/WaitlistRepository.cs
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class WaitlistRepository(TenantDbContext context)
    : BaseRepository<WaitlistEntry, TenantDbContext>(context), IWaitlistRepository
{
    public async Task<IReadOnlyList<WaitlistEntry>> GetByServiceResourceDateAsync(
        Guid serviceId, Guid resourceId, DateOnly preferredDate,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(w => w.ServiceId == serviceId
                     && w.ResourceId == resourceId
                     && w.PreferredDate == preferredDate
                     && w.Status == WaitlistStatus.Waiting)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WaitlistEntry>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(w => w.CustomerId == customerId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<bool> ExistsActiveAsync(
        Guid serviceId, Guid resourceId, Guid customerId, DateOnly preferredDate,
        CancellationToken cancellationToken = default) =>
        await DbSet.AnyAsync(w =>
            w.ServiceId == serviceId
            && w.ResourceId == resourceId
            && w.CustomerId == customerId
            && w.PreferredDate == preferredDate
            && w.Status == WaitlistStatus.Waiting,
            cancellationToken);
}
```

- [ ] **Step 8: Adicionar ao TenantDbContext e DependencyInjection**

Em `TenantDbContext.cs`, adicionar ao final das propriedades DbSet:

```csharp
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
```

Em `DependencyInjection.cs`, adicionar na seção de repositórios de tenant:

```csharp
        services.AddScoped<IWaitlistRepository, WaitlistRepository>();
```

- [ ] **Step 9: Build + commit**

```
dotnet build Horafy.sln -c Debug

git add src/Horafy.Domain/Entities/Bookings/WaitlistEntry.cs
git add src/Horafy.Domain/Entities/Bookings/WaitlistStatus.cs
git add src/Horafy.Domain/Events/Bookings/WaitlistPromotedEvent.cs
git add src/Horafy.Domain/Interfaces/Repositories/IWaitlistRepository.cs
git add src/Horafy.Infrastructure/Persistence/TenantConfigurations/WaitlistEntryEntityConfiguration.cs
git add src/Horafy.Infrastructure/Repositories/WaitlistRepository.cs
git add src/Horafy.Infrastructure/Persistence/TenantDbContext.cs
git add src/Horafy.Infrastructure/DependencyInjection.cs
git add src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs
git commit -m "feat: add WaitlistEntry entity, repository, and schema DDL"
```

---

## Task 7: Waitlist Commands + Controller

**Files:**
- Create: `src/Horafy.Application/Features/Waitlist/WaitlistErrors.cs`
- Create: `src/Horafy.Application/Features/Waitlist/Commands/JoinWaitlistCommand.cs`
- Create: `src/Horafy.Application/Features/Waitlist/Commands/LeaveWaitlistCommand.cs`
- Create: `src/Horafy.Application/Features/Waitlist/Queries/GetMyWaitlistQuery.cs`
- Create: `src/Horafy.API/Controllers/V1/WaitlistController.cs`
- Create: `tests/Horafy.Application.Tests/Waitlist/JoinWaitlistCommandHandlerTests.cs`

- [ ] **Step 1: Criar WaitlistErrors**

```csharp
// src/Horafy.Application/Features/Waitlist/WaitlistErrors.cs
using Horafy.Shared;

namespace Horafy.Application.Features.Waitlist;

public static class WaitlistErrors
{
    public static readonly Error NotFound = new(
        "Waitlist.NotFound", "Entrada na fila não encontrada.", ErrorType.NotFound);

    public static readonly Error AlreadyInQueue = new(
        "Waitlist.AlreadyInQueue",
        "Você já está na fila para este serviço nesta data.",
        ErrorType.Conflict);

    public static readonly Error ServiceNotFound = new(
        "Waitlist.ServiceNotFound", "Serviço não encontrado.", ErrorType.NotFound);

    public static readonly Error ResourceNotFound = new(
        "Waitlist.ResourceNotFound", "Recurso não encontrado.", ErrorType.NotFound);
}
```

- [ ] **Step 2: Escrever testes de JoinWaitlist que falham**

```csharp
// tests/Horafy.Application.Tests/Waitlist/JoinWaitlistCommandHandlerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Waitlist;
using Horafy.Application.Features.Waitlist.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Waitlist;

public class JoinWaitlistCommandHandlerTests
{
    private readonly Mock<IWaitlistRepository>  _waitlistRepo = new();
    private readonly Mock<IServiceRepository>   _serviceRepo  = new();
    private readonly Mock<IResourceRepository>  _resourceRepo = new();
    private readonly Mock<ICurrentUserService>  _currentUser  = new();
    private readonly Mock<ITenantUnitOfWork>    _unitOfWork   = new();

    private JoinWaitlistCommandHandler MakeHandler() =>
        new(_waitlistRepo.Object, _serviceRepo.Object, _resourceRepo.Object,
            _currentUser.Object, _unitOfWork.Object);

    [Fact]
    public async Task Handle_ValidRequest_ReturnsEntryId()
    {
        var service  = Service.Create("Corte", 60, 50m);
        var resource = Resource.Create("João", ResourceType.Professional);
        var userId   = Guid.NewGuid();
        var date     = DateOnly.FromDateTime(DateTime.Today.AddDays(3));

        _serviceRepo.Setup(r => r.GetByIdAsync(service.Id, default)).ReturnsAsync(service);
        _resourceRepo.Setup(r => r.GetByIdAsync(resource.Id, default)).ReturnsAsync(resource);
        _waitlistRepo.Setup(r => r.ExistsActiveAsync(
            service.Id, resource.Id, userId, date, default)).ReturnsAsync(false);
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _currentUser.SetupGet(u => u.Email).Returns("c@test.com");

        var result = await MakeHandler().Handle(
            new JoinWaitlistCommand(service.Id, resource.Id, date), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_AlreadyInQueue_ReturnsConflict()
    {
        var service  = Service.Create("Corte", 60, 50m);
        var resource = Resource.Create("João", ResourceType.Professional);
        var userId   = Guid.NewGuid();
        var date     = DateOnly.FromDateTime(DateTime.Today.AddDays(3));

        _serviceRepo.Setup(r => r.GetByIdAsync(service.Id, default)).ReturnsAsync(service);
        _resourceRepo.Setup(r => r.GetByIdAsync(resource.Id, default)).ReturnsAsync(resource);
        _waitlistRepo.Setup(r => r.ExistsActiveAsync(
            service.Id, resource.Id, userId, date, default)).ReturnsAsync(true);
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);

        var result = await MakeHandler().Handle(
            new JoinWaitlistCommand(service.Id, resource.Id, date), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Waitlist.AlreadyInQueue");
    }
}
```

- [ ] **Step 3: Verificar que os testes falham**

```
dotnet test tests/Horafy.Application.Tests --filter "JoinWaitlistCommandHandlerTests" --no-build 2>&1 | tail -5
```

- [ ] **Step 4: Implementar JoinWaitlistCommand**

```csharp
// src/Horafy.Application/Features/Waitlist/Commands/JoinWaitlistCommand.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Waitlist.Commands;

public sealed record JoinWaitlistCommand(
    Guid ServiceId,
    Guid ResourceId,
    DateOnly PreferredDate) : IRequest<Result<Guid>>;

internal sealed class JoinWaitlistCommandHandler(
    IWaitlistRepository waitlistRepository,
    IServiceRepository serviceRepository,
    IResourceRepository resourceRepository,
    ICurrentUserService currentUser,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<JoinWaitlistCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(JoinWaitlistCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
            return Result.Failure<Guid>(Error.Unauthorized);

        var service = await serviceRepository.GetByIdAsync(request.ServiceId, cancellationToken);
        if (service is null) return Result.Failure<Guid>(WaitlistErrors.ServiceNotFound);

        var resource = await resourceRepository.GetByIdAsync(request.ResourceId, cancellationToken);
        if (resource is null) return Result.Failure<Guid>(WaitlistErrors.ResourceNotFound);

        var alreadyWaiting = await waitlistRepository.ExistsActiveAsync(
            request.ServiceId, request.ResourceId,
            currentUser.UserId.Value, request.PreferredDate, cancellationToken);

        if (alreadyWaiting) return Result.Failure<Guid>(WaitlistErrors.AlreadyInQueue);

        var entry = WaitlistEntry.Create(
            request.ServiceId, request.ResourceId,
            currentUser.UserId.Value,
            currentUser.Email ?? "Cliente",
            currentUser.Email ?? string.Empty,
            request.PreferredDate);

        waitlistRepository.Add(entry);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(entry.Id);
    }
}
```

- [ ] **Step 5: Implementar LeaveWaitlistCommand**

```csharp
// src/Horafy.Application/Features/Waitlist/Commands/LeaveWaitlistCommand.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Waitlist.Commands;

public sealed record LeaveWaitlistCommand(Guid EntryId) : IRequest<Result>;

internal sealed class LeaveWaitlistCommandHandler(
    IWaitlistRepository waitlistRepository,
    ICurrentUserService currentUser,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<LeaveWaitlistCommand, Result>
{
    public async Task<Result> Handle(LeaveWaitlistCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
            return Result.Failure(Error.Unauthorized);

        var entry = await waitlistRepository.GetByIdAsync(request.EntryId, cancellationToken);
        if (entry is null) return Result.Failure(WaitlistErrors.NotFound);

        if (entry.CustomerId != currentUser.UserId)
            return Result.Failure(Error.Unauthorized);

        entry.Cancel();
        waitlistRepository.Update(entry);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

- [ ] **Step 6: Implementar GetMyWaitlistQuery**

```csharp
// src/Horafy.Application/Features/Waitlist/Queries/GetMyWaitlistQuery.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Waitlist.Queries;

public sealed record WaitlistEntryResult(
    Guid Id,
    Guid ServiceId,
    Guid ResourceId,
    DateOnly PreferredDate,
    WaitlistStatus Status,
    DateTimeOffset CreatedAt);

public sealed record GetMyWaitlistQuery : IRequest<Result<IReadOnlyList<WaitlistEntryResult>>>;

internal sealed class GetMyWaitlistQueryHandler(
    IWaitlistRepository waitlistRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetMyWaitlistQuery, Result<IReadOnlyList<WaitlistEntryResult>>>
{
    public async Task<Result<IReadOnlyList<WaitlistEntryResult>>> Handle(
        GetMyWaitlistQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
            return Result.Failure<IReadOnlyList<WaitlistEntryResult>>(Error.Unauthorized);

        var entries = await waitlistRepository.GetByCustomerAsync(
            currentUser.UserId.Value, cancellationToken);

        var result = entries.Select(e => new WaitlistEntryResult(
            e.Id, e.ServiceId, e.ResourceId, e.PreferredDate, e.Status, e.CreatedAt)).ToList();

        return Result.Success<IReadOnlyList<WaitlistEntryResult>>(result);
    }
}
```

- [ ] **Step 7: Criar WaitlistController**

```csharp
// src/Horafy.API/Controllers/V1/WaitlistController.cs
using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Waitlist.Commands;
using Horafy.Application.Features.Waitlist.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize]
public sealed class WaitlistController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WaitlistEntryResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetMyWaitlistQuery(), cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Join(
        [FromBody] JoinWaitlistRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new JoinWaitlistCommand(request.ServiceId, request.ResourceId, request.PreferredDate),
            cancellationToken);

        if (result.IsFailure) return ToActionResult(result);
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Leave(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new LeaveWaitlistCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record JoinWaitlistRequest(Guid ServiceId, Guid ResourceId, DateOnly PreferredDate);
```

- [ ] **Step 8: Rodar os testes**

```
dotnet test tests/Horafy.Application.Tests --filter "JoinWaitlistCommandHandlerTests" -v minimal
```
Expected: 2 passed

- [ ] **Step 9: Build + commit**

```
dotnet build Horafy.sln -c Debug

git add src/Horafy.Application/Features/Waitlist/
git add src/Horafy.API/Controllers/V1/WaitlistController.cs
git add tests/Horafy.Application.Tests/Waitlist/JoinWaitlistCommandHandlerTests.cs
git commit -m "feat: add Waitlist commands (Join, Leave, GetMine) and controller"
```

---

## Task 8: BookingCancelledEvent Handler — Promoção de Fila de Espera

**Files:**
- Create: `src/Horafy.Application/Features/Waitlist/EventHandlers/BookingCancelledEventHandler.cs`
- Create: `tests/Horafy.Application.Tests/Waitlist/BookingCancelledEventHandlerTests.cs`

- [ ] **Step 1: Escrever o teste que falha**

```csharp
// tests/Horafy.Application.Tests/Waitlist/BookingCancelledEventHandlerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Waitlist.EventHandlers;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Waitlist;

public class BookingCancelledEventHandlerTests
{
    private readonly Mock<IWaitlistRepository> _waitlistRepo = new();
    private readonly Mock<ITenantUnitOfWork>   _unitOfWork   = new();

    private BookingCancelledEventHandler MakeHandler() =>
        new(_waitlistRepo.Object, _unitOfWork.Object);

    [Fact]
    public async Task Handle_WaitingEntries_PromotesFirst()
    {
        var serviceId  = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var date       = DateOnly.FromDateTime(DateTime.Today.AddDays(3));

        var entry1 = WaitlistEntry.Create(serviceId, resourceId, Guid.NewGuid(), "A", "a@test.com", date);
        var entry2 = WaitlistEntry.Create(serviceId, resourceId, Guid.NewGuid(), "B", "b@test.com", date);

        _waitlistRepo
            .Setup(r => r.GetByServiceResourceDateAsync(serviceId, resourceId, date, default))
            .ReturnsAsync(new List<WaitlistEntry> { entry1, entry2 });

        var cancelledEvent = new BookingCancelledEvent(
            Guid.NewGuid(), Guid.NewGuid(), reason: null);

        // We need a booking to get service/resource/date context
        // The event handler looks up bookings by the booking in the event
        // Simpler: the event carries enough context
        // Since BookingCancelledEvent only has BookingId + CustomerId,
        // the handler needs IBookingRepository to get service/resource/scheduledAt
        // Let's inject IBookingRepository in the handler
        // ... see implementation below
        await Task.CompletedTask; // placeholder
    }

    [Fact]
    public async Task Handle_NoWaitingEntries_DoesNothing()
    {
        _waitlistRepo
            .Setup(r => r.GetByServiceResourceDateAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), default))
            .ReturnsAsync(new List<WaitlistEntry>());

        // Should complete without error
        await Task.CompletedTask;
    }
}
```

The handler needs `IBookingRepository` to reconstruct the service/resource/date from the cancelled booking. Let me rewrite the test with that in mind.

- [ ] **Step 2: Reescrever o teste com injeção correta**

```csharp
// tests/Horafy.Application.Tests/Waitlist/BookingCancelledEventHandlerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Waitlist.EventHandlers;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Waitlist;

public class BookingCancelledEventHandlerTests
{
    private readonly Mock<IBookingRepository>  _bookingRepo  = new();
    private readonly Mock<IWaitlistRepository> _waitlistRepo = new();
    private readonly Mock<ITenantUnitOfWork>   _unitOfWork   = new();

    private BookingCancelledEventHandler MakeHandler() =>
        new(_bookingRepo.Object, _waitlistRepo.Object, _unitOfWork.Object);

    private static Booking MakeCancelledBooking(Guid serviceId, Guid resourceId, DateTimeOffset scheduledAt)
    {
        var b = Booking.Create(serviceId, resourceId,
            Guid.NewGuid(), "Cliente", "c@test.com", scheduledAt, 60);
        b.Cancel();
        return b;
    }

    [Fact]
    public async Task Handle_WaitingEntriesExist_PromotesFirst()
    {
        var serviceId  = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var scheduled  = DateTimeOffset.UtcNow.AddDays(3);
        var date       = DateOnly.FromDateTime(scheduled.Date);
        var booking    = MakeCancelledBooking(serviceId, resourceId, scheduled);

        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);

        var entry = WaitlistEntry.Create(serviceId, resourceId, Guid.NewGuid(), "A", "a@test.com", date);
        _waitlistRepo
            .Setup(r => r.GetByServiceResourceDateAsync(serviceId, resourceId, date, default))
            .ReturnsAsync(new List<WaitlistEntry> { entry });

        var domainEvent = new BookingCancelledEvent(booking.Id, booking.CustomerId, null);

        await MakeHandler().Handle(domainEvent, default);

        entry.Status.Should().Be(WaitlistStatus.Notified);
        _waitlistRepo.Verify(r => r.Update(entry), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_NoWaitingEntries_SkipsSave()
    {
        var serviceId  = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var scheduled  = DateTimeOffset.UtcNow.AddDays(3);
        var date       = DateOnly.FromDateTime(scheduled.Date);
        var booking    = MakeCancelledBooking(serviceId, resourceId, scheduled);

        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _waitlistRepo
            .Setup(r => r.GetByServiceResourceDateAsync(serviceId, resourceId, date, default))
            .ReturnsAsync(new List<WaitlistEntry>());

        var domainEvent = new BookingCancelledEvent(booking.Id, booking.CustomerId, null);

        await MakeHandler().Handle(domainEvent, default);

        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }
}
```

- [ ] **Step 3: Verificar que o teste falha**

```
dotnet test tests/Horafy.Application.Tests --filter "BookingCancelledEventHandlerTests" --no-build 2>&1 | tail -5
```

- [ ] **Step 4: Implementar BookingCancelledEventHandler**

```csharp
// src/Horafy.Application/Features/Waitlist/EventHandlers/BookingCancelledEventHandler.cs
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using MediatR;

namespace Horafy.Application.Features.Waitlist.EventHandlers;

internal sealed class BookingCancelledEventHandler(
    IBookingRepository bookingRepository,
    IWaitlistRepository waitlistRepository,
    ITenantUnitOfWork unitOfWork)
    : INotificationHandler<BookingCancelledEvent>
{
    public async Task Handle(BookingCancelledEvent notification, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);
        if (booking is null) return;

        var date = DateOnly.FromDateTime(booking.ScheduledAt.Date);

        var waitingEntries = await waitlistRepository.GetByServiceResourceDateAsync(
            booking.ServiceId, booking.ResourceId, date, cancellationToken);

        if (!waitingEntries.Any()) return;

        var first = waitingEntries[0];
        first.Promote();
        waitlistRepository.Update(first);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Verificar que BookingCancelledEvent tem o campo BookingId**

O arquivo `src/Horafy.Domain/Events/Bookings/BookingCancelledEvent.cs` deve ter `BookingId` e `CustomerId`. Verificar que existe:

```csharp
// Conteúdo esperado (verificar com Read):
public sealed record BookingCancelledEvent(
    Guid BookingId,
    Guid CustomerId,
    string? Reason) : DomainEvent;
```

Se existir apenas `Id` (não `BookingId`), adaptar o handler para usar o nome correto da propriedade.

- [ ] **Step 6: Rodar os testes**

```
dotnet test tests/Horafy.Application.Tests --filter "BookingCancelledEventHandlerTests" -v minimal
```
Expected: 2 passed

- [ ] **Step 7: Build + commit**

```
dotnet build Horafy.sln -c Debug

git add src/Horafy.Application/Features/Waitlist/EventHandlers/BookingCancelledEventHandler.cs
git add tests/Horafy.Application.Tests/Waitlist/BookingCancelledEventHandlerTests.cs
git commit -m "feat: promote first waitlist entry when booking is cancelled"
```

---

## Task 9: Multi-Serviço por Agendamento (BookingService)

**Files:**
- Create: `src/Horafy.Domain/Entities/Bookings/BookingService.cs`
- Modify: `src/Horafy.Domain/Entities/Bookings/Booking.cs`
- Create: `src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingServiceEntityConfiguration.cs`
- Modify: `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs`
- Modify: `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs`
- Modify: `src/Horafy.Application/Features/Bookings/Commands/CreateBookingCommand.cs`
- Modify: `src/Horafy.Application/Features/Bookings/Queries/GetBookingsQuery.cs`
- Modify: `src/Horafy.API/Controllers/V1/BookingsController.cs`
- Modify: `tests/Horafy.Application.Tests/Bookings/CreateBookingCommandHandlerTests.cs`

- [ ] **Step 1: Criar BookingService child entity**

```csharp
// src/Horafy.Domain/Entities/Bookings/BookingService.cs
namespace Horafy.Domain.Entities.Bookings;

public sealed class BookingService
{
    private BookingService() { }

    public Guid   Id              { get; private set; } = Guid.NewGuid();
    public Guid   BookingId       { get; private set; }
    public Guid   ServiceId       { get; private set; }
    public string ServiceName     { get; private set; } = default!;
    public int    DurationMinutes { get; private set; }

    internal static BookingService Create(
        Guid bookingId, Guid serviceId, string serviceName, int durationMinutes) =>
        new()
        {
            BookingId       = bookingId,
            ServiceId       = serviceId,
            ServiceName     = serviceName.Trim(),
            DurationMinutes = durationMinutes
        };
}
```

- [ ] **Step 2: Atualizar Booking para suportar múltiplos serviços**

Substituir o `Booking.cs` completo:

```csharp
// src/Horafy.Domain/Entities/Bookings/Booking.cs
using Horafy.Domain.Entities.Base;
using Horafy.Domain.Events.Bookings;

namespace Horafy.Domain.Entities.Bookings;

public sealed class Booking : BaseEntity
{
    private Booking() { }
    private readonly List<BookingService> _services = [];

    public Guid ServiceId  { get; private set; }   // serviço primário (primeiro)
    public Guid ResourceId { get; private set; }
    public Guid CustomerId { get; private set; }

    public string CustomerName  { get; private set; } = default!;
    public string CustomerEmail { get; private set; } = default!;

    public DateTimeOffset ScheduledAt     { get; private set; }
    public DateTimeOffset EndsAt          { get; private set; }
    public int            DurationMinutes { get; private set; }

    public string? Notes { get; private set; }

    public BookingStatus Status             { get; private set; } = BookingStatus.Pending;
    public string?       CancellationReason { get; private set; }

    public DateTimeOffset? ConfirmedAt  { get; private set; }
    public DateTimeOffset? CancelledAt  { get; private set; }
    public DateTimeOffset? CompletedAt  { get; private set; }

    public Guid?           RecurrenceGroupId { get; private set; }
    public DateTimeOffset? ExpiresAt         { get; private set; }

    public IReadOnlyList<BookingService> Services => _services.AsReadOnly();

    public static Booking Create(
        IReadOnlyList<(Guid ServiceId, string ServiceName, int DurationMinutes)> services,
        Guid resourceId,
        Guid customerId,
        string customerName,
        string customerEmail,
        DateTimeOffset scheduledAt,
        string? notes = null,
        Guid? recurrenceGroupId = null,
        DateTimeOffset? expiresAt = null)
    {
        if (services.Count == 0)
            throw new ArgumentException("Pelo menos um serviço é obrigatório.", nameof(services));

        if (scheduledAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException("A data do agendamento deve ser futura.", nameof(scheduledAt));

        var totalDuration = services.Sum(s => s.DurationMinutes);

        if (totalDuration <= 0)
            throw new ArgumentException("Duração total deve ser maior que zero.", nameof(services));

        var booking = new Booking
        {
            ServiceId         = services[0].ServiceId,
            ResourceId        = resourceId,
            CustomerId        = customerId,
            CustomerName      = customerName.Trim(),
            CustomerEmail     = customerEmail.ToLowerInvariant().Trim(),
            ScheduledAt       = scheduledAt,
            EndsAt            = scheduledAt.AddMinutes(totalDuration),
            DurationMinutes   = totalDuration,
            Notes             = notes?.Trim(),
            RecurrenceGroupId = recurrenceGroupId,
            ExpiresAt         = expiresAt
        };

        foreach (var svc in services)
            booking._services.Add(BookingService.Create(booking.Id, svc.ServiceId, svc.ServiceName, svc.DurationMinutes));

        booking.RaiseDomainEvent(new BookingCreatedEvent(
            booking.Id, booking.ServiceId, resourceId, customerId, scheduledAt));

        return booking;
    }

    // Overload para compatibilidade com código de recorrência (single-service)
    public static Booking Create(
        Guid serviceId,
        Guid resourceId,
        Guid customerId,
        string customerName,
        string customerEmail,
        DateTimeOffset scheduledAt,
        int durationMinutes,
        string? notes = null,
        Guid? recurrenceGroupId = null,
        DateTimeOffset? expiresAt = null)
    {
        return Create(
            new[] { (serviceId, serviceName: serviceId.ToString(), durationMinutes) },
            resourceId, customerId, customerName, customerEmail,
            scheduledAt, notes, recurrenceGroupId, expiresAt);
    }

    public void Confirm()
    {
        if (Status != BookingStatus.Pending)
            throw new InvalidOperationException($"Não é possível confirmar um agendamento no status {Status}.");

        Status      = BookingStatus.Confirmed;
        ConfirmedAt = DateTimeOffset.UtcNow;
        UpdatedAt   = DateTimeOffset.UtcNow;
    }

    public void Cancel(string? reason = null)
    {
        if (Status is BookingStatus.Completed or BookingStatus.Cancelled)
            throw new InvalidOperationException($"Não é possível cancelar um agendamento no status {Status}.");

        Status             = BookingStatus.Cancelled;
        CancellationReason = reason?.Trim();
        CancelledAt        = DateTimeOffset.UtcNow;
        UpdatedAt          = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new BookingCancelledEvent(Id, CustomerId, reason));
    }

    public void Complete()
    {
        if (Status != BookingStatus.Confirmed)
            throw new InvalidOperationException("Apenas agendamentos confirmados podem ser concluídos.");

        Status      = BookingStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        UpdatedAt   = DateTimeOffset.UtcNow;
    }

    public void MarkNoShow()
    {
        if (Status != BookingStatus.Confirmed)
            throw new InvalidOperationException("Apenas agendamentos confirmados podem ser marcados como no-show.");

        Status    = BookingStatus.NoShow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool OverlapsWith(DateTimeOffset start, DateTimeOffset end) =>
        Status is not (BookingStatus.Cancelled or BookingStatus.NoShow)
        && (ExpiresAt is null || ExpiresAt > DateTimeOffset.UtcNow)
        && ScheduledAt < end
        && EndsAt > start;
}
```

- [ ] **Step 3: Adicionar booking_services ao DDL**

Em `TenantSchemaService.cs`, adicionar após a tabela `bookings`:

```sql
        -- ── Serviços por Agendamento ───────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.booking_services (
            id               UUID         NOT NULL DEFAULT gen_random_uuid(),
            booking_id       UUID         NOT NULL,
            service_id       UUID         NOT NULL,
            service_name     VARCHAR(200) NOT NULL,
            duration_minutes INT          NOT NULL,
            CONSTRAINT pk_booking_services PRIMARY KEY (id),
            CONSTRAINT fk_booking_services_booking
                FOREIGN KEY (booking_id) REFERENCES {s}.bookings (id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS ix_booking_services_booking
            ON {s}.booking_services (booking_id);
```

- [ ] **Step 4: Criar BookingServiceEntityConfiguration**

```csharp
// src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingServiceEntityConfiguration.cs
using Horafy.Domain.Entities.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class BookingServiceEntityConfiguration : IEntityTypeConfiguration<BookingService>
{
    public void Configure(EntityTypeBuilder<BookingService> builder)
    {
        builder.ToTable("booking_services");

        builder.HasKey(bs => bs.Id);

        builder.Property(bs => bs.ServiceName).IsRequired().HasMaxLength(200);

        builder.HasIndex(bs => bs.BookingId)
            .HasDatabaseName("ix_booking_services_booking");
    }
}
```

- [ ] **Step 5: Adicionar BookingService ao TenantDbContext**

Em `TenantDbContext.cs`, adicionar:

```csharp
    public DbSet<BookingService> BookingServices => Set<BookingService>();
```

E em `OnModelCreating`, após `ApplyConfigurationsFromAssembly`, adicionar a relação:

```csharp
        modelBuilder.Entity<Booking>()
            .HasMany(b => b.Services)
            .WithOne()
            .HasForeignKey(bs => bs.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
```

- [ ] **Step 6: Atualizar CreateBookingCommand para aceitar múltiplos serviços**

```csharp
// src/Horafy.Application/Features/Bookings/Commands/CreateBookingCommand.cs
using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Commands;

public sealed record CreateBookingCommand(
    IReadOnlyList<Guid> ServiceIds,
    Guid ResourceId,
    DateTimeOffset ScheduledAt,
    string? Notes) : IRequest<Result<Guid>>;

public sealed class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
    {
        RuleFor(x => x.ServiceIds).NotEmpty().WithMessage("Pelo menos um serviço é obrigatório.");
        RuleForEach(x => x.ServiceIds).NotEmpty();
        RuleFor(x => x.ResourceId).NotEmpty();
        RuleFor(x => x.ScheduledAt)
            .GreaterThan(DateTimeOffset.UtcNow)
            .WithMessage("O horário deve ser futuro.");
    }
}

internal sealed class CreateBookingCommandHandler(
    IServiceRepository serviceRepository,
    IResourceRepository resourceRepository,
    IBookingRepository bookingRepository,
    ICurrentUserService currentUser,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CreateBookingCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateBookingCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
            return Result.Failure<Guid>(Error.Unauthorized);

        var resource = await resourceRepository.GetByIdAsync(request.ResourceId, cancellationToken);
        if (resource is null) return Result.Failure<Guid>(BookingErrors.ResourceNotFound);

        var services = new List<(Guid ServiceId, string ServiceName, int DurationMinutes)>();

        foreach (var serviceId in request.ServiceIds)
        {
            var service = await serviceRepository.GetByIdAsync(serviceId, cancellationToken);
            if (service is null) return Result.Failure<Guid>(BookingErrors.ServiceNotFound);
            services.Add((service.Id, service.Name, service.DurationMinutes));
        }

        var totalDuration = services.Sum(s => s.DurationMinutes);
        var endsAt = request.ScheduledAt.AddMinutes(totalDuration);

        var hasConflict = await bookingRepository.HasConflictAsync(
            request.ResourceId, request.ScheduledAt, endsAt,
            cancellationToken: cancellationToken);

        if (hasConflict) return Result.Failure<Guid>(BookingErrors.Conflict);

        var booking = Booking.Create(
            services,
            request.ResourceId,
            customerId:    currentUser.UserId.Value,
            customerName:  currentUser.Email ?? "Cliente",
            customerEmail: currentUser.Email ?? string.Empty,
            scheduledAt:   request.ScheduledAt,
            notes:         request.Notes);

        bookingRepository.Add(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(booking.Id);
    }
}
```

- [ ] **Step 7: Atualizar BookingResult DTO e GetBookingsQuery**

Em `src/Horafy.Application/Features/Bookings/Queries/GetBookingsQuery.cs`, atualizar o record `BookingResult` para incluir os serviços:

```csharp
public sealed record BookingServiceResult(Guid ServiceId, string ServiceName, int DurationMinutes);

public sealed record BookingResult(
    Guid Id,
    Guid ServiceId,
    Guid ResourceId,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    DateTimeOffset ScheduledAt,
    DateTimeOffset EndsAt,
    int DurationMinutes,
    string? Notes,
    BookingStatus Status,
    string? CancellationReason,
    Guid? RecurrenceGroupId,
    IReadOnlyList<BookingServiceResult> Services);
```

Atualizar o método `ToResult` em `GetBookingsQueryHandler`:

```csharp
    private static BookingResult ToResult(Domain.Entities.Bookings.Booking b) => new(
        b.Id, b.ServiceId, b.ResourceId, b.CustomerId,
        b.CustomerName, b.CustomerEmail, b.ScheduledAt, b.EndsAt,
        b.DurationMinutes, b.Notes, b.Status, b.CancellationReason,
        b.RecurrenceGroupId,
        b.Services.Select(s => new BookingServiceResult(s.ServiceId, s.ServiceName, s.DurationMinutes)).ToList());
```

Atualizar também `GetBookingByIdQueryHandler.cs`:

```csharp
        return Result.Success(new BookingResult(
            b.Id, b.ServiceId, b.ResourceId, b.CustomerId,
            b.CustomerName, b.CustomerEmail, b.ScheduledAt, b.EndsAt,
            b.DurationMinutes, b.Notes, b.Status, b.CancellationReason,
            b.RecurrenceGroupId,
            b.Services.Select(s => new BookingServiceResult(s.ServiceId, s.ServiceName, s.DurationMinutes)).ToList()));
```

- [ ] **Step 8: Atualizar BookingsController para multi-serviço**

Alterar `CreateBookingRequest` e o endpoint Create:

```csharp
public sealed record CreateBookingRequest(
    IReadOnlyList<Guid> ServiceIds,
    Guid ResourceId,
    DateTimeOffset ScheduledAt,
    string? Notes);
```

E no método `Create`:

```csharp
        var result = await Sender.Send(
            new CreateBookingCommand(
                request.ServiceIds, request.ResourceId,
                request.ScheduledAt, request.Notes),
            cancellationToken);
```

- [ ] **Step 9: Atualizar os testes existentes de CreateBooking**

Em `tests/Horafy.Application.Tests/Bookings/CreateBookingCommandHandlerTests.cs`, atualizar todos os `new CreateBookingCommand(service.Id, ...)` para `new CreateBookingCommand(new[] { service.Id }, ...)`:

```csharp
    // Em Handle_ValidRequest_ReturnsBookingId:
    var result = await CreateHandler().Handle(
        new CreateBookingCommand(new[] { service.Id }, resource.Id, scheduled, null), default);

    // Em Handle_ServiceNotFound_ReturnsError:
    var result = await CreateHandler().Handle(
        new CreateBookingCommand(new[] { Guid.NewGuid() }, Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddHours(1), null), default);

    // Em Handle_ResourceNotFound_ReturnsError:
    var result = await CreateHandler().Handle(
        new CreateBookingCommand(new[] { service.Id }, Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddHours(1), null), default);

    // Em Handle_TimeConflict_ReturnsConflictError:
    var result = await CreateHandler().Handle(
        new CreateBookingCommand(new[] { service.Id }, resource.Id, scheduled, null), default);

    // Em Handle_NotAuthenticated_ReturnsUnauthorized:
    var result = await CreateHandler().Handle(
        new CreateBookingCommand(new[] { Guid.NewGuid() }, Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddHours(1), null), default);
```

Também atualizar os `_bookingRepo.Setup(r => r.HasConflictAsync(...))` para usar `scheduled.AddMinutes(60)` (já correto).

Atualizar `MakeService()` para incluir `Name`:

```csharp
    private static Service MakeService() =>
        Service.Create("Corte", 60, 50m);
```

Verificar que `Service.Create` retorna um objeto com `.Name` e `.DurationMinutes`. Se o handler agora faz `service.Name`, verificar o modelo de `Service`.

- [ ] **Step 10: Rodar todos os testes**

```
dotnet test tests/ -v minimal
```
Expected: todos passed

- [ ] **Step 11: Build + commit**

```
dotnet build Horafy.sln -c Debug

git add src/Horafy.Domain/Entities/Bookings/BookingService.cs
git add src/Horafy.Domain/Entities/Bookings/Booking.cs
git add src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingServiceEntityConfiguration.cs
git add src/Horafy.Infrastructure/Persistence/TenantDbContext.cs
git add src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs
git add src/Horafy.Application/Features/Bookings/Commands/CreateBookingCommand.cs
git add src/Horafy.Application/Features/Bookings/Queries/GetBookingsQuery.cs
git add src/Horafy.Application/Features/Bookings/Queries/GetBookingByIdQuery.cs
git add src/Horafy.API/Controllers/V1/BookingsController.cs
git add tests/Horafy.Application.Tests/Bookings/CreateBookingCommandHandlerTests.cs
git commit -m "feat: multi-service bookings with BookingService child entity"
```

---

## Self-Review

### 1. Spec Coverage

| Requisito (spec §6.4) | Task |
|---|---|
| Fluxo completo: Confirm, Cancel | existente ✓ |
| CompleteBooking, NoShow | Task 1 ✓ |
| GetMyBookings (customer) | Task 2 ✓ |
| Política de cancelamento por tenant | Task 3 ✓ |
| Pré-agendamento com prazo (ExpiresAt) | Task 4 ✓ (ExpiresAt no Create) |
| Recorrência semanal/quinzenal/mensal | Tasks 4+5 ✓ |
| Fila de espera — join/leave | Tasks 6+7 ✓ |
| Fila de espera — promoção automática | Task 8 ✓ |
| Múltiplos serviços por agendamento | Task 9 ✓ |

### 2. Verificação de Tipos

- `Booking.Create(IReadOnlyList<(Guid, string, int)>, ...)` → usado em `CreateBookingCommand` handler (Task 9) ✓
- `Booking.Create(Guid serviceId, ..., int durationMinutes, ...)` overload → usado em `CreateRecurringBookingCommand` (Task 5) ✓
- `BookingCancelledEvent.BookingId` → usado no handler de waitlist (Task 8); verificar que o event tem essa propriedade (Task 8 step 5) ✓
- `RecurrenceFrequency` enum importado no `CreateRecurringBookingCommand` e no controller ✓
- `BookingResult` adicionado `RecurrenceGroupId` e `Services` → `GetMyBookingsQuery` retorna `BookingResult`, precisa ser atualizado para usar o novo DTO ✓

### 3. Dependência em GetMyBookingsQuery

`GetMyBookingsQuery` (Task 2) usa `BookingResult` com a assinatura antiga (sem `RecurrenceGroupId` e `Services`). Após Task 9, o DTO muda. Atualizar o `ToResult` inline do handler em Task 2 para incluir os novos campos — ou, se Task 2 for implementada antes de Task 9, a compilação vai falhar em Task 9. **Solução**: implementar Task 9 primeiro, ou atualizar o `GetMyBookingsQuery` handler em Task 9 junto com `GetBookingsQuery`.

O plano já inclui atualizar `GetBookingByIdQuery` na Task 9 step 7. Adicionar também `GetMyBookingsQuery`.
