# Sprint 15 — Programa de Fidelidade + Cancelamento no Portal

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar o programa de fidelidade (créditos automáticos ao concluir um agendamento) e o cancelamento self-service pelo cliente no portal, fechando o ciclo de vida completo do agendamento.

**Architecture:** `Booking.Complete()` passa a disparar `BookingCompletedEvent`; um novo `BookingCompletedEventHandler` carrega as configurações de fidelidade do tenant, calcula o bônus (`amount * ratePercent / 100`) e credita a wallet do cliente via `Wallet.AddLoyaltyBonus()`. O cancelamento pelo portal usa o `CancelBookingCommand` já existente no backend — falta apenas a UI no portal e o endpoint de configuração da política de cancelamento para o admin. `LoyaltySettings` e `CancellationPolicy` ficam como OwnsOne no tenant (schema `public` → `HorafyDbContext`).

**Tech Stack:** .NET 8 + MediatR + EF Core (backend), Next.js 16 + shadcn/ui (frontend), xUnit + Moq + FluentAssertions (testes).

---

## File Map

```
# Backend — Domain (modify)
src/Horafy.Domain/Events/Bookings/BookingCompletedEvent.cs             (create)
src/Horafy.Domain/Entities/Bookings/Booking.cs                         (modify — Complete() raises event)
src/Horafy.Domain/Entities/Wallet/Wallet.cs                            (modify — AddLoyaltyBonus())
src/Horafy.Domain/Entities/Tenants/LoyaltySettings.cs                  (create)
src/Horafy.Domain/Entities/Tenants/Tenant.cs                           (modify — add LoyaltySettings prop + UpdateLoyaltySettings())

# Backend — Application (create)
src/Horafy.Application/Features/Bookings/EventHandlers/BookingCompletedEventHandler.cs
src/Horafy.Application/Features/Tenants/Commands/UpdateLoyaltySettings/UpdateLoyaltySettingsCommand.cs
src/Horafy.Application/Features/Tenants/Commands/UpdateCancellationPolicy/UpdateCancellationPolicyCommand.cs
src/Horafy.Application/Features/Tenants/Queries/GetCurrentTenant/GetCurrentTenantQuery.cs   (modify — add fields to TenantResult)

# Backend — Tests (create)
tests/Horafy.Application.Tests/Wallet/WalletLoyaltyBonusTests.cs
tests/Horafy.Application.Tests/Bookings/BookingCompletedEventHandlerTests.cs
tests/Horafy.Application.Tests/Tenants/UpdateLoyaltySettingsCommandHandlerTests.cs
tests/Horafy.Application.Tests/Tenants/UpdateCancellationPolicyCommandHandlerTests.cs

# Backend — Infrastructure (modify)
src/Horafy.Infrastructure/Persistence/Configurations/TenantEntityConfiguration.cs
src/Horafy.Infrastructure/Persistence/Migrations/<timestamp>_AddLoyaltySettings.cs   (generated)

# Backend — API (modify)
src/Horafy.API/Controllers/V1/TenantsController.cs

# Frontend (modify)
frontend/lib/types/tenant.ts
frontend/lib/api/tenants.ts
frontend/app/(admin)/admin/configuracoes/page.tsx
frontend/lib/api/portal.ts
frontend/app/(portal)/[slug]/minha-conta/page.tsx
frontend/__tests__/LoyaltySettings.test.tsx   (create)
frontend/__tests__/PortalCancel.test.tsx       (create)
```

---

### Task 1: Domain — BookingCompletedEvent + Booking.Complete() → raises event

**Files:**
- Create: `src/Horafy.Domain/Events/Bookings/BookingCompletedEvent.cs`
- Modify: `src/Horafy.Domain/Entities/Bookings/Booking.cs`

- [ ] **Step 1: Criar o evento**

```csharp
// src/Horafy.Domain/Events/Bookings/BookingCompletedEvent.cs
using Horafy.Domain.Events.Base;

namespace Horafy.Domain.Events.Bookings;

public sealed record BookingCompletedEvent(
    Guid BookingId,
    Guid CustomerId) : DomainEvent;
```

- [ ] **Step 2: Modificar `Booking.Complete()` para disparar o evento**

Localizar o método `Complete()` em `Booking.cs` (linhas 128-136). Substituir:

```csharp
    public void Complete()
    {
        if (Status != BookingStatus.Confirmed)
            throw new InvalidOperationException("Apenas agendamentos confirmados podem ser concluídos.");

        Status      = BookingStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        UpdatedAt   = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new BookingCompletedEvent(Id, CustomerId));
    }
```

- [ ] **Step 3: Build do Domain**

```powershell
cd C:\Projetos\JEL\JEL\Horafy
dotnet build src/Horafy.Shared/Horafy.Shared.csproj -q 2>&1 | Select-Object -Last 3
dotnet build src/Horafy.Domain/Horafy.Domain.csproj --no-dependencies -q 2>&1 | Select-Object -Last 3
```

Expected: `0 Erro(s)` em ambos.

- [ ] **Step 4: Commit**

```powershell
git add src/Horafy.Domain/Events/Bookings/BookingCompletedEvent.cs src/Horafy.Domain/Entities/Bookings/Booking.cs
git commit -m "feat: raise BookingCompletedEvent when booking is completed"
```

---

### Task 2: Domain — LoyaltySettings value object + Wallet.AddLoyaltyBonus()

**Files:**
- Create: `src/Horafy.Domain/Entities/Tenants/LoyaltySettings.cs`
- Modify: `src/Horafy.Domain/Entities/Tenants/Tenant.cs`
- Modify: `src/Horafy.Domain/Entities/Wallet/Wallet.cs`
- Create: `tests/Horafy.Application.Tests/Wallet/WalletLoyaltyBonusTests.cs`

- [ ] **Step 1: Escrever os testes de `AddLoyaltyBonus`**

```csharp
// tests/Horafy.Application.Tests/Wallet/WalletLoyaltyBonusTests.cs
using FluentAssertions;
using Horafy.Domain.Entities.Wallet;
using WalletEntity = Horafy.Domain.Entities.Wallet.Wallet;
using Xunit;

namespace Horafy.Application.Tests.Wallet;

public sealed class WalletLoyaltyBonusTests
{
    private static WalletEntity MakeWallet(decimal balance = 0)
    {
        var w = WalletEntity.Create(Guid.NewGuid());
        if (balance > 0) w.AddCredits(balance, "Setup");
        return w;
    }

    [Fact]
    public void AddLoyaltyBonus_ValidAmount_IncreasesBalanceWithLoyaltyType()
    {
        var wallet    = MakeWallet();
        var bookingId = Guid.NewGuid();

        var result = wallet.AddLoyaltyBonus(5m, "Bônus de fidelidade", bookingId);

        result.IsSuccess.Should().BeTrue();
        wallet.Balance.Should().Be(5m);
        wallet.Transactions.Should().HaveCount(1);
        wallet.Transactions[0].Type.Should().Be(WalletTransactionType.LoyaltyBonus);
        wallet.Transactions[0].Amount.Should().Be(5m);
        wallet.Transactions[0].BookingId.Should().Be(bookingId);
    }

    [Fact]
    public void AddLoyaltyBonus_ZeroAmount_ReturnsFailure()
    {
        var wallet = MakeWallet();

        var result = wallet.AddLoyaltyBonus(0m, "Bônus", Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Wallet.InvalidAmount");
        wallet.Balance.Should().Be(0m);
    }
}
```

- [ ] **Step 2: Executar para verificar FAIL**

```powershell
cd C:\Projetos\JEL\JEL\Horafy
dotnet test tests/Horafy.Application.Tests --filter "WalletLoyaltyBonusTests" 2>&1 | Select-Object -Last 5
```

Expected: FAIL — `AddLoyaltyBonus` não existe ainda.

- [ ] **Step 3: Implementar `AddLoyaltyBonus` em `Wallet.cs`**

Abrir `src/Horafy.Domain/Entities/Wallet/Wallet.cs`. Adicionar após o método `DebitPayment`:

```csharp
    public Result AddLoyaltyBonus(decimal amount, string description, Guid bookingId)
    {
        if (amount <= 0) return Result.Failure(WalletErrors.InvalidAmount);
        Balance += amount;
        _transactions.Add(WalletTransaction.Create(
            Id, WalletTransactionType.LoyaltyBonus, amount, description, bookingId));
        return Result.Success();
    }
```

- [ ] **Step 4: Criar `LoyaltySettings.cs`**

```csharp
// src/Horafy.Domain/Entities/Tenants/LoyaltySettings.cs
namespace Horafy.Domain.Entities.Tenants;

public sealed class LoyaltySettings
{
    private LoyaltySettings() { }

    public bool    IsEnabled         { get; private set; }
    public decimal CreditRatePercent { get; private set; } // 0–100: % do valor pago vira crédito
    public decimal MinBookingAmount  { get; private set; } // valor mínimo para ganhar pontos

    public static readonly LoyaltySettings Default =
        new() { IsEnabled = false, CreditRatePercent = 0, MinBookingAmount = 0 };

    public static LoyaltySettings Create(bool isEnabled, decimal ratePercent, decimal minAmount)
    {
        if (ratePercent < 0 || ratePercent > 100)
            throw new ArgumentException("CreditRatePercent deve estar entre 0 e 100.", nameof(ratePercent));
        if (minAmount < 0)
            throw new ArgumentException("MinBookingAmount não pode ser negativo.", nameof(minAmount));
        return new() { IsEnabled = isEnabled, CreditRatePercent = ratePercent, MinBookingAmount = minAmount };
    }

    public decimal CalculateBonus(decimal bookingAmount)
    {
        if (!IsEnabled || bookingAmount < MinBookingAmount) return 0;
        return Math.Round(bookingAmount * CreditRatePercent / 100m, 2);
    }
}
```

- [ ] **Step 5: Modificar `Tenant.cs` para incluir `LoyaltySettings`**

Adicionar a propriedade após `PaymentSettings`:

```csharp
    public LoyaltySettings LoyaltySettings { get; private set; } = LoyaltySettings.Default;
```

Adicionar o método `UpdateLoyaltySettings` após `UpdatePaymentSettings`:

```csharp
    public void UpdateLoyaltySettings(bool isEnabled, decimal ratePercent, decimal minAmount)
    {
        LoyaltySettings = LoyaltySettings.Create(isEnabled, ratePercent, minAmount);
        UpdatedAt = DateTimeOffset.UtcNow;
    }
```

- [ ] **Step 6: Executar testes**

```powershell
dotnet test tests/Horafy.Application.Tests --filter "WalletLoyaltyBonusTests" 2>&1 | Select-Object -Last 5
```

Expected: `Aprovado! – Com falha: 0, Aprovado: 2`.

- [ ] **Step 7: Commit**

```powershell
git add src/Horafy.Domain/Entities/Tenants/LoyaltySettings.cs src/Horafy.Domain/Entities/Tenants/Tenant.cs src/Horafy.Domain/Entities/Wallet/Wallet.cs tests/Horafy.Application.Tests/Wallet/WalletLoyaltyBonusTests.cs
git commit -m "feat: add LoyaltySettings, Wallet.AddLoyaltyBonus() with tests"
```

---

### Task 3: Application — BookingCompletedEventHandler + testes

**Files:**
- Create: `src/Horafy.Application/Features/Bookings/EventHandlers/BookingCompletedEventHandler.cs`
- Create: `tests/Horafy.Application.Tests/Bookings/BookingCompletedEventHandlerTests.cs`

- [ ] **Step 1: Escrever os testes**

```csharp
// tests/Horafy.Application.Tests/Bookings/BookingCompletedEventHandlerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Bookings.EventHandlers;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using WalletEntity = Horafy.Domain.Entities.Wallet.Wallet;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Bookings;

public sealed class BookingCompletedEventHandlerTests
{
    private readonly Mock<ITenantRepository>     _tenantRepo  = new();
    private readonly Mock<IPaymentRepository>    _paymentRepo = new();
    private readonly Mock<IWalletRepository>     _walletRepo  = new();
    private readonly Mock<ICurrentTenantService> _tenantSvc   = new();
    private readonly Mock<ITenantUnitOfWork>     _uow         = new();

    private BookingCompletedEventHandler MakeHandler() => new(
        _tenantRepo.Object, _paymentRepo.Object,
        _walletRepo.Object, _tenantSvc.Object, _uow.Object);

    private static Tenant MakeTenantWithLoyalty(bool enabled, decimal rate = 10m, decimal min = 0m)
    {
        var t = Tenant.Create("T", "t", TenantVertical.Barbershop);
        t.UpdateLoyaltySettings(enabled, rate, min);
        return t;
    }

    [Fact]
    public async Task Handle_LoyaltyEnabled_AwardsWalletBonusToCustomer()
    {
        var tenantId  = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var tenant    = MakeTenantWithLoyalty(enabled: true, rate: 10m);

        // Payment with Amount=100 → bonus should be 10
        var payment = Payment.Create(bookingId, "pref", PaymentMethod.Pix, 100m, 0m);
        typeof(Payment).GetProperty(nameof(Payment.Status))!
            .SetValue(payment, PaymentStatus.Approved);

        _tenantSvc.Setup(t => t.TenantId).Returns(tenantId);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);
        _paymentRepo.Setup(r => r.GetByBookingIdAsync(bookingId, default)).ReturnsAsync(payment);
        _walletRepo.Setup(r => r.GetByUserIdAsync(customerId, default)).ReturnsAsync((WalletEntity?)null);

        await MakeHandler().Handle(
            new BookingCompletedEvent(bookingId, customerId), default);

        _walletRepo.Verify(r => r.Add(It.Is<WalletEntity>(w =>
            w.UserId == customerId && w.Balance == 10m)), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_LoyaltyDisabled_DoesNotAwardBonus()
    {
        var tenantId  = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var tenant    = MakeTenantWithLoyalty(enabled: false);

        _tenantSvc.Setup(t => t.TenantId).Returns(tenantId);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        await MakeHandler().Handle(
            new BookingCompletedEvent(bookingId, Guid.NewGuid()), default);

        _paymentRepo.Verify(r => r.GetByBookingIdAsync(It.IsAny<Guid>(), default), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_PaymentNotFound_DoesNotAwardBonus()
    {
        var tenantId  = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var tenant    = MakeTenantWithLoyalty(enabled: true, rate: 10m);

        _tenantSvc.Setup(t => t.TenantId).Returns(tenantId);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);
        _paymentRepo.Setup(r => r.GetByBookingIdAsync(bookingId, default)).ReturnsAsync((Payment?)null);

        await MakeHandler().Handle(
            new BookingCompletedEvent(bookingId, Guid.NewGuid()), default);

        _walletRepo.Verify(r => r.Add(It.IsAny<WalletEntity>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }
}
```

**Nota:** A linha que seta `payment.Status` via reflection usa a propriedade pública `Status`. Se `Payment.Status` não tiver setter via reflection (porque tem `private set`), use o método `Approve()` passando um dummy `MpPaymentId`. Substitua:

```csharp
typeof(Payment).GetProperty(nameof(Payment.Status))!
    .SetValue(payment, PaymentStatus.Approved);
```

por:

```csharp
payment.Approve("mp-dummy-id");
```

- [ ] **Step 2: Executar para verificar FAIL**

```powershell
dotnet test tests/Horafy.Application.Tests --filter "BookingCompletedEventHandlerTests" 2>&1 | Select-Object -Last 5
```

Expected: FAIL — `BookingCompletedEventHandler` não existe.

- [ ] **Step 3: Implementar o handler**

```csharp
// src/Horafy.Application/Features/Bookings/EventHandlers/BookingCompletedEventHandler.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using MediatR;
using WalletEntity = Horafy.Domain.Entities.Wallet.Wallet;

namespace Horafy.Application.Features.Bookings.EventHandlers;

internal sealed class BookingCompletedEventHandler(
    ITenantRepository     tenantRepository,
    IPaymentRepository    paymentRepository,
    IWalletRepository     walletRepository,
    ICurrentTenantService currentTenant,
    ITenantUnitOfWork     unitOfWork)
    : INotificationHandler<BookingCompletedEvent>
{
    public async Task Handle(BookingCompletedEvent notification, CancellationToken cancellationToken)
    {
        if (!currentTenant.TenantId.HasValue) return;

        var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
        if (tenant is null || !tenant.LoyaltySettings.IsEnabled) return;

        var payment = await paymentRepository.GetByBookingIdAsync(notification.BookingId, cancellationToken);
        if (payment?.Status != PaymentStatus.Approved) return;

        var bonus = tenant.LoyaltySettings.CalculateBonus(payment.Amount);
        if (bonus <= 0) return;

        var wallet  = await walletRepository.GetByUserIdAsync(notification.CustomerId, cancellationToken);
        var isNew   = wallet is null;

        if (isNew)
            wallet = WalletEntity.Create(notification.CustomerId);

        wallet!.AddLoyaltyBonus(
            bonus,
            $"Bônus de fidelidade — #{notification.BookingId.ToString()[..8]}",
            notification.BookingId);

        if (isNew)
            walletRepository.Add(wallet);
        else
            walletRepository.Update(wallet);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: Executar testes**

```powershell
dotnet test tests/Horafy.Application.Tests --filter "BookingCompletedEventHandlerTests" 2>&1 | Select-Object -Last 5
```

Expected: `Aprovado! – Com falha: 0, Aprovado: 3`.

Se o teste `Handle_LoyaltyEnabled_AwardsWalletBonusToCustomer` falhar com reflection, substituir a criação do payment pelo uso de `payment.Approve("mp-dummy-id")`.

- [ ] **Step 5: Commit**

```powershell
git add src/Horafy.Application/Features/Bookings/EventHandlers/BookingCompletedEventHandler.cs tests/Horafy.Application.Tests/Bookings/BookingCompletedEventHandlerTests.cs
git commit -m "feat: add BookingCompletedEventHandler for loyalty bonus"
```

---

### Task 4: Application — UpdateLoyaltySettingsCommand + UpdateCancellationPolicyCommand + testes

**Files:**
- Create: `src/Horafy.Application/Features/Tenants/Commands/UpdateLoyaltySettings/UpdateLoyaltySettingsCommand.cs`
- Create: `src/Horafy.Application/Features/Tenants/Commands/UpdateCancellationPolicy/UpdateCancellationPolicyCommand.cs`
- Create: `tests/Horafy.Application.Tests/Tenants/UpdateLoyaltySettingsCommandHandlerTests.cs`
- Create: `tests/Horafy.Application.Tests/Tenants/UpdateCancellationPolicyCommandHandlerTests.cs`

- [ ] **Step 1: Criar `UpdateLoyaltySettingsCommand.cs`**

```csharp
// src/Horafy.Application/Features/Tenants/Commands/UpdateLoyaltySettings/UpdateLoyaltySettingsCommand.cs
using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Commands.UpdateLoyaltySettings;

public sealed record UpdateLoyaltySettingsCommand(
    bool    IsEnabled,
    decimal CreditRatePercent,
    decimal MinBookingAmount) : IRequest<Result>;

public sealed class UpdateLoyaltySettingsCommandValidator : AbstractValidator<UpdateLoyaltySettingsCommand>
{
    public UpdateLoyaltySettingsCommandValidator()
    {
        RuleFor(x => x.CreditRatePercent)
            .GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
        RuleFor(x => x.MinBookingAmount)
            .GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateLoyaltySettingsCommandHandler(
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenant,
    IUnitOfWork           unitOfWork) : IRequestHandler<UpdateLoyaltySettingsCommand, Result>
{
    public async Task<Result> Handle(UpdateLoyaltySettingsCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.TenantId.HasValue)
            return Result.Failure(Error.Unauthorized);

        var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
        if (tenant is null) return Result.Failure(TenantErrors.NotFound);

        tenant.UpdateLoyaltySettings(request.IsEnabled, request.CreditRatePercent, request.MinBookingAmount);
        tenantRepository.Update(tenant);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 2: Criar `UpdateCancellationPolicyCommand.cs`**

```csharp
// src/Horafy.Application/Features/Tenants/Commands/UpdateCancellationPolicy/UpdateCancellationPolicyCommand.cs
using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Commands.UpdateCancellationPolicy;

public sealed record UpdateCancellationPolicyCommand(
    int     MinCancellationHours,
    decimal CancellationFeePercent,
    bool    AllowCustomerCancellation) : IRequest<Result>;

public sealed class UpdateCancellationPolicyCommandValidator : AbstractValidator<UpdateCancellationPolicyCommand>
{
    public UpdateCancellationPolicyCommandValidator()
    {
        RuleFor(x => x.MinCancellationHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CancellationFeePercent)
            .GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
    }
}

internal sealed class UpdateCancellationPolicyCommandHandler(
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenant,
    IUnitOfWork           unitOfWork) : IRequestHandler<UpdateCancellationPolicyCommand, Result>
{
    public async Task<Result> Handle(UpdateCancellationPolicyCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.TenantId.HasValue)
            return Result.Failure(Error.Unauthorized);

        var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
        if (tenant is null) return Result.Failure(TenantErrors.NotFound);

        tenant.UpdateCancellationPolicy(
            request.MinCancellationHours,
            request.CancellationFeePercent,
            request.AllowCustomerCancellation);
        tenantRepository.Update(tenant);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 3: Criar testes dos dois commands**

```csharp
// tests/Horafy.Application.Tests/Tenants/UpdateLoyaltySettingsCommandHandlerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Tenants.Commands.UpdateLoyaltySettings;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Tenants;

public sealed class UpdateLoyaltySettingsCommandHandlerTests
{
    private readonly Mock<ITenantRepository>     _repo       = new();
    private readonly Mock<ICurrentTenantService> _tenantSvc  = new();
    private readonly Mock<IUnitOfWork>           _uow        = new();

    private UpdateLoyaltySettingsCommandHandler MakeHandler() =>
        new(_repo.Object, _tenantSvc.Object, _uow.Object);

    [Fact]
    public async Task Handle_ValidSettings_UpdatesLoyaltyAndSaves()
    {
        var tenantId = Guid.NewGuid();
        var tenant   = Tenant.Create("T", "t", TenantVertical.Barbershop);

        _tenantSvc.Setup(s => s.TenantId).Returns(tenantId);
        _repo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        var result = await MakeHandler().Handle(
            new UpdateLoyaltySettingsCommand(true, 10m, 50m), default);

        result.IsSuccess.Should().BeTrue();
        tenant.LoyaltySettings.IsEnabled.Should().BeTrue();
        tenant.LoyaltySettings.CreditRatePercent.Should().Be(10m);
        tenant.LoyaltySettings.MinBookingAmount.Should().Be(50m);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_NoTenantContext_ReturnsUnauthorized()
    {
        _tenantSvc.Setup(s => s.TenantId).Returns((Guid?)null);

        var result = await MakeHandler().Handle(
            new UpdateLoyaltySettingsCommand(true, 10m, 0m), default);

        result.IsFailure.Should().BeTrue();
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }
}
```

```csharp
// tests/Horafy.Application.Tests/Tenants/UpdateCancellationPolicyCommandHandlerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Tenants.Commands.UpdateCancellationPolicy;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Tenants;

public sealed class UpdateCancellationPolicyCommandHandlerTests
{
    private readonly Mock<ITenantRepository>     _repo      = new();
    private readonly Mock<ICurrentTenantService> _tenantSvc = new();
    private readonly Mock<IUnitOfWork>           _uow       = new();

    private UpdateCancellationPolicyCommandHandler MakeHandler() =>
        new(_repo.Object, _tenantSvc.Object, _uow.Object);

    [Fact]
    public async Task Handle_ValidPolicy_UpdatesCancellationPolicyAndSaves()
    {
        var tenantId = Guid.NewGuid();
        var tenant   = Tenant.Create("T", "t", TenantVertical.Barbershop);

        _tenantSvc.Setup(s => s.TenantId).Returns(tenantId);
        _repo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        var result = await MakeHandler().Handle(
            new UpdateCancellationPolicyCommand(24, 20m, false), default);

        result.IsSuccess.Should().BeTrue();
        tenant.CancellationPolicy.MinCancellationHours.Should().Be(24);
        tenant.CancellationPolicy.CancellationFeePercent.Should().Be(20m);
        tenant.CancellationPolicy.AllowCustomerCancellation.Should().BeFalse();
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }
}
```

- [ ] **Step 4: Executar testes**

```powershell
dotnet test tests/Horafy.Application.Tests --filter "UpdateLoyaltySettingsCommandHandlerTests|UpdateCancellationPolicyCommandHandlerTests" 2>&1 | Select-Object -Last 5
```

Expected: `Aprovado! – Com falha: 0, Aprovado: 3`.

- [ ] **Step 5: Atualizar `TenantResult` para incluir os novos campos**

Abrir `src/Horafy.Application/Features/Tenants/Queries/GetCurrentTenant/GetCurrentTenantQuery.cs`.

Adicionar dois novos records após `TenantThemeResult`:

```csharp
public sealed record CancellationPolicyResult(
    int     MinCancellationHours,
    decimal CancellationFeePercent,
    bool    AllowCustomerCancellation);

public sealed record LoyaltySettingsResult(
    bool    IsEnabled,
    decimal CreditRatePercent,
    decimal MinBookingAmount);
```

Adicionar os campos ao final do record `TenantResult` (manter todos os existentes, acrescentar):

```csharp
public sealed record TenantResult(
    Guid   Id,
    string Name,
    string Slug,
    string? CustomDomain,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? ZipCode,
    string  TimeZoneId,
    string  Locale,
    TenantStatus Status,
    TenantPlan   Plan,
    TenantVertical Vertical,
    TenantThemeResult Theme,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset? PlanRenewsAt,
    CancellationPolicyResult CancellationPolicy,
    LoyaltySettingsResult    LoyaltySettings);
```

Atualizar o método `ToResult` para mapear os novos campos (adicionar no final do construtor):

```csharp
    internal static TenantResult ToResult(Domain.Entities.Tenants.Tenant t) => new(
        t.Id, t.Name, t.Slug, t.CustomDomain,
        t.Email, t.Phone, t.Address, t.City, t.State, t.ZipCode,
        t.TimeZoneId, t.Locale, t.Status, t.Plan, t.Vertical,
        new TenantThemeResult(
            t.Theme.PrimaryColor, t.Theme.SecondaryColor,
            t.Theme.BackgroundColor, t.Theme.TextColor, t.Theme.FontFamily,
            t.Theme.LogoUrl, t.Theme.FaviconUrl,
            t.Theme.BannerUrl, t.Theme.BannerText,
            t.Theme.ShowReviews, t.Theme.ShowTeam, t.Theme.ShowServicePrices,
            t.Theme.InstagramUrl, t.Theme.WhatsAppNumber, t.Theme.FacebookUrl,
            t.Theme.SectionsOrder),
        t.TrialEndsAt, t.PlanRenewsAt,
        new CancellationPolicyResult(
            t.CancellationPolicy.MinCancellationHours,
            t.CancellationPolicy.CancellationFeePercent,
            t.CancellationPolicy.AllowCustomerCancellation),
        new LoyaltySettingsResult(
            t.LoyaltySettings.IsEnabled,
            t.LoyaltySettings.CreditRatePercent,
            t.LoyaltySettings.MinBookingAmount));
```

- [ ] **Step 6: Executar todos os testes**

```powershell
dotnet test tests/Horafy.Application.Tests 2>&1 | Select-Object -Last 5
```

Expected: `Com falha: 0` — todos os 130+ testes passando.

- [ ] **Step 7: Commit**

```powershell
git add src/Horafy.Application/Features/Tenants/ tests/Horafy.Application.Tests/Tenants/UpdateLoyaltySettingsCommandHandlerTests.cs tests/Horafy.Application.Tests/Tenants/UpdateCancellationPolicyCommandHandlerTests.cs
git commit -m "feat: add UpdateLoyaltySettings and UpdateCancellationPolicy commands with TenantResult extensions"
```

---

### Task 5: Infrastructure — EF migration AddLoyaltySettings

**Files:**
- Modify: `src/Horafy.Infrastructure/Persistence/Configurations/TenantEntityConfiguration.cs`
- Generate: `src/Horafy.Infrastructure/Persistence/Migrations/<timestamp>_AddLoyaltySettings.cs`

- [ ] **Step 1: Adicionar OwnsOne LoyaltySettings em `TenantEntityConfiguration.cs`**

Abrir `src/Horafy.Infrastructure/Persistence/Configurations/TenantEntityConfiguration.cs`. Adicionar após o bloco `builder.OwnsOne(t => t.PaymentSettings, ...)`:

```csharp
        builder.OwnsOne(t => t.LoyaltySettings, ls =>
        {
            ls.Property(l => l.IsEnabled).HasDefaultValue(false);
            ls.Property(l => l.CreditRatePercent)
              .HasColumnType("numeric(5,2)").HasDefaultValue(0m);
            ls.Property(l => l.MinBookingAmount)
              .HasColumnType("numeric(10,2)").HasDefaultValue(0m);
        });
```

- [ ] **Step 2: Gerar a migration**

```powershell
cd C:\Projetos\JEL\JEL\Horafy
dotnet ef migrations add AddLoyaltySettings `
  --context HorafyDbContext `
  --project src/Horafy.Infrastructure `
  --startup-project src/Horafy.API `
  --output-dir Persistence/Migrations `
  2>&1 | Select-Object -Last 5
```

Expected: `Done. To undo this action, use 'ef migrations remove'`.

Se `Build failed`, executar os builds sequenciais antes:
```powershell
dotnet build src/Horafy.Shared/Horafy.Shared.csproj -q
dotnet build src/Horafy.Domain/Horafy.Domain.csproj --no-dependencies -q
dotnet build src/Horafy.Application/Horafy.Application.csproj --no-dependencies -q
dotnet build src/Horafy.Infrastructure/Horafy.Infrastructure.csproj --no-dependencies -q
dotnet build src/Horafy.API/Horafy.API.csproj --no-dependencies -q
```

- [ ] **Step 3: Verificar a migration gerada**

Abrir o arquivo de migration gerado em `src/Horafy.Infrastructure/Persistence/Migrations/`. Confirmar que contém:
- `loyalty_settings_is_enabled` (boolean)
- `loyalty_settings_credit_rate_percent` (numeric(5,2))
- `loyalty_settings_min_booking_amount` (numeric(10,2))

na tabela `tenants` do schema `public`.

- [ ] **Step 4: Commit**

```powershell
git add src/Horafy.Infrastructure/Persistence/Configurations/TenantEntityConfiguration.cs src/Horafy.Infrastructure/Persistence/Migrations/
git commit -m "feat: add EF migration AddLoyaltySettings to tenants table"
```

---

### Task 6: API — loyalty-settings + cancellation-policy endpoints

**Files:**
- Modify: `src/Horafy.API/Controllers/V1/TenantsController.cs`

- [ ] **Step 1: Adicionar os dois novos endpoints e DTOs em `TenantsController.cs`**

Adicionar os usings necessários no topo:

```csharp
using Horafy.Application.Features.Tenants.Commands.UpdateLoyaltySettings;
using Horafy.Application.Features.Tenants.Commands.UpdateCancellationPolicy;
```

Adicionar os endpoints após `UpdatePaymentSettings`:

```csharp
    /// <summary>Atualiza as configurações de fidelidade do tenant.</summary>
    [HttpPut("loyalty-settings")]
    [Authorize(Roles = "TenantOwner,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateLoyaltySettings(
        [FromBody] UpdateLoyaltySettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new UpdateLoyaltySettingsCommand(
                request.IsEnabled, request.CreditRatePercent, request.MinBookingAmount),
            cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    /// <summary>Atualiza a política de cancelamento do tenant.</summary>
    [HttpPut("cancellation-policy")]
    [Authorize(Roles = "TenantOwner,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateCancellationPolicy(
        [FromBody] UpdateCancellationPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new UpdateCancellationPolicyCommand(
                request.MinCancellationHours, request.CancellationFeePercent, request.AllowCustomerCancellation),
            cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
```

Adicionar os DTOs no final do arquivo:

```csharp
public sealed record UpdateLoyaltySettingsRequest(
    bool    IsEnabled,
    decimal CreditRatePercent,
    decimal MinBookingAmount);

public sealed record UpdateCancellationPolicyRequest(
    int     MinCancellationHours,
    decimal CancellationFeePercent,
    bool    AllowCustomerCancellation);
```

- [ ] **Step 2: Build da API**

```powershell
dotnet build src/Horafy.Shared/Horafy.Shared.csproj -q
dotnet build src/Horafy.API/Horafy.API.csproj --no-dependencies -q 2>&1 | Select-Object -Last 3
```

Expected: `0 Erro(s)`.

- [ ] **Step 3: Commit**

```powershell
git add src/Horafy.API/Controllers/V1/TenantsController.cs
git commit -m "feat: add loyalty-settings and cancellation-policy endpoints to TenantsController"
```

---

### Task 7: Frontend (admin) — Fidelidade + Cancelamentos tabs em configurações

**Files:**
- Modify: `frontend/lib/types/tenant.ts`
- Modify: `frontend/lib/api/tenants.ts`
- Modify: `frontend/app/(admin)/admin/configuracoes/page.tsx`
- Create: `frontend/__tests__/LoyaltySettings.test.tsx`

- [ ] **Step 1: Atualizar tipos em `frontend/lib/types/tenant.ts`**

Conteúdo completo do arquivo:

```typescript
// frontend/lib/types/tenant.ts
export interface CancellationPolicy {
  minCancellationHours: number
  cancellationFeePercent: number
  allowCustomerCancellation: boolean
}

export interface LoyaltySettings {
  isEnabled: boolean
  creditRatePercent: number
  minBookingAmount: number
}

export interface Tenant {
  id: string
  name: string
  slug: string
  logoUrl?: string
  primaryColor?: string
  customDomain?: string
  timezone: string
  plan: string
  cancellationPolicy: CancellationPolicy
  loyaltySettings: LoyaltySettings
}

export interface UpdateTenantRequest {
  name?: string
  logoUrl?: string
  primaryColor?: string
  timezone?: string
}

export interface UpdateLoyaltySettingsRequest {
  isEnabled: boolean
  creditRatePercent: number
  minBookingAmount: number
}

export interface UpdateCancellationPolicyRequest {
  minCancellationHours: number
  cancellationFeePercent: number
  allowCustomerCancellation: boolean
}
```

- [ ] **Step 2: Adicionar métodos em `frontend/lib/api/tenants.ts`**

Conteúdo completo:

```typescript
// frontend/lib/api/tenants.ts
import { apiFetch } from './client'
import type {
  Tenant, UpdateTenantRequest,
  UpdateLoyaltySettingsRequest, UpdateCancellationPolicyRequest,
} from '../types/tenant'

export const tenantsApi = {
  me: () => apiFetch<Tenant>('/api/v1/tenants/me'),
  update: (data: UpdateTenantRequest) =>
    apiFetch<void>('/api/v1/tenants/me', { method: 'PUT', body: JSON.stringify(data) }),
  updateTheme: (primaryColor: string, logoUrl?: string) =>
    apiFetch<void>('/api/v1/tenants/me/theme', {
      method: 'PUT',
      body: JSON.stringify({ primaryColor, logoUrl }),
    }),
  updateLoyaltySettings: (data: UpdateLoyaltySettingsRequest) =>
    apiFetch<void>('/api/v1/tenants/loyalty-settings', {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  updateCancellationPolicy: (data: UpdateCancellationPolicyRequest) =>
    apiFetch<void>('/api/v1/tenants/cancellation-policy', {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
}
```

- [ ] **Step 3: Escrever o teste**

```tsx
// frontend/__tests__/LoyaltySettings.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { vi } from 'vitest'

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/configuracoes',
}))

vi.mock('@/store/auth', () => ({
  useAuthStore: (sel: (s: { accessToken: string | null }) => unknown) =>
    sel({ accessToken: 'test-token' }),
}))

vi.mock('@/lib/api/tenants', () => ({
  tenantsApi: {
    me: vi.fn().mockResolvedValue({
      id: 't1', name: 'Barbearia', slug: 'barb', timezone: 'America/Sao_Paulo', plan: 'Free',
      cancellationPolicy: { minCancellationHours: 2, cancellationFeePercent: 0, allowCustomerCancellation: true },
      loyaltySettings: { isEnabled: true, creditRatePercent: 5, minBookingAmount: 0 },
    }),
    update: vi.fn().mockResolvedValue(undefined),
    updateTheme: vi.fn().mockResolvedValue(undefined),
    updateLoyaltySettings: vi.fn().mockResolvedValue(undefined),
    updateCancellationPolicy: vi.fn().mockResolvedValue(undefined),
  },
}))

import ConfiguracoesPage from '@/app/(admin)/admin/configuracoes/page'

describe('ConfiguracoesPage — Loyalty & Cancellation tabs', () => {
  it('shows loyalty tab with current settings', async () => {
    render(<ConfiguracoesPage />)
    fireEvent.click(await screen.findByRole('tab', { name: /fidelidade/i }))
    await waitFor(() => {
      expect(screen.getByLabelText(/taxa de crédito/i)).toBeInTheDocument()
    })
  })

  it('shows cancellation tab with current settings', async () => {
    render(<ConfiguracoesPage />)
    fireEvent.click(await screen.findByRole('tab', { name: /cancelamentos/i }))
    await waitFor(() => {
      expect(screen.getByLabelText(/horas mínimas/i)).toBeInTheDocument()
    })
  })
})
```

- [ ] **Step 4: Executar para verificar FAIL**

```powershell
cd C:\Projetos\JEL\JEL\Horafy\frontend
npx vitest run __tests__/LoyaltySettings.test.tsx 2>&1 | Select-Object -Last 5
```

Expected: FAIL — tabs não existem ainda.

- [ ] **Step 5: Atualizar `frontend/app/(admin)/admin/configuracoes/page.tsx`**

Conteúdo completo:

```tsx
'use client'

import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { tenantsApi } from '@/lib/api/tenants'
import type { Tenant } from '@/lib/types/tenant'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'

const identitySchema = z.object({
  name: z.string().min(1, 'Nome obrigatório'),
  logoUrl: z.string().url('URL inválida').optional().or(z.literal('')),
  primaryColor: z.string().regex(/^#[0-9a-fA-F]{6}$/, 'Cor hex inválida').optional().or(z.literal('')),
  timezone: z.string().min(1),
})
type IdentityForm = z.infer<typeof identitySchema>

const loyaltySchema = z.object({
  isEnabled: z.boolean(),
  creditRatePercent: z.coerce.number().min(0).max(100),
  minBookingAmount: z.coerce.number().min(0),
})
type LoyaltyForm = z.infer<typeof loyaltySchema>

const cancellationSchema = z.object({
  allowCustomerCancellation: z.boolean(),
  minCancellationHours: z.coerce.number().int().min(0),
  cancellationFeePercent: z.coerce.number().min(0).max(100),
})
type CancellationForm = z.infer<typeof cancellationSchema>

export default function ConfiguracoesPage() {
  const [tenant, setTenant] = useState<Tenant | null>(null)
  const [savedIdentity, setSavedIdentity] = useState(false)
  const [savedLoyalty, setSavedLoyalty] = useState(false)
  const [savedCancel, setSavedCancel] = useState(false)

  const identityForm = useForm<IdentityForm>({ resolver: zodResolver(identitySchema) })
  const loyaltyForm  = useForm<LoyaltyForm>({ resolver: zodResolver(loyaltySchema) })
  const cancelForm   = useForm<CancellationForm>({ resolver: zodResolver(cancellationSchema) })

  useEffect(() => {
    tenantsApi.me().then(t => {
      setTenant(t)
      identityForm.reset({ name: t.name, logoUrl: t.logoUrl ?? '', primaryColor: t.primaryColor ?? '', timezone: t.timezone })
      loyaltyForm.reset({
        isEnabled: t.loyaltySettings.isEnabled,
        creditRatePercent: t.loyaltySettings.creditRatePercent,
        minBookingAmount: t.loyaltySettings.minBookingAmount,
      })
      cancelForm.reset({
        allowCustomerCancellation: t.cancellationPolicy.allowCustomerCancellation,
        minCancellationHours: t.cancellationPolicy.minCancellationHours,
        cancellationFeePercent: t.cancellationPolicy.cancellationFeePercent,
      })
    })
  }, [identityForm, loyaltyForm, cancelForm])

  const onIdentitySubmit = async (data: IdentityForm) => {
    await tenantsApi.update(data)
    if (data.primaryColor) await tenantsApi.updateTheme(data.primaryColor, data.logoUrl || undefined)
    setSavedIdentity(true)
    setTimeout(() => setSavedIdentity(false), 3000)
  }

  const onLoyaltySubmit = async (data: LoyaltyForm) => {
    await tenantsApi.updateLoyaltySettings(data)
    setSavedLoyalty(true)
    setTimeout(() => setSavedLoyalty(false), 3000)
  }

  const onCancelSubmit = async (data: CancellationForm) => {
    await tenantsApi.updateCancellationPolicy(data)
    setSavedCancel(true)
    setTimeout(() => setSavedCancel(false), 3000)
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Configurações</h1>

      <Tabs defaultValue="identidade">
        <TabsList>
          <TabsTrigger value="identidade">Identidade Visual</TabsTrigger>
          <TabsTrigger value="cancelamentos">Cancelamentos</TabsTrigger>
          <TabsTrigger value="fidelidade">Fidelidade</TabsTrigger>
          <TabsTrigger value="plano">Plano</TabsTrigger>
        </TabsList>

        <TabsContent value="identidade">
          <Card>
            <CardHeader><CardTitle>Identidade Visual</CardTitle></CardHeader>
            <CardContent>
              <form onSubmit={identityForm.handleSubmit(onIdentitySubmit)} className="space-y-4 max-w-md">
                <div>
                  <Label htmlFor="name">Nome do Negócio</Label>
                  <Input id="name" {...identityForm.register('name')} />
                  {identityForm.formState.errors.name && <p className="text-sm text-red-500 mt-1">{identityForm.formState.errors.name.message}</p>}
                </div>
                <div>
                  <Label htmlFor="logoUrl">URL do Logo</Label>
                  <Input id="logoUrl" {...identityForm.register('logoUrl')} placeholder="https://..." />
                </div>
                <div>
                  <Label htmlFor="primaryColor">Cor Principal</Label>
                  <div className="flex gap-2 items-center">
                    <Input id="primaryColor" {...identityForm.register('primaryColor')} placeholder="#6366f1" className="font-mono" />
                    <input type="color" {...identityForm.register('primaryColor')} className="h-10 w-10 rounded border cursor-pointer" />
                  </div>
                </div>
                <div>
                  <Label htmlFor="timezone">Fuso Horário</Label>
                  <Input id="timezone" {...identityForm.register('timezone')} placeholder="America/Sao_Paulo" />
                </div>
                <div className="flex items-center gap-4">
                  <Button type="submit">Salvar</Button>
                  {savedIdentity && <span className="text-sm text-green-600">Salvo com sucesso!</span>}
                </div>
              </form>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="cancelamentos">
          <Card>
            <CardHeader><CardTitle>Política de Cancelamento</CardTitle></CardHeader>
            <CardContent>
              <form onSubmit={cancelForm.handleSubmit(onCancelSubmit)} className="space-y-4 max-w-md">
                <div className="flex items-center gap-3">
                  <input
                    type="checkbox"
                    id="allowCustomerCancellation"
                    {...cancelForm.register('allowCustomerCancellation')}
                    className="h-4 w-4"
                  />
                  <Label htmlFor="allowCustomerCancellation">Permitir cancelamento pelo cliente</Label>
                </div>
                <div>
                  <Label htmlFor="minCancellationHours">Horas mínimas para cancelamento gratuito</Label>
                  <Input
                    id="minCancellationHours"
                    type="number"
                    min={0}
                    {...cancelForm.register('minCancellationHours')}
                    placeholder="Ex: 24"
                  />
                  <p className="text-xs text-slate-500 mt-1">
                    Cancelamentos com menos de X horas de antecedência estão sujeitos à taxa.
                  </p>
                </div>
                <div>
                  <Label htmlFor="cancellationFeePercent">Taxa de cancelamento fora do prazo (%)</Label>
                  <Input
                    id="cancellationFeePercent"
                    type="number"
                    min={0}
                    max={100}
                    step="0.01"
                    {...cancelForm.register('cancellationFeePercent')}
                    placeholder="Ex: 20"
                  />
                  <p className="text-xs text-slate-500 mt-1">
                    Percentual do valor pago retido como taxa. 0 = reembolso total.
                  </p>
                </div>
                <div className="flex items-center gap-4">
                  <Button type="submit">Salvar</Button>
                  {savedCancel && <span className="text-sm text-green-600">Salvo com sucesso!</span>}
                </div>
              </form>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="fidelidade">
          <Card>
            <CardHeader><CardTitle>Programa de Fidelidade</CardTitle></CardHeader>
            <CardContent>
              <form onSubmit={loyaltyForm.handleSubmit(onLoyaltySubmit)} className="space-y-4 max-w-md">
                <div className="flex items-center gap-3">
                  <input
                    type="checkbox"
                    id="loyaltyIsEnabled"
                    {...loyaltyForm.register('isEnabled')}
                    className="h-4 w-4"
                  />
                  <Label htmlFor="loyaltyIsEnabled">Ativar programa de fidelidade</Label>
                </div>
                <div>
                  <Label htmlFor="creditRatePercent">Taxa de crédito (% do valor do serviço)</Label>
                  <Input
                    id="creditRatePercent"
                    type="number"
                    min={0}
                    max={100}
                    step="0.1"
                    {...loyaltyForm.register('creditRatePercent')}
                    placeholder="Ex: 5"
                  />
                  <p className="text-xs text-slate-500 mt-1">
                    Ex: 5% → agendamento de R$ 100 gera R$ 5,00 em créditos.
                  </p>
                </div>
                <div>
                  <Label htmlFor="minBookingAmount">Valor mínimo para ganhar pontos (R$)</Label>
                  <Input
                    id="minBookingAmount"
                    type="number"
                    min={0}
                    step="0.01"
                    {...loyaltyForm.register('minBookingAmount')}
                    placeholder="Ex: 0"
                  />
                  <p className="text-xs text-slate-500 mt-1">
                    0 = todos os agendamentos geram créditos.
                  </p>
                </div>
                <div className="flex items-center gap-4">
                  <Button type="submit">Salvar</Button>
                  {savedLoyalty && <span className="text-sm text-green-600">Salvo com sucesso!</span>}
                </div>
              </form>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="plano">
          <Card>
            <CardHeader><CardTitle>Plano Atual</CardTitle></CardHeader>
            <CardContent>
              <p className="text-slate-600">Plano: <span className="font-semibold">{tenant?.plan ?? '...'}</span></p>
              <p className="text-sm text-slate-400 mt-2">Gerenciamento de plano será disponibilizado em breve.</p>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  )
}
```

- [ ] **Step 6: Executar testes**

```powershell
cd C:\Projetos\JEL\JEL\Horafy\frontend
npx vitest run __tests__/LoyaltySettings.test.tsx 2>&1 | Select-Object -Last 6
```

Expected: `Tests 2 passed (2)`.

- [ ] **Step 7: Commit**

```powershell
cd C:\Projetos\JEL\JEL\Horafy
git add frontend/lib/types/tenant.ts frontend/lib/api/tenants.ts frontend/app/(admin)/admin/configuracoes/page.tsx frontend/__tests__/LoyaltySettings.test.tsx
git commit -m "feat: add Fidelidade and Cancelamentos tabs to admin configuracoes"
```

---

### Task 8: Frontend (portal) — cancelamento self-service no minha-conta

**Files:**
- Modify: `frontend/lib/api/portal.ts`
- Modify: `frontend/app/(portal)/[slug]/minha-conta/page.tsx`
- Create: `frontend/__tests__/PortalCancel.test.tsx`

- [ ] **Step 1: Adicionar `cancelBooking` em `portal.ts`**

Abrir `frontend/lib/api/portal.ts`. Adicionar ao objeto `portalApi` após `createPayment`:

```typescript
  cancelBooking: (slug: string, token: string, bookingId: string, reason?: string) =>
    portalFetch<void>(`/api/v1/bookings/${bookingId}/cancel`, slug, {
      method: 'POST',
      body: JSON.stringify({ reason: reason ?? null }),
    }, token),
```

- [ ] **Step 2: Escrever o teste**

```tsx
// frontend/__tests__/PortalCancel.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { vi } from 'vitest'

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/slug/minha-conta',
}))

vi.mock('@/store/portal-auth', () => ({
  usePortalAuthStore: () => ({
    accessToken: 'tok123',
    customer: { id: 'c1', name: 'Cliente', email: 'c@test.com' },
  }),
}))

vi.mock('@/lib/api/portal', () => ({
  portalApi: {
    myBookings: vi.fn().mockResolvedValue([
      {
        id: 'b1', serviceName: 'Corte', resourceName: 'João',
        scheduledAt: new Date(Date.now() + 86400000).toISOString(), // amanhã
        durationMinutes: 30, status: 'Confirmed', totalAmount: 100,
      },
    ]),
    myFavorites: vi.fn().mockResolvedValue([]),
    cancelBooking: vi.fn().mockResolvedValue(undefined),
  },
}))

vi.mock('@/lib/api/wallet', () => ({
  portalWalletApi: {
    getWallet: vi.fn().mockResolvedValue({ balance: 0, transactions: [] }),
  },
}))

import MinhaContaPage from '@/app/(portal)/[slug]/minha-conta/page'

describe('MinhaContaPage — cancel booking', () => {
  it('shows cancel button for upcoming confirmed bookings', async () => {
    render(<MinhaContaPage params={{ slug: 'barb' } as any} />)
    await waitFor(() => {
      expect(screen.getByText('Corte')).toBeInTheDocument()
    })
    expect(screen.getByRole('button', { name: /cancelar/i })).toBeInTheDocument()
  })

  it('calls cancelBooking on confirm', async () => {
    const { portalApi } = await import('@/lib/api/portal')
    render(<MinhaContaPage params={{ slug: 'barb' } as any} />)
    await waitFor(() => screen.getByText('Corte'))

    fireEvent.click(screen.getByRole('button', { name: /cancelar/i }))
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /confirmar cancelamento/i })).toBeInTheDocument()
    })
    fireEvent.click(screen.getByRole('button', { name: /confirmar cancelamento/i }))
    await waitFor(() => {
      expect(portalApi.cancelBooking).toHaveBeenCalledWith('barb', 'tok123', 'b1', undefined)
    })
  })
})
```

- [ ] **Step 3: Executar para verificar FAIL**

```powershell
cd C:\Projetos\JEL\JEL\Horafy\frontend
npx vitest run __tests__/PortalCancel.test.tsx 2>&1 | Select-Object -Last 5
```

Expected: FAIL — sem botão de cancelar.

- [ ] **Step 4: Modificar `minha-conta/page.tsx` para adicionar cancelamento**

Abrir `frontend/app/(portal)/[slug]/minha-conta/page.tsx`.

**Adicionar estado de cancelamento** após os `useState` existentes:

```typescript
  const [cancellingId, setCancellingId] = useState<string | null>(null)
  const [cancelError, setCancelError]   = useState<string | null>(null)
```

**Adicionar handler de cancelamento** antes do `return`:

```typescript
  const handleCancel = async (bookingId: string) => {
    if (!accessToken) return
    setCancelError(null)
    try {
      await portalApi.cancelBooking(slug, accessToken, bookingId)
      setBookings(prev => prev.filter(b => b.id !== bookingId))
      setCancellingId(null)
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Erro ao cancelar'
      setCancelError(
        msg.includes('CancellationNotAllowed')
          ? 'Cancelamentos não são permitidos por este estabelecimento.'
          : msg.includes('CancellationWindowClosed')
            ? 'O prazo para cancelamento gratuito já encerrou.'
            : msg
      )
    }
  }
```

**Dentro do card de cada booking `upcoming`**, localizar o bloco:

```tsx
                        <div className="flex flex-col items-end gap-2">
                            <Badge>{STATUS_LABEL[b.status]}</Badge>
                            <Link href={`/${slug}/agendar/${b.id}/status`} className="text-xs text-indigo-600 hover:underline">
                              Ver detalhes
                            </Link>
                          </div>
```

E substituir por:

```tsx
                        <div className="flex flex-col items-end gap-2">
                            <Badge>{STATUS_LABEL[b.status]}</Badge>
                            <Link href={`/${slug}/agendar/${b.id}/status`} className="text-xs text-indigo-600 hover:underline">
                              Ver detalhes
                            </Link>
                            {cancellingId === b.id ? (
                              <div className="flex flex-col items-end gap-1">
                                {cancelError && (
                                  <p className="text-xs text-red-500 text-right max-w-[180px]">{cancelError}</p>
                                )}
                                <div className="flex gap-2">
                                  <Button
                                    size="sm"
                                    variant="destructive"
                                    onClick={() => handleCancel(b.id)}
                                  >
                                    Confirmar cancelamento
                                  </Button>
                                  <Button
                                    size="sm"
                                    variant="ghost"
                                    onClick={() => { setCancellingId(null); setCancelError(null) }}
                                  >
                                    Voltar
                                  </Button>
                                </div>
                              </div>
                            ) : (
                              <Button
                                size="sm"
                                variant="ghost"
                                className="text-xs text-red-500 hover:text-red-700 h-auto p-0"
                                onClick={() => { setCancellingId(b.id); setCancelError(null) }}
                              >
                                Cancelar
                              </Button>
                            )}
                          </div>
```

Verificar que o import de `portalApi` já existe (está na linha 8). Confirmar que `Button` está importado (linha 12).

- [ ] **Step 5: Executar testes**

```powershell
cd C:\Projetos\JEL\JEL\Horafy\frontend
npx vitest run __tests__/PortalCancel.test.tsx 2>&1 | Select-Object -Last 6
```

Expected: `Tests 2 passed (2)`.

- [ ] **Step 6: Commit**

```powershell
cd C:\Projetos\JEL\JEL\Horafy
git add frontend/lib/api/portal.ts frontend/app/(portal)/[slug]/minha-conta/page.tsx frontend/__tests__/PortalCancel.test.tsx
git commit -m "feat: add customer self-service booking cancellation to portal"
```

---

### Task 9: Full Suite + Build Final

- [ ] **Step 1: Backend — todos os testes**

```powershell
cd C:\Projetos\JEL\JEL\Horafy
dotnet test tests/Horafy.Application.Tests 2>&1 | Select-Object -Last 5
```

Expected: `Com falha: 0, Aprovado: 134+`.

Se algum teste falhar por causa de `TenantResult` com novos campos obrigatórios (position args), verifique se outros testes no projeto criam `TenantResult` diretamente. Se sim, atualize-os para incluir os dois novos campos.

- [ ] **Step 2: Frontend — toda a suíte**

```powershell
cd C:\Projetos\JEL\JEL\Horafy\frontend
npx vitest run 2>&1 | Select-Object -Last 8
```

Expected: 22+ arquivos, 44+ testes passando.

- [ ] **Step 3: Build de produção**

```powershell
npx next build 2>&1 | Select-Object -Last 10
```

Expected: `Compiled successfully`, 0 erros.

- [ ] **Step 4: Commit final (se houver pendências)**

```powershell
cd C:\Projetos\JEL\JEL\Horafy
git status --short
```

Se houver arquivos não commitados, adicioná-los agora.

---

## Self-Review

### Spec Coverage

| Requisito | Task | Status |
|-----------|------|--------|
| Loyalty bonus ao completar agendamento | Tasks 1, 2, 3 | ✅ |
| Taxa de crédito configurável (% do valor pago) | Tasks 2, 4, 6, 7 | ✅ |
| Valor mínimo de agendamento para ganhar pontos | Tasks 2, 4, 6, 7 | ✅ |
| Wallet criada automaticamente na primeira recompensa | Task 3 (handler) | ✅ |
| Tipo `LoyaltyBonus` na transação da wallet | Tasks 2, 3 | ✅ |
| Admin configura política de cancelamento via UI | Tasks 4, 6, 7 | ✅ |
| Cliente cancela agendamento no portal | Task 8 | ✅ |
| Mensagem de erro amigável (janela fechada / não permitido) | Task 8 (handler) | ✅ |
| `TenantResult` inclui loyalty + cancellation policy | Task 4 (GetCurrentTenantQuery) | ✅ |
| EF migration para LoyaltySettings no schema público | Task 5 | ✅ |

### Placeholder Scan

Nenhum TBD. Todo código é completo.

**Ponto de atenção — `Payment.Approve("mp-dummy-id")` no teste (Task 3):** `Approve()` valida `Status == Pending` — um `Payment.Create()` novo sempre começa em `Pending`, então `Approve("mp-dummy-id")` funcionará sem erros.

### Type Consistency

- `LoyaltySettings.CalculateBonus(payment.Amount)` → `decimal` → passado para `wallet.AddLoyaltyBonus(bonus, ...)` → `decimal` — ✅
- `TenantResult` com `CancellationPolicyResult` e `LoyaltySettingsResult` — fields mapeados de `t.CancellationPolicy.*` e `t.LoyaltySettings.*` — ✅
- `portalApi.cancelBooking(slug, token, bookingId, reason?)` → `POST /api/v1/bookings/{id}/cancel` com body `{ reason }` — handler backend espera `CancelBookingCommand(BookingId, Reason?)` — ✅
- `UpdateLoyaltySettingsRequest` (frontend) → `UpdateLoyaltySettingsCommand` (backend) via `PUT /api/v1/tenants/loyalty-settings` — campo a campo: `isEnabled/IsEnabled`, `creditRatePercent/CreditRatePercent`, `minBookingAmount/MinBookingAmount` — ✅
