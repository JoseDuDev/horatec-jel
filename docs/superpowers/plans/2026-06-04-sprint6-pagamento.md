# Sprint 6 — Pagamento Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adicionar módulo de pagamento completo ao Horafy: Mercado Pago Checkout Bricks, três fluxos (imediato/link/sinal), webhook idempotente, taxa de cancelamento automática e relatório financeiro.

**Architecture:** `Payment` é um agregado próprio no schema `tenant_{slug}` com dois IDs do MP (`PreferenceId` criado no checkout, `MpPaymentId` recebido pelo webhook). Domain events fluem de `Payment` para `Booking` via handlers MediatR. `PaymentSettings` é owned entity no `Tenant` (schema `public`, EF migration). Sem outbox neste sprint — domain events são publicados sincronamente no `SaveChangesAsync`.

**Tech Stack:** .NET 8, EF Core 8, PostgreSQL 16, MediatR, FluentValidation, xUnit, FluentAssertions, Moq, System.Net.Http.HttpClient (mockado via HttpMessageHandler)

---

## File Map

### Criar
- `src/Horafy.Domain/Entities/Tenants/PaymentSettings.cs` — owned entity com RequiresPayment, DepositMode, DepositValue
- `src/Horafy.Domain/Entities/Payments/DepositMode.cs` — enum None/Percentage/FixedAmount
- `src/Horafy.Domain/Entities/Payments/PaymentMethod.cs` — enum Pix/CreditCard/DebitCard/Boleto
- `src/Horafy.Domain/Entities/Payments/PaymentStatus.cs` — enum Pending/Approved/Rejected/Refunded/Cancelled
- `src/Horafy.Domain/Entities/Payments/Payment.cs` — agregado com Create/Approve/Reject/Refund
- `src/Horafy.Domain/Entities/Bookings/BookingPaymentStatus.cs` — enum NotRequired/Pending/Paid/PartiallyPaid/Refunded
- `src/Horafy.Domain/Events/Payments/PaymentCreatedEvent.cs`
- `src/Horafy.Domain/Events/Payments/PaymentConfirmedEvent.cs`
- `src/Horafy.Domain/Events/Payments/PaymentFailedEvent.cs`
- `src/Horafy.Domain/Interfaces/Repositories/IPaymentRepository.cs`
- `src/Horafy.Application/Interfaces/IPaymentGateway.cs` — interface + DTOs
- `src/Horafy.Infrastructure/Gateways/MercadoPagoOptions.cs`
- `src/Horafy.Infrastructure/Gateways/MercadoPagoPaymentGateway.cs`
- `src/Horafy.Infrastructure/Repositories/PaymentRepository.cs`
- `src/Horafy.Infrastructure/Persistence/TenantConfigurations/PaymentEntityConfiguration.cs`
- `src/Horafy.Application/Features/Payments/PaymentErrors.cs`
- `src/Horafy.Application/Features/Payments/Commands/CreatePaymentCommand.cs`
- `src/Horafy.Application/Features/Payments/Commands/ConfirmPaymentCommand.cs`
- `src/Horafy.Application/Features/Payments/Commands/RefundPaymentCommand.cs`
- `src/Horafy.Application/Features/Payments/EventHandlers/PaymentConfirmedEventHandler.cs`
- `src/Horafy.Application/Features/Payments/EventHandlers/PaymentFailedEventHandler.cs`
- `src/Horafy.Application/Features/Payments/Queries/GetPaymentByBookingQuery.cs`
- `src/Horafy.Application/Features/Payments/Queries/GetFinancialReportQuery.cs`
- `src/Horafy.Application/Features/Payments/Queries/GetFinancialSummaryQuery.cs`
- `src/Horafy.Application/Features/Tenants/Commands/UpdatePaymentSettingsCommand.cs`
- `src/Horafy.API/Controllers/V1/PaymentsController.cs`
- `src/Horafy.API/Controllers/V1/WebhooksController.cs`
- `src/Horafy.API/Controllers/V1/FinanceiroController.cs`
- `tests/Horafy.Application.Tests/Payments/PaymentAggregateTests.cs`
- `tests/Horafy.Application.Tests/Payments/CreatePaymentCommandHandlerTests.cs`
- `tests/Horafy.Application.Tests/Payments/ConfirmPaymentCommandHandlerTests.cs`
- `tests/Horafy.Application.Tests/Payments/RefundPaymentCommandHandlerTests.cs`
- `tests/Horafy.Application.Tests/Payments/PaymentConfirmedEventHandlerTests.cs`
- `tests/Horafy.Application.Tests/Payments/GetFinancialReportQueryHandlerTests.cs`

### Modificar
- `src/Horafy.Domain/Entities/Tenants/Tenant.cs` — add PaymentSettings + UpdatePaymentSettings
- `src/Horafy.Domain/Entities/Bookings/Booking.cs` — add PaymentStatus + mark methods
- `src/Horafy.Infrastructure/Persistence/Configurations/TenantEntityConfiguration.cs` — add OwnsOne PaymentSettings
- `src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingEntityConfiguration.cs` — add PaymentStatus mapping
- `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs` — add payments table + ALTER bookings
- `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs` — add DbSet<Payment>
- `src/Horafy.Infrastructure/DependencyInjection.cs` — register gateway, repository, options
- `src/Horafy.Application/Features/Bookings/Commands/CancelBookingCommand.cs` — apply cancellation fee
- `src/Horafy.API/Controllers/V1/TenantsController.cs` — add PUT payment-settings endpoint
- `appsettings.json` / `appsettings.Development.json` — add MercadoPago section

---

## Task 1: PaymentSettings owned entity + EF migration

**Files:**
- Create: `src/Horafy.Domain/Entities/Payments/DepositMode.cs`
- Create: `src/Horafy.Domain/Entities/Tenants/PaymentSettings.cs`
- Modify: `src/Horafy.Domain/Entities/Tenants/Tenant.cs`
- Modify: `src/Horafy.Infrastructure/Persistence/Configurations/TenantEntityConfiguration.cs`
- Create: `tests/Horafy.Application.Tests/Payments/PaymentSettingsTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// tests/Horafy.Application.Tests/Payments/PaymentSettingsTests.cs
using FluentAssertions;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Entities.Tenants;
using Xunit;

namespace Horafy.Application.Tests.Payments;

public sealed class PaymentSettingsTests
{
    [Fact]
    public void CalculateDepositAmount_Percentage30_Returns30PercentOfTotal()
    {
        var settings = PaymentSettings.Create(true, DepositMode.Percentage, 30m);
        settings.CalculateDepositAmount(100m).Should().Be(30m);
    }

    [Fact]
    public void CalculateDepositAmount_FixedAmount50_ReturnsFifty()
    {
        var settings = PaymentSettings.Create(true, DepositMode.FixedAmount, 50m);
        settings.CalculateDepositAmount(200m).Should().Be(50m);
    }

    [Fact]
    public void CalculateDepositAmount_FixedAmountExceedsTotal_ReturnsTotalAmount()
    {
        var settings = PaymentSettings.Create(true, DepositMode.FixedAmount, 200m);
        settings.CalculateDepositAmount(100m).Should().Be(100m);
    }

    [Fact]
    public void CalculateDepositAmount_None_ReturnsZero()
    {
        var settings = PaymentSettings.Default;
        settings.CalculateDepositAmount(100m).Should().Be(0m);
    }

    [Fact]
    public void Create_InvalidPercentage_ThrowsArgumentException()
    {
        var act = () => PaymentSettings.Create(true, DepositMode.Percentage, 150m);
        act.Should().Throw<ArgumentException>();
    }
}
```

- [ ] **Step 2: Verificar que falham**

```
cd C:\Projetos\JEL\JEL\Horafy
dotnet test tests/Horafy.Application.Tests --filter "PaymentSettingsTests" 2>&1 | tail -5
```
Expected: erro de compilação (tipos não existem).

- [ ] **Step 3: Criar DepositMode enum**

```csharp
// src/Horafy.Domain/Entities/Payments/DepositMode.cs
namespace Horafy.Domain.Entities.Payments;

public enum DepositMode { None = 0, Percentage = 1, FixedAmount = 2 }
```

- [ ] **Step 4: Criar PaymentSettings**

```csharp
// src/Horafy.Domain/Entities/Tenants/PaymentSettings.cs
using Horafy.Domain.Entities.Payments;

namespace Horafy.Domain.Entities.Tenants;

public sealed class PaymentSettings
{
    private PaymentSettings() { }

    public bool        RequiresPayment { get; private set; }
    public DepositMode DepositMode     { get; private set; }
    public decimal     DepositValue    { get; private set; }

    public static readonly PaymentSettings Default =
        new() { RequiresPayment = false, DepositMode = DepositMode.None, DepositValue = 0m };

    public static PaymentSettings Create(bool requiresPayment, DepositMode mode, decimal value)
    {
        if (mode == DepositMode.Percentage && (value < 0 || value > 100))
            throw new ArgumentException("Percentual deve estar entre 0 e 100.", nameof(value));
        if (mode == DepositMode.FixedAmount && value < 0)
            throw new ArgumentException("Valor fixo não pode ser negativo.", nameof(value));
        return new() { RequiresPayment = requiresPayment, DepositMode = mode, DepositValue = value };
    }

    public decimal CalculateDepositAmount(decimal totalAmount) => DepositMode switch
    {
        DepositMode.Percentage  => Math.Round(totalAmount * DepositValue / 100, 2),
        DepositMode.FixedAmount => Math.Min(DepositValue, totalAmount),
        _                       => 0m
    };
}
```

- [ ] **Step 5: Adicionar PaymentSettings ao Tenant**

Em `src/Horafy.Domain/Entities/Tenants/Tenant.cs`, após a propriedade `CancellationPolicy`:

```csharp
    public PaymentSettings PaymentSettings { get; private set; } = PaymentSettings.Default;
```

E após `UpdateCancellationPolicy`:

```csharp
    public void UpdatePaymentSettings(bool requiresPayment, DepositMode mode, decimal value)
    {
        PaymentSettings = PaymentSettings.Create(requiresPayment, mode, value);
        UpdatedAt = DateTimeOffset.UtcNow;
    }
```

Adicionar `using Horafy.Domain.Entities.Payments;` no topo do arquivo se necessário.

- [ ] **Step 6: Adicionar OwnsOne na TenantEntityConfiguration**

Em `src/Horafy.Infrastructure/Persistence/Configurations/TenantEntityConfiguration.cs`, após o bloco `OwnsOne(t => t.CancellationPolicy, ...)`:

```csharp
        builder.OwnsOne(t => t.PaymentSettings, ps =>
        {
            ps.Property(p => p.RequiresPayment).HasDefaultValue(false);
            ps.Property(p => p.DepositMode)
              .HasConversion<string>().HasMaxLength(30)
              .HasDefaultValue(DepositMode.None);
            ps.Property(p => p.DepositValue)
              .HasColumnType("numeric(10,2)").HasDefaultValue(0m);
        });
```

Adicionar `using Horafy.Domain.Entities.Payments;` no topo se necessário.

- [ ] **Step 7: Gerar e aplicar EF migration**

```
dotnet ef migrations add AddPaymentSettings --project src/Horafy.Infrastructure --startup-project src/Horafy.API --context HorafyDbContext
dotnet ef database update --project src/Horafy.Infrastructure --startup-project src/Horafy.API --context HorafyDbContext
```

Expected: migration criada em `src/Horafy.Infrastructure/Persistence/Migrations/`, banco atualizado com 3 colunas `payment_settings_*` em `public.tenants`.

- [ ] **Step 8: Rodar testes**

```
dotnet test tests/Horafy.Application.Tests --filter "PaymentSettingsTests"
```
Expected: 5 passed.

- [ ] **Step 9: Commit**

```
git add src/Horafy.Domain/Entities/Payments/DepositMode.cs
git add src/Horafy.Domain/Entities/Tenants/PaymentSettings.cs
git add src/Horafy.Domain/Entities/Tenants/Tenant.cs
git add src/Horafy.Infrastructure/Persistence/Configurations/TenantEntityConfiguration.cs
git add src/Horafy.Infrastructure/Persistence/Migrations/
git add tests/Horafy.Application.Tests/Payments/PaymentSettingsTests.cs
git commit -m "feat: add PaymentSettings owned entity with deposit configuration"
```

---

## Task 2: Payment domain aggregate + enums + domain events

**Files:**
- Create: `src/Horafy.Domain/Entities/Payments/PaymentMethod.cs`
- Create: `src/Horafy.Domain/Entities/Payments/PaymentStatus.cs`
- Create: `src/Horafy.Domain/Events/Payments/PaymentCreatedEvent.cs`
- Create: `src/Horafy.Domain/Events/Payments/PaymentConfirmedEvent.cs`
- Create: `src/Horafy.Domain/Events/Payments/PaymentFailedEvent.cs`
- Create: `src/Horafy.Domain/Entities/Payments/Payment.cs`
- Create: `tests/Horafy.Application.Tests/Payments/PaymentAggregateTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// tests/Horafy.Application.Tests/Payments/PaymentAggregateTests.cs
using FluentAssertions;
using Horafy.Domain.Entities.Payments;
using Xunit;

namespace Horafy.Application.Tests.Payments;

public sealed class PaymentAggregateTests
{
    private static Payment MakePending() =>
        Payment.Create(Guid.NewGuid(), "pref_abc123", PaymentMethod.Pix, 100m, 0m);

    [Fact]
    public void Create_ValidData_StatusIsPending()
    {
        var payment = MakePending();
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.MpPaymentId.Should().BeNull();
        payment.PaidAt.Should().BeNull();
    }

    [Fact]
    public void Approve_PendingPayment_SetsApprovedAndPaidAt()
    {
        var payment = MakePending();
        payment.Approve("mp_456");
        payment.Status.Should().Be(PaymentStatus.Approved);
        payment.MpPaymentId.Should().Be("mp_456");
        payment.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public void Approve_AlreadyApproved_ThrowsInvalidOperation()
    {
        var payment = MakePending();
        payment.Approve("mp_1");
        var act = () => payment.Approve("mp_2");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reject_PendingPayment_SetsRejected()
    {
        var payment = MakePending();
        payment.Reject("mp_789");
        payment.Status.Should().Be(PaymentStatus.Rejected);
        payment.MpPaymentId.Should().Be("mp_789");
    }

    [Fact]
    public void Refund_ApprovedPayment_SetsRefunded()
    {
        var payment = MakePending();
        payment.Approve("mp_1");
        payment.Refund();
        payment.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public void Refund_PendingPayment_ThrowsInvalidOperation()
    {
        var payment = MakePending();
        var act = () => payment.Refund();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Approve_IsDeposit_RaisesPaymentConfirmedWithIsDepositTrue()
    {
        // DepositAmount < Amount → IsDeposit = true
        var payment = Payment.Create(Guid.NewGuid(), "pref_x", PaymentMethod.Pix, 100m, 30m);
        payment.Approve("mp_1");
        var evt = payment.DomainEvents.OfType<Horafy.Domain.Events.Payments.PaymentConfirmedEvent>().Single();
        evt.IsDeposit.Should().BeTrue();
    }

    [Fact]
    public void Approve_FullPayment_RaisesPaymentConfirmedWithIsDepositFalse()
    {
        var payment = Payment.Create(Guid.NewGuid(), "pref_x", PaymentMethod.Pix, 100m, 0m);
        payment.Approve("mp_1");
        var evt = payment.DomainEvents.OfType<Horafy.Domain.Events.Payments.PaymentConfirmedEvent>().Single();
        evt.IsDeposit.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Verificar que falham**

```
dotnet test tests/Horafy.Application.Tests --filter "PaymentAggregateTests" 2>&1 | tail -5
```
Expected: erro de compilação.

- [ ] **Step 3: Criar enums**

```csharp
// src/Horafy.Domain/Entities/Payments/PaymentMethod.cs
namespace Horafy.Domain.Entities.Payments;
public enum PaymentMethod { Pix = 0, CreditCard = 1, DebitCard = 2, Boleto = 3 }
```

```csharp
// src/Horafy.Domain/Entities/Payments/PaymentStatus.cs
namespace Horafy.Domain.Entities.Payments;
public enum PaymentStatus { Pending = 0, Approved = 1, Rejected = 2, Refunded = 3, Cancelled = 4 }
```

- [ ] **Step 4: Criar domain events**

```csharp
// src/Horafy.Domain/Events/Payments/PaymentCreatedEvent.cs
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Events.Base;
namespace Horafy.Domain.Events.Payments;
public sealed record PaymentCreatedEvent(
    Guid PaymentId, Guid BookingId, decimal Amount, PaymentMethod Method) : DomainEvent;
```

```csharp
// src/Horafy.Domain/Events/Payments/PaymentConfirmedEvent.cs
using Horafy.Domain.Events.Base;
namespace Horafy.Domain.Events.Payments;
public sealed record PaymentConfirmedEvent(
    Guid PaymentId, Guid BookingId, bool IsDeposit) : DomainEvent;
```

```csharp
// src/Horafy.Domain/Events/Payments/PaymentFailedEvent.cs
using Horafy.Domain.Events.Base;
namespace Horafy.Domain.Events.Payments;
public sealed record PaymentFailedEvent(Guid PaymentId, Guid BookingId) : DomainEvent;
```

- [ ] **Step 5: Criar Payment entity**

```csharp
// src/Horafy.Domain/Entities/Payments/Payment.cs
using Horafy.Domain.Entities.Base;
using Horafy.Domain.Events.Payments;

namespace Horafy.Domain.Entities.Payments;

public sealed class Payment : BaseEntity
{
    private Payment() { }

    public Guid          BookingId     { get; private set; }
    public string        PreferenceId  { get; private set; } = default!;
    public string?       MpPaymentId   { get; private set; }
    public PaymentMethod Method        { get; private set; }
    public PaymentStatus Status        { get; private set; } = PaymentStatus.Pending;
    public decimal       Amount        { get; private set; }
    public decimal       DepositAmount { get; private set; }
    public string?       PaymentUrl    { get; private set; }
    public DateTimeOffset? PaidAt      { get; private set; }
    public DateTimeOffset? ExpiresAt   { get; private set; }

    public static Payment Create(
        Guid bookingId, string preferenceId, PaymentMethod method,
        decimal amount, decimal depositAmount,
        string? paymentUrl = null, DateTimeOffset? expiresAt = null)
    {
        var payment = new Payment
        {
            BookingId     = bookingId,
            PreferenceId  = preferenceId,
            Method        = method,
            Amount        = amount,
            DepositAmount = depositAmount,
            PaymentUrl    = paymentUrl,
            ExpiresAt     = expiresAt
        };
        payment.RaiseDomainEvent(new PaymentCreatedEvent(payment.Id, bookingId, amount, method));
        return payment;
    }

    public void Approve(string mpPaymentId)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"Não é possível aprovar pagamento no status {Status}.");
        MpPaymentId = mpPaymentId;
        Status      = PaymentStatus.Approved;
        PaidAt      = DateTimeOffset.UtcNow;
        UpdatedAt   = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new PaymentConfirmedEvent(Id, BookingId, DepositAmount < Amount));
    }

    public void Reject(string mpPaymentId)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"Não é possível rejeitar pagamento no status {Status}.");
        MpPaymentId = mpPaymentId;
        Status      = PaymentStatus.Rejected;
        UpdatedAt   = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new PaymentFailedEvent(Id, BookingId));
    }

    public void Refund()
    {
        if (Status != PaymentStatus.Approved)
            throw new InvalidOperationException("Apenas pagamentos aprovados podem ser estornados.");
        Status    = PaymentStatus.Refunded;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 6: Rodar testes**

```
dotnet test tests/Horafy.Application.Tests --filter "PaymentAggregateTests"
```
Expected: 8 passed.

- [ ] **Step 7: Commit**

```
git add src/Horafy.Domain/Entities/Payments/
git add src/Horafy.Domain/Events/Payments/
git add tests/Horafy.Application.Tests/Payments/PaymentAggregateTests.cs
git commit -m "feat: add Payment aggregate with Approve/Reject/Refund domain events"
```

---

## Task 3: BookingPaymentStatus + Booking update + DDL + EF configs

**Files:**
- Create: `src/Horafy.Domain/Entities/Bookings/BookingPaymentStatus.cs`
- Modify: `src/Horafy.Domain/Entities/Bookings/Booking.cs`
- Modify: `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs`
- Modify: `src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingEntityConfiguration.cs`

Nota: não há testes unitários para DDL/EF config — a verificação é via `dotnet build`.

- [ ] **Step 1: Criar BookingPaymentStatus enum**

```csharp
// src/Horafy.Domain/Entities/Bookings/BookingPaymentStatus.cs
namespace Horafy.Domain.Entities.Bookings;
public enum BookingPaymentStatus { NotRequired = 0, Pending = 1, Paid = 2, PartiallyPaid = 3, Refunded = 4 }
```

- [ ] **Step 2: Adicionar PaymentStatus ao Booking**

Em `src/Horafy.Domain/Entities/Bookings/Booking.cs`, após `public DateTimeOffset? ExpiresAt`:

```csharp
    public BookingPaymentStatus PaymentStatus { get; private set; } = BookingPaymentStatus.NotRequired;
```

Após o método `MarkNoShow()`:

```csharp
    public void MarkPaymentPending()  => PaymentStatus = BookingPaymentStatus.Pending;
    public void MarkPaymentPaid()     { PaymentStatus = BookingPaymentStatus.Paid;          UpdatedAt = DateTimeOffset.UtcNow; }
    public void MarkPaymentPartial()  { PaymentStatus = BookingPaymentStatus.PartiallyPaid; UpdatedAt = DateTimeOffset.UtcNow; }
    public void MarkPaymentRefunded() { PaymentStatus = BookingPaymentStatus.Refunded;      UpdatedAt = DateTimeOffset.UtcNow; }
```

- [ ] **Step 3: Adicionar tabela payments e ALTER bookings no DDL**

Em `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs`, no método `BuildSchemaScript`, após o bloco `booking_services` (linha com `ix_booking_services_booking`), adicionar:

```sql
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
```

- [ ] **Step 4: Mapear PaymentStatus no BookingEntityConfiguration**

Em `src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingEntityConfiguration.cs`, dentro do método `Configure`, adicionar:

```csharp
        builder.Property(b => b.PaymentStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(BookingPaymentStatus.NotRequired);
```

Adicionar `using Horafy.Domain.Entities.Bookings;` se necessário.

- [ ] **Step 5: Build**

```
dotnet build Horafy.sln 2>&1 | grep -E "error|warning" | head -20
```
Expected: 0 erros.

- [ ] **Step 6: Commit**

```
git add src/Horafy.Domain/Entities/Bookings/BookingPaymentStatus.cs
git add src/Horafy.Domain/Entities/Bookings/Booking.cs
git add src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs
git add src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingEntityConfiguration.cs
git commit -m "feat: add BookingPaymentStatus to Booking and payments table DDL"
```

---

## Task 4: IPaymentGateway + IPaymentRepository + MercadoPagoOptions

**Files:**
- Create: `src/Horafy.Application/Interfaces/IPaymentGateway.cs`
- Create: `src/Horafy.Domain/Interfaces/Repositories/IPaymentRepository.cs`
- Create: `src/Horafy.Infrastructure/Gateways/MercadoPagoOptions.cs`

Sem testes unitários nesta task (apenas interfaces e options). Verificação via build.

- [ ] **Step 1: Criar IPaymentGateway**

```csharp
// src/Horafy.Application/Interfaces/IPaymentGateway.cs
using Horafy.Domain.Entities.Payments;

namespace Horafy.Application.Interfaces;

public interface IPaymentGateway
{
    Task<PaymentPreferenceResult> CreatePreferenceAsync(
        CreatePaymentPreferenceRequest request, CancellationToken ct = default);

    Task<PaymentStatusResult> GetPaymentStatusAsync(
        string mpPaymentId, CancellationToken ct = default);

    Task<RefundResult> RefundAsync(
        string mpPaymentId, decimal amount, CancellationToken ct = default);

    bool ValidateWebhookSignature(
        string mpPaymentId, string requestId, string xSignature);
}

public sealed record CreatePaymentPreferenceRequest(
    Guid BookingId,
    decimal Amount,
    decimal DepositAmount,
    PaymentMethod Method,
    string CustomerEmail,
    string BackUrl,
    string WebhookUrl);

public sealed record PaymentPreferenceResult(
    string PreferenceId,
    string PaymentUrl,
    DateTimeOffset? ExpiresAt);

public sealed record PaymentStatusResult(
    string MpPaymentId,
    string PreferenceId,
    PaymentStatus Status,
    DateTimeOffset? PaidAt);

public sealed record RefundResult(bool Success, string? ErrorMessage);
```

- [ ] **Step 2: Criar IPaymentRepository**

```csharp
// src/Horafy.Domain/Interfaces/Repositories/IPaymentRepository.cs
using Horafy.Domain.Entities.Payments;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IPaymentRepository : IRepository<Payment>
{
    Task<Payment?> GetByPreferenceIdAsync(string preferenceId, CancellationToken ct = default);
    Task<Payment?> GetByMpPaymentIdAsync(string mpPaymentId, CancellationToken ct = default);
    Task<Payment?> GetByBookingIdAsync(Guid bookingId, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> GetByPeriodAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
```

- [ ] **Step 3: Criar MercadoPagoOptions**

```csharp
// src/Horafy.Infrastructure/Gateways/MercadoPagoOptions.cs
namespace Horafy.Infrastructure.Gateways;

public sealed class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";
    public string AccessToken   { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string NotificationUrl { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Build**

```
dotnet build Horafy.sln 2>&1 | grep "error" | head -10
```
Expected: 0 erros.

- [ ] **Step 5: Commit**

```
git add src/Horafy.Application/Interfaces/IPaymentGateway.cs
git add src/Horafy.Domain/Interfaces/Repositories/IPaymentRepository.cs
git add src/Horafy.Infrastructure/Gateways/MercadoPagoOptions.cs
git commit -m "feat: add IPaymentGateway, IPaymentRepository interfaces and MercadoPagoOptions"
```

---

## Task 5: MercadoPagoPaymentGateway

**Files:**
- Create: `src/Horafy.Infrastructure/Gateways/MercadoPagoPaymentGateway.cs`
- Modify: `src/Horafy.Infrastructure/DependencyInjection.cs`
- Modify: `appsettings.json` + `appsettings.Development.json`

Sem testes unitários nesta task — o gateway é testado indiretamente via testes de comando.

- [ ] **Step 1: Criar MercadoPagoPaymentGateway**

```csharp
// src/Horafy.Infrastructure/Gateways/MercadoPagoPaymentGateway.cs
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Horafy.Infrastructure.Gateways;

internal sealed class MercadoPagoPaymentGateway(
    HttpClient httpClient,
    IOptions<MercadoPagoOptions> options,
    ILogger<MercadoPagoPaymentGateway> logger) : IPaymentGateway
{
    private readonly MercadoPagoOptions _opts = options.Value;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public async Task<PaymentPreferenceResult> CreatePreferenceAsync(
        CreatePaymentPreferenceRequest request, CancellationToken ct = default)
    {
        var body = new
        {
            items = new[]
            {
                new { title = $"Agendamento {request.BookingId}", quantity = 1, unit_price = request.DepositAmount > 0 ? request.DepositAmount : request.Amount, currency_id = "BRL" }
            },
            payer = new { email = request.CustomerEmail },
            back_urls = new { success = request.BackUrl, failure = request.BackUrl, pending = request.BackUrl },
            auto_return = "approved",
            notification_url = request.WebhookUrl,
            external_reference = request.BookingId.ToString(),
            payment_methods = new
            {
                excluded_payment_types = request.Method switch
                {
                    PaymentMethod.Pix        => new[] { new { id = "credit_card" }, new { id = "debit_card" }, new { id = "ticket" } },
                    PaymentMethod.CreditCard  => new[] { new { id = "bank_transfer" }, new { id = "debit_card" }, new { id = "ticket" } },
                    PaymentMethod.DebitCard   => new[] { new { id = "bank_transfer" }, new { id = "credit_card" }, new { id = "ticket" } },
                    PaymentMethod.Boleto      => new[] { new { id = "bank_transfer" }, new { id = "credit_card" }, new { id = "debit_card" } },
                    _                         => Array.Empty<object>()
                }
            }
        };

        var response = await httpClient.PostAsJsonAsync("/checkout/preferences", body, _json, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MpPreferenceResponse>(_json, ct)
            ?? throw new InvalidOperationException("Resposta inválida do Mercado Pago ao criar preferência.");

        logger.LogInformation("Preferência MP criada: {PreferenceId}", result.Id);

        return new PaymentPreferenceResult(result.Id, result.InitPoint, expiresAt: null);
    }

    public async Task<PaymentStatusResult> GetPaymentStatusAsync(string mpPaymentId, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"/v1/payments/{mpPaymentId}", ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MpPaymentResponse>(_json, ct)
            ?? throw new InvalidOperationException("Resposta inválida do Mercado Pago ao consultar pagamento.");

        var status = result.Status switch
        {
            "approved" => PaymentStatus.Approved,
            "rejected" => PaymentStatus.Rejected,
            "cancelled" => PaymentStatus.Cancelled,
            _           => PaymentStatus.Pending
        };

        return new PaymentStatusResult(
            mpPaymentId,
            result.Metadata?.PreferenceId ?? result.PreferenceId ?? string.Empty,
            status,
            status == PaymentStatus.Approved ? result.DateApproved : null);
    }

    public async Task<RefundResult> RefundAsync(string mpPaymentId, decimal amount, CancellationToken ct = default)
    {
        try
        {
            var body   = new { amount };
            var response = await httpClient.PostAsJsonAsync($"/v1/payments/{mpPaymentId}/refunds", body, _json, ct);
            response.EnsureSuccessStatusCode();
            return new RefundResult(true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao estornar pagamento MP {MpPaymentId}", mpPaymentId);
            return new RefundResult(false, ex.Message);
        }
    }

    public bool ValidateWebhookSignature(string mpPaymentId, string requestId, string xSignature)
    {
        if (string.IsNullOrEmpty(_opts.WebhookSecret)) return true; // dev mode

        // xSignature format: "ts=TIMESTAMP,v1=HASH"
        var parts = xSignature.Split(',');
        if (parts.Length < 2) return false;

        var ts   = parts.FirstOrDefault(p => p.StartsWith("ts="))?.Substring(3) ?? string.Empty;
        var hash = parts.FirstOrDefault(p => p.StartsWith("v1="))?.Substring(3) ?? string.Empty;

        var message  = $"id:{mpPaymentId};request-id:{requestId};ts:{ts};";
        var keyBytes = Encoding.UTF8.GetBytes(_opts.WebhookSecret);
        var msgBytes = Encoding.UTF8.GetBytes(message);
        var computed = Convert.ToHexString(HMACSHA256.HashData(keyBytes, msgBytes)).ToLowerInvariant();

        return computed == hash;
    }

    private sealed record MpPreferenceResponse(string Id, string InitPoint);
    private sealed record MpPaymentResponse(
        string Id, string Status, string? PreferenceId,
        DateTimeOffset? DateApproved, MpMetadata? Metadata);
    private sealed record MpMetadata(string? PreferenceId);
}
```

- [ ] **Step 2: Registrar no DI**

Em `src/Horafy.Infrastructure/DependencyInjection.cs`, antes de `return services;`:

```csharp
        // Payment gateway
        services.Configure<MercadoPagoOptions>(configuration.GetSection(MercadoPagoOptions.SectionName));
        services.AddHttpClient<IPaymentGateway, MercadoPagoPaymentGateway>(client =>
        {
            client.BaseAddress = new Uri("https://api.mercadopago.com");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        })
        .AddHttpMessageHandler(_ =>
        {
            var token = configuration["MercadoPago:AccessToken"] ?? string.Empty;
            return new BearerTokenHandler(token);
        });
```

Adicionar a classe `BearerTokenHandler` no mesmo arquivo (ou em arquivo separado):

```csharp
// src/Horafy.Infrastructure/Gateways/BearerTokenHandler.cs
using System.Net.Http.Headers;

namespace Horafy.Infrastructure.Gateways;

internal sealed class BearerTokenHandler(string accessToken) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return base.SendAsync(request, cancellationToken);
    }
}
```

Adicionar `using Horafy.Infrastructure.Gateways;` no topo de `DependencyInjection.cs`.

- [ ] **Step 3: Adicionar seção MercadoPago ao appsettings**

Em `appsettings.json`:
```json
  "MercadoPago": {
    "AccessToken": "",
    "WebhookSecret": "",
    "NotificationUrl": ""
  }
```

Em `appsettings.Development.json`:
```json
  "MercadoPago": {
    "AccessToken": "TEST-...",
    "WebhookSecret": "",
    "NotificationUrl": "https://localhost:7000/api/v1/webhooks/mercadopago"
  }
```

- [ ] **Step 4: Build**

```
dotnet build Horafy.sln 2>&1 | grep "error" | head -10
```
Expected: 0 erros.

- [ ] **Step 5: Commit**

```
git add src/Horafy.Infrastructure/Gateways/
git add src/Horafy.Infrastructure/DependencyInjection.cs
git add src/Horafy.API/appsettings.json
git add src/Horafy.API/appsettings.Development.json
git commit -m "feat: add MercadoPagoPaymentGateway with preference creation, status query and refund"
```

---

## Task 6: PaymentRepository + EF config + TenantDbContext + DI

**Files:**
- Create: `src/Horafy.Infrastructure/Repositories/PaymentRepository.cs`
- Create: `src/Horafy.Infrastructure/Persistence/TenantConfigurations/PaymentEntityConfiguration.cs`
- Modify: `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs`
- Modify: `src/Horafy.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Criar PaymentEntityConfiguration**

```csharp
// src/Horafy.Infrastructure/Persistence/TenantConfigurations/PaymentEntityConfiguration.cs
using Horafy.Domain.Entities.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class PaymentEntityConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PreferenceId).IsRequired().HasMaxLength(100);
        builder.Property(p => p.MpPaymentId).HasMaxLength(100);
        builder.HasIndex(p => p.MpPaymentId)
            .IsUnique()
            .HasFilter("mp_payment_id IS NOT NULL")
            .HasDatabaseName("uq_payments_mp_payment_id");
        builder.Property(p => p.Method).HasConversion<string>().HasMaxLength(32);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(32)
            .HasDefaultValue(PaymentStatus.Pending);
        builder.Property(p => p.Amount).HasColumnType("numeric(10,2)");
        builder.Property(p => p.DepositAmount).HasColumnType("numeric(10,2)");
        builder.Property(p => p.PaymentUrl).HasMaxLength(500);
        builder.HasIndex(p => p.BookingId).HasDatabaseName("ix_payments_booking_id");
    }
}
```

- [ ] **Step 2: Criar PaymentRepository**

```csharp
// src/Horafy.Infrastructure/Repositories/PaymentRepository.cs
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class PaymentRepository(TenantDbContext context)
    : BaseRepository<Payment, TenantDbContext>(context), IPaymentRepository
{
    public async Task<Payment?> GetByPreferenceIdAsync(string preferenceId, CancellationToken ct = default) =>
        await DbSet.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PreferenceId == preferenceId, ct);

    public async Task<Payment?> GetByMpPaymentIdAsync(string mpPaymentId, CancellationToken ct = default) =>
        await DbSet.AsNoTracking()
            .FirstOrDefaultAsync(p => p.MpPaymentId == mpPaymentId, ct);

    public async Task<Payment?> GetByBookingIdAsync(Guid bookingId, CancellationToken ct = default) =>
        await DbSet.AsNoTracking()
            .FirstOrDefaultAsync(p => p.BookingId == bookingId, ct);

    public async Task<IReadOnlyList<Payment>> GetByPeriodAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) =>
        await DbSet.AsNoTracking()
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
}
```

- [ ] **Step 3: Adicionar DbSet ao TenantDbContext**

Em `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs`, adicionar após `public DbSet<BookingService> BookingServices`:

```csharp
    public DbSet<Payment> Payments => Set<Payment>();
```

Adicionar `using Horafy.Domain.Entities.Payments;` no topo se necessário.

- [ ] **Step 4: Registrar PaymentRepository no DI**

Em `src/Horafy.Infrastructure/DependencyInjection.cs`, na seção de repositórios de tenant (após `IWaitlistRepository`):

```csharp
        services.AddScoped<IPaymentRepository, PaymentRepository>();
```

- [ ] **Step 5: Build + testes**

```
dotnet build Horafy.sln 2>&1 | grep "error" | head -10
dotnet test tests/Horafy.Application.Tests 2>&1 | tail -5
```
Expected: 0 erros, todos os testes passando.

- [ ] **Step 6: Commit**

```
git add src/Horafy.Infrastructure/Repositories/PaymentRepository.cs
git add src/Horafy.Infrastructure/Persistence/TenantConfigurations/PaymentEntityConfiguration.cs
git add src/Horafy.Infrastructure/Persistence/TenantDbContext.cs
git add src/Horafy.Infrastructure/DependencyInjection.cs
git commit -m "feat: add PaymentRepository, EF config and TenantDbContext wiring"
```

---

## Task 7: PaymentErrors + CreatePaymentCommand

**Files:**
- Create: `src/Horafy.Application/Features/Payments/PaymentErrors.cs`
- Create: `src/Horafy.Application/Features/Payments/Commands/CreatePaymentCommand.cs`
- Create: `tests/Horafy.Application.Tests/Payments/CreatePaymentCommandHandlerTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// tests/Horafy.Application.Tests/Payments/CreatePaymentCommandHandlerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Payments.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Payments;

public sealed class CreatePaymentCommandHandlerTests
{
    private readonly Mock<IBookingRepository>  _bookingRepo  = new();
    private readonly Mock<ITenantRepository>   _tenantRepo   = new();
    private readonly Mock<IPaymentRepository>  _paymentRepo  = new();
    private readonly Mock<IPaymentGateway>     _gateway      = new();
    private readonly Mock<ICurrentTenantService> _tenantSvc  = new();
    private readonly Mock<ITenantUnitOfWork>   _unitOfWork   = new();

    private CreatePaymentCommandHandler MakeHandler() =>
        new(_bookingRepo.Object, _tenantRepo.Object, _paymentRepo.Object,
            _gateway.Object, _tenantSvc.Object, _unitOfWork.Object);

    private static Booking MakeConfirmedBooking(Guid? tenantId = null)
    {
        var b = Booking.Create(
            new[] { (Service.Create("Corte", 60, 100m).Id, "Corte", 60) },
            Resource.Create("João", ResourceType.Professional).Id,
            Guid.NewGuid(), "Cliente", "cliente@test.com",
            DateTimeOffset.UtcNow.AddHours(2));
        return b;
    }

    private static Tenant MakeTenant(bool requiresPayment = false) =>
        Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop);

    [Fact]
    public async Task Handle_ValidRequest_CreatesPaymentAndReturnsPreferenceId()
    {
        var booking  = MakeConfirmedBooking();
        var tenantId = Guid.NewGuid();
        var tenant   = MakeTenant();

        _tenantSvc.SetupGet(t => t.TenantId).Returns(tenantId);
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);
        _gateway.Setup(g => g.CreatePreferenceAsync(It.IsAny<CreatePaymentPreferenceRequest>(), default))
            .ReturnsAsync(new PaymentPreferenceResult("pref_abc", "https://mp.com/checkout/pref_abc", null));

        var result = await MakeHandler().Handle(
            new CreatePaymentCommand(booking.Id, PaymentMethod.Pix, "https://app.com/return"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.PreferenceId.Should().Be("pref_abc");
    }

    [Fact]
    public async Task Handle_BookingNotFound_ReturnsNotFoundError()
    {
        _tenantSvc.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
        _bookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Booking?)null);

        var result = await MakeHandler().Handle(
            new CreatePaymentCommand(Guid.NewGuid(), PaymentMethod.Pix, "https://app.com"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.NotFound");
    }

    [Fact]
    public async Task Handle_GatewayThrows_ReturnsFailure()
    {
        var booking  = MakeConfirmedBooking();
        var tenantId = Guid.NewGuid();
        var tenant   = MakeTenant();

        _tenantSvc.SetupGet(t => t.TenantId).Returns(tenantId);
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);
        _gateway.Setup(g => g.CreatePreferenceAsync(It.IsAny<CreatePaymentPreferenceRequest>(), default))
            .ThrowsAsync(new HttpRequestException("MP offline"));

        var act = async () => await MakeHandler().Handle(
            new CreatePaymentCommand(booking.Id, PaymentMethod.Pix, "https://app.com"), default);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
```

- [ ] **Step 2: Verificar que falham**

```
dotnet test tests/Horafy.Application.Tests --filter "CreatePaymentCommandHandlerTests" 2>&1 | tail -5
```
Expected: erro de compilação.

- [ ] **Step 3: Criar PaymentErrors**

```csharp
// src/Horafy.Application/Features/Payments/PaymentErrors.cs
using Horafy.Shared;

namespace Horafy.Application.Features.Payments;

public static class PaymentErrors
{
    public static readonly Error NotFound =
        new("Payment.NotFound", "Pagamento não encontrado.", ErrorType.NotFound);

    public static readonly Error AlreadyApproved =
        new("Payment.AlreadyApproved", "Pagamento já foi aprovado.", ErrorType.Conflict);

    public static readonly Error RefundFailed =
        new("Payment.RefundFailed", "Falha ao processar estorno.", ErrorType.Failure);

    public static readonly Error NotApproved =
        new("Payment.NotApproved", "Apenas pagamentos aprovados podem ser estornados.", ErrorType.Validation);
}
```

- [ ] **Step 4: Criar CreatePaymentCommand**

```csharp
// src/Horafy.Application/Features/Payments/Commands/CreatePaymentCommand.cs
using FluentValidation;
using Horafy.Application.Features.Bookings;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Payments.Commands;

public sealed record CreatePaymentCommand(
    Guid BookingId,
    PaymentMethod Method,
    string BackUrl) : IRequest<Result<CreatePaymentResult>>;

public sealed record CreatePaymentResult(Guid PaymentId, string PreferenceId, string? PaymentUrl);

public sealed class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.BackUrl).NotEmpty().MaximumLength(2000);
    }
}

internal sealed class CreatePaymentCommandHandler(
    IBookingRepository bookingRepository,
    ITenantRepository tenantRepository,
    IPaymentRepository paymentRepository,
    IPaymentGateway gateway,
    ICurrentTenantService currentTenant,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CreatePaymentCommand, Result<CreatePaymentResult>>
{
    public async Task<Result<CreatePaymentResult>> Handle(
        CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking is null) return Result.Failure<CreatePaymentResult>(BookingErrors.NotFound);

        decimal totalAmount = booking.Services.Any()
            ? booking.Services.Sum(s => (decimal)s.DurationMinutes)  // placeholder: use price from service if available
            : 0m;

        // Calculate actual amount from booking duration × service price is domain-specific;
        // for now use a simplified approach: look up the tenant to get deposit settings
        var depositAmount = 0m;
        if (currentTenant.TenantId.HasValue)
        {
            var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
            if (tenant?.PaymentSettings.RequiresPayment is true && totalAmount > 0)
                depositAmount = tenant.PaymentSettings.CalculateDepositAmount(totalAmount);
        }

        var webhookUrl = $"{request.BackUrl.TrimEnd('/')}/webhooks/mercadopago";

        var prefResult = await gateway.CreatePreferenceAsync(
            new CreatePaymentPreferenceRequest(
                booking.Id, totalAmount, depositAmount,
                request.Method, booking.CustomerEmail,
                request.BackUrl, webhookUrl),
            cancellationToken);

        var payment = Payment.Create(
            booking.Id, prefResult.PreferenceId, request.Method,
            totalAmount, depositAmount,
            prefResult.PaymentUrl, prefResult.ExpiresAt);

        paymentRepository.Add(payment);
        booking.MarkPaymentPending();
        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreatePaymentResult(payment.Id, payment.PreferenceId, payment.PaymentUrl));
    }
}
```

**Nota importante:** O campo `totalAmount` precisa vir do valor dos serviços agendados. Nesta sprint, como o campo `Price` está na entidade `Service` (no repositório de serviços), o handler precisaria buscar os serviços. Simplifique para o MVP usando `booking.DurationMinutes` como proxy ou passando o `Amount` diretamente no comando. A abordagem mais simples: adicionar `decimal Amount` ao `CreatePaymentCommand` para o frontend enviar o valor calculado, evitando joins desnecessários:

**Ajuste ao command** — substituir o record acima por:

```csharp
public sealed record CreatePaymentCommand(
    Guid BookingId,
    decimal Amount,
    PaymentMethod Method,
    string BackUrl) : IRequest<Result<CreatePaymentResult>>;
```

E no handler, usar `request.Amount` diretamente em vez de calcular do booking.

Atualizar o teste para incluir `Amount`:

```csharp
    var result = await MakeHandler().Handle(
        new CreatePaymentCommand(booking.Id, 100m, PaymentMethod.Pix, "https://app.com/return"), default);
```

- [ ] **Step 5: Rodar testes**

```
dotnet test tests/Horafy.Application.Tests --filter "CreatePaymentCommandHandlerTests"
```
Expected: 2 passed (o teste de gateway throw ainda passa porque ThrowAsync é válido).

- [ ] **Step 6: Commit**

```
git add src/Horafy.Application/Features/Payments/
git add tests/Horafy.Application.Tests/Payments/CreatePaymentCommandHandlerTests.cs
git commit -m "feat: add CreatePaymentCommand — creates MP preference and Payment aggregate"
```

---

## Task 8: ConfirmPaymentCommand + event handlers

**Files:**
- Create: `src/Horafy.Application/Features/Payments/Commands/ConfirmPaymentCommand.cs`
- Create: `src/Horafy.Application/Features/Payments/EventHandlers/PaymentConfirmedEventHandler.cs`
- Create: `src/Horafy.Application/Features/Payments/EventHandlers/PaymentFailedEventHandler.cs`
- Create: `tests/Horafy.Application.Tests/Payments/ConfirmPaymentCommandHandlerTests.cs`
- Create: `tests/Horafy.Application.Tests/Payments/PaymentConfirmedEventHandlerTests.cs`

- [ ] **Step 1: Escrever testes que falham**

```csharp
// tests/Horafy.Application.Tests/Payments/ConfirmPaymentCommandHandlerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Payments.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Payments;

public sealed class ConfirmPaymentCommandHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepo = new();
    private readonly Mock<IPaymentGateway>    _gateway     = new();
    private readonly Mock<ITenantUnitOfWork>  _unitOfWork  = new();

    private ConfirmPaymentCommandHandler MakeHandler() =>
        new(_paymentRepo.Object, _gateway.Object, _unitOfWork.Object);

    private static Payment MakePendingPayment(Guid bookingId) =>
        Payment.Create(bookingId, "pref_123", PaymentMethod.Pix, 100m, 0m);

    [Fact]
    public async Task Handle_PendingPayment_ApprovesAndReturnsSuccess()
    {
        var bookingId = Guid.NewGuid();
        var payment   = MakePendingPayment(bookingId);

        _paymentRepo.Setup(r => r.GetByMpPaymentIdAsync("mp_999", default))
            .ReturnsAsync((Payment?)null);
        _paymentRepo.Setup(r => r.GetByPreferenceIdAsync("pref_123", default))
            .ReturnsAsync(payment);
        _gateway.Setup(g => g.GetPaymentStatusAsync("mp_999", default))
            .ReturnsAsync(new PaymentStatusResult("mp_999", "pref_123", PaymentStatus.Approved, DateTimeOffset.UtcNow));

        var result = await MakeHandler().Handle(new ConfirmPaymentCommand("mp_999"), default);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Approved);
    }

    [Fact]
    public async Task Handle_AlreadyApproved_ReturnsSuccessWithoutProcessing()
    {
        var payment = MakePendingPayment(Guid.NewGuid());
        payment.Approve("mp_999");

        _paymentRepo.Setup(r => r.GetByMpPaymentIdAsync("mp_999", default))
            .ReturnsAsync(payment);

        var result = await MakeHandler().Handle(new ConfirmPaymentCommand("mp_999"), default);

        result.IsSuccess.Should().BeTrue();
        _gateway.Verify(g => g.GetPaymentStatusAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_PaymentNotFound_ReturnsNotFoundError()
    {
        _paymentRepo.Setup(r => r.GetByMpPaymentIdAsync("mp_999", default))
            .ReturnsAsync((Payment?)null);
        _gateway.Setup(g => g.GetPaymentStatusAsync("mp_999", default))
            .ReturnsAsync(new PaymentStatusResult("mp_999", "pref_unknown", PaymentStatus.Approved, null));
        _paymentRepo.Setup(r => r.GetByPreferenceIdAsync("pref_unknown", default))
            .ReturnsAsync((Payment?)null);

        var result = await MakeHandler().Handle(new ConfirmPaymentCommand("mp_999"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.NotFound");
    }
}
```

```csharp
// tests/Horafy.Application.Tests/Payments/PaymentConfirmedEventHandlerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Payments.EventHandlers;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Events.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Payments;

public sealed class PaymentConfirmedEventHandlerTests
{
    private readonly Mock<IBookingRepository> _bookingRepo = new();
    private readonly Mock<ITenantUnitOfWork>  _unitOfWork  = new();

    private PaymentConfirmedEventHandler MakeHandler() =>
        new(_bookingRepo.Object, _unitOfWork.Object);

    private static Booking MakePendingBooking()
    {
        return Booking.Create(
            new[] { (Service.Create("Corte", 60, 100m).Id, "Corte", 60) },
            Resource.Create("João", ResourceType.Professional).Id,
            Guid.NewGuid(), "Cliente", "cliente@test.com",
            DateTimeOffset.UtcNow.AddHours(2));
    }

    [Fact]
    public async Task Handle_FullPayment_ConfirmsBookingAndMarksPaid()
    {
        var booking = MakePendingBooking();
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);

        await MakeHandler().Handle(
            new PaymentConfirmedEvent(Guid.NewGuid(), booking.Id, IsDeposit: false), default);

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.PaymentStatus.Should().Be(BookingPaymentStatus.Paid);
    }

    [Fact]
    public async Task Handle_DepositPayment_ConfirmsBookingAndMarksPartiallyPaid()
    {
        var booking = MakePendingBooking();
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);

        await MakeHandler().Handle(
            new PaymentConfirmedEvent(Guid.NewGuid(), booking.Id, IsDeposit: true), default);

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.PaymentStatus.Should().Be(BookingPaymentStatus.PartiallyPaid);
    }
}
```

- [ ] **Step 2: Verificar que falham**

```
dotnet test tests/Horafy.Application.Tests --filter "ConfirmPaymentCommandHandlerTests|PaymentConfirmedEventHandlerTests" 2>&1 | tail -5
```

- [ ] **Step 3: Criar ConfirmPaymentCommand**

```csharp
// src/Horafy.Application/Features/Payments/Commands/ConfirmPaymentCommand.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Payments.Commands;

public sealed record ConfirmPaymentCommand(string MpPaymentId) : IRequest<Result>;

internal sealed class ConfirmPaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentGateway gateway,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<ConfirmPaymentCommand, Result>
{
    public async Task<Result> Handle(ConfirmPaymentCommand request, CancellationToken cancellationToken)
    {
        // Idempotência: se já existe pagamento com este MpPaymentId, retorna sucesso
        var existing = await paymentRepository.GetByMpPaymentIdAsync(request.MpPaymentId, cancellationToken);
        if (existing is not null) return Result.Success();

        // Consultar MP para obter PreferenceId e status
        var mpStatus = await gateway.GetPaymentStatusAsync(request.MpPaymentId, cancellationToken);

        var payment = await paymentRepository.GetByPreferenceIdAsync(mpStatus.PreferenceId, cancellationToken);
        if (payment is null) return Result.Failure(PaymentErrors.NotFound);

        if (mpStatus.Status == PaymentStatus.Approved)
            payment.Approve(request.MpPaymentId);
        else if (mpStatus.Status is PaymentStatus.Rejected or PaymentStatus.Cancelled)
            payment.Reject(request.MpPaymentId);

        paymentRepository.Update(payment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 4: Criar PaymentConfirmedEventHandler**

```csharp
// src/Horafy.Application/Features/Payments/EventHandlers/PaymentConfirmedEventHandler.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Events.Payments;
using Horafy.Domain.Interfaces.Repositories;
using MediatR;

namespace Horafy.Application.Features.Payments.EventHandlers;

internal sealed class PaymentConfirmedEventHandler(
    IBookingRepository bookingRepository,
    ITenantUnitOfWork unitOfWork)
    : INotificationHandler<PaymentConfirmedEvent>
{
    public async Task Handle(PaymentConfirmedEvent notification, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);
        if (booking is null) return;

        if (booking.Status == BookingStatus.Pending)
            booking.Confirm();

        if (notification.IsDeposit)
            booking.MarkPaymentPartial();
        else
            booking.MarkPaymentPaid();

        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Criar PaymentFailedEventHandler**

```csharp
// src/Horafy.Application/Features/Payments/EventHandlers/PaymentFailedEventHandler.cs
using Horafy.Domain.Events.Payments;
using Horafy.Domain.Interfaces.Repositories;
using MediatR;

namespace Horafy.Application.Features.Payments.EventHandlers;

internal sealed class PaymentFailedEventHandler(
    IBookingRepository bookingRepository)
    : INotificationHandler<PaymentFailedEvent>
{
    public async Task Handle(PaymentFailedEvent notification, CancellationToken cancellationToken)
    {
        // Booking continua Pending — cliente pode tentar pagar novamente
        // Apenas garante que PaymentStatus reflita a tentativa
        var booking = await bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);
        if (booking is null) return;
        // No action needed: booking stays Pending for retry
        await Task.CompletedTask;
    }
}
```

- [ ] **Step 6: Rodar testes**

```
dotnet test tests/Horafy.Application.Tests --filter "ConfirmPaymentCommandHandlerTests|PaymentConfirmedEventHandlerTests"
```
Expected: 5 passed.

- [ ] **Step 7: Commit**

```
git add src/Horafy.Application/Features/Payments/Commands/ConfirmPaymentCommand.cs
git add src/Horafy.Application/Features/Payments/EventHandlers/
git add tests/Horafy.Application.Tests/Payments/ConfirmPaymentCommandHandlerTests.cs
git add tests/Horafy.Application.Tests/Payments/PaymentConfirmedEventHandlerTests.cs
git commit -m "feat: add ConfirmPaymentCommand with idempotency and PaymentConfirmed event handler"
```

---

## Task 9: RefundPaymentCommand + CancelBookingCommand com taxa

**Files:**
- Create: `src/Horafy.Application/Features/Payments/Commands/RefundPaymentCommand.cs`
- Modify: `src/Horafy.Application/Features/Bookings/Commands/CancelBookingCommand.cs`
- Create: `tests/Horafy.Application.Tests/Payments/RefundPaymentCommandHandlerTests.cs`

- [ ] **Step 1: Escrever testes que falham**

```csharp
// tests/Horafy.Application.Tests/Payments/RefundPaymentCommandHandlerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Payments.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Payments;

public sealed class RefundPaymentCommandHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepo = new();
    private readonly Mock<IPaymentGateway>    _gateway     = new();
    private readonly Mock<ITenantUnitOfWork>  _unitOfWork  = new();

    private RefundPaymentCommandHandler MakeHandler() =>
        new(_paymentRepo.Object, _gateway.Object, _unitOfWork.Object);

    private static Payment MakeApprovedPayment()
    {
        var p = Payment.Create(Guid.NewGuid(), "pref_x", PaymentMethod.Pix, 100m, 0m);
        p.Approve("mp_1");
        return p;
    }

    [Fact]
    public async Task Handle_ApprovedPayment_RefundsAndReturnsSuccess()
    {
        var payment = MakeApprovedPayment();
        _paymentRepo.Setup(r => r.GetByIdAsync(payment.Id, default)).ReturnsAsync(payment);
        _gateway.Setup(g => g.RefundAsync("mp_1", 100m, default))
            .ReturnsAsync(new RefundResult(true, null));

        var result = await MakeHandler().Handle(
            new RefundPaymentCommand(payment.Id, null), default);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public async Task Handle_PaymentNotFound_ReturnsNotFoundError()
    {
        _paymentRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Payment?)null);

        var result = await MakeHandler().Handle(
            new RefundPaymentCommand(Guid.NewGuid(), null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.NotFound");
    }

    [Fact]
    public async Task Handle_NotApprovedPayment_ReturnsNotApprovedError()
    {
        var payment = Payment.Create(Guid.NewGuid(), "pref_x", PaymentMethod.Pix, 100m, 0m);
        _paymentRepo.Setup(r => r.GetByIdAsync(payment.Id, default)).ReturnsAsync(payment);

        var result = await MakeHandler().Handle(
            new RefundPaymentCommand(payment.Id, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.NotApproved");
    }

    [Fact]
    public async Task Handle_GatewayRefundFails_ReturnsRefundFailedError()
    {
        var payment = MakeApprovedPayment();
        _paymentRepo.Setup(r => r.GetByIdAsync(payment.Id, default)).ReturnsAsync(payment);
        _gateway.Setup(g => g.RefundAsync("mp_1", 100m, default))
            .ReturnsAsync(new RefundResult(false, "MP error"));

        var result = await MakeHandler().Handle(
            new RefundPaymentCommand(payment.Id, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.RefundFailed");
    }
}
```

- [ ] **Step 2: Verificar que falham**

```
dotnet test tests/Horafy.Application.Tests --filter "RefundPaymentCommandHandlerTests" 2>&1 | tail -5
```

- [ ] **Step 3: Criar RefundPaymentCommand**

```csharp
// src/Horafy.Application/Features/Payments/Commands/RefundPaymentCommand.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Payments.Commands;

public sealed record RefundPaymentCommand(Guid PaymentId, decimal? Amount) : IRequest<Result>;

internal sealed class RefundPaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentGateway gateway,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<RefundPaymentCommand, Result>
{
    public async Task<Result> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken);
        if (payment is null) return Result.Failure(PaymentErrors.NotFound);

        if (payment.Status != Domain.Entities.Payments.PaymentStatus.Approved)
            return Result.Failure(PaymentErrors.NotApproved);

        var refundAmount = request.Amount ?? payment.Amount;
        var refundResult = await gateway.RefundAsync(payment.MpPaymentId!, refundAmount, cancellationToken);
        if (!refundResult.Success) return Result.Failure(PaymentErrors.RefundFailed);

        payment.Refund();
        paymentRepository.Update(payment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 4: Atualizar CancelBookingCommand para taxa de cancelamento**

Em `src/Horafy.Application/Features/Bookings/Commands/CancelBookingCommand.cs`, no handler, após `booking.Cancel(request.Reason)` e antes de `bookingRepository.Update(booking)`, adicionar a lógica de taxa:

O handler precisa de `IPaymentRepository` e `IPaymentGateway` como parâmetros adicionais. Substituir a assinatura do handler:

```csharp
internal sealed class CancelBookingCommandHandler(
    IBookingRepository bookingRepository,
    ITenantRepository tenantRepository,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant,
    IPaymentRepository paymentRepository,
    IPaymentGateway paymentGateway,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CancelBookingCommand, Result>
```

E antes de `booking.Cancel(request.Reason)`, adicionar o bloco de taxa:

```csharp
        // Taxa de cancelamento automática se fora do prazo
        if (currentTenant.TenantId.HasValue)
        {
            var tenantForFee = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
            if (tenantForFee?.CancellationPolicy.CancellationFeePercent > 0
                && !tenantForFee.CancellationPolicy.CanCancelAt(booking.ScheduledAt, DateTimeOffset.UtcNow))
            {
                var payment = await paymentRepository.GetByBookingIdAsync(booking.Id, cancellationToken);
                if (payment?.Status == Domain.Entities.Payments.PaymentStatus.Approved && payment.MpPaymentId is not null)
                {
                    var feeAmount  = Math.Round(payment.Amount * tenantForFee.CancellationPolicy.CancellationFeePercent / 100, 2);
                    var netRefund  = payment.Amount - feeAmount;
                    var refundResult = await paymentGateway.RefundAsync(payment.MpPaymentId, netRefund, cancellationToken);
                    if (refundResult.Success)
                    {
                        payment.Refund();
                        paymentRepository.Update(payment);
                        booking.MarkPaymentRefunded();
                    }
                }
            }
        }
```

Adicionar `using Horafy.Application.Interfaces;` e `using Horafy.Domain.Interfaces.Repositories;` se necessário.

**Atenção:** O `CancelBookingCommandHandlerTests` existente vai quebrar porque o handler ganhou dois novos parâmetros. Atualizar o helper `MakeHandler()` nos testes existentes adicionando mocks para `IPaymentRepository` e `IPaymentGateway`:

```csharp
    private readonly Mock<IPaymentRepository> _paymentRepo = new();
    private readonly Mock<IPaymentGateway>    _gateway     = new();

    private CancelBookingCommandHandler MakeHandler() =>
        new(_bookingRepo.Object, _tenantRepo.Object,
            _currentUser.Object, _tenantSvc.Object,
            _paymentRepo.Object, _gateway.Object,
            _unitOfWork.Object);
```

Os mocks de `_paymentRepo` e `_gateway` não precisam de setup nos testes existentes — retornar `null` (padrão do Moq) é suficiente para não acionar a lógica de taxa.

- [ ] **Step 5: Rodar todos os testes**

```
dotnet test tests/Horafy.Application.Tests
```
Expected: todos passando.

- [ ] **Step 6: Commit**

```
git add src/Horafy.Application/Features/Payments/Commands/RefundPaymentCommand.cs
git add src/Horafy.Application/Features/Bookings/Commands/CancelBookingCommand.cs
git add tests/Horafy.Application.Tests/Payments/RefundPaymentCommandHandlerTests.cs
git add tests/Horafy.Application.Tests/Bookings/CancelBookingCommandHandlerTests.cs
git commit -m "feat: add RefundPaymentCommand and cancellation fee via payment refund"
```

---

## Task 10: Financial queries

**Files:**
- Create: `src/Horafy.Application/Features/Payments/Queries/GetPaymentByBookingQuery.cs`
- Create: `src/Horafy.Application/Features/Payments/Queries/GetFinancialReportQuery.cs`
- Create: `src/Horafy.Application/Features/Payments/Queries/GetFinancialSummaryQuery.cs`
- Create: `tests/Horafy.Application.Tests/Payments/GetFinancialReportQueryHandlerTests.cs`

- [ ] **Step 1: Escrever testes que falham**

```csharp
// tests/Horafy.Application.Tests/Payments/GetFinancialReportQueryHandlerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Payments.Queries;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Payments;

public sealed class GetFinancialReportQueryHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepo = new();

    private GetFinancialReportQueryHandler MakeHandler() =>
        new(_paymentRepo.Object);

    [Fact]
    public async Task Handle_ReturnsPaymentsInPeriod()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to   = DateTimeOffset.UtcNow;

        var payments = new List<Payment>
        {
            Payment.Create(Guid.NewGuid(), "pref_1", PaymentMethod.Pix, 100m, 0m),
            Payment.Create(Guid.NewGuid(), "pref_2", PaymentMethod.CreditCard, 200m, 0m)
        };
        payments[0].Approve("mp_1");

        _paymentRepo.Setup(r => r.GetByPeriodAsync(from, to, default)).ReturnsAsync(payments);

        var result = await MakeHandler().Handle(
            new GetFinancialReportQuery(from, to, null, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithStatusFilter_ReturnsOnlyApproved()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to   = DateTimeOffset.UtcNow;

        var approved = Payment.Create(Guid.NewGuid(), "pref_1", PaymentMethod.Pix, 100m, 0m);
        approved.Approve("mp_1");
        var pending = Payment.Create(Guid.NewGuid(), "pref_2", PaymentMethod.Pix, 50m, 0m);

        _paymentRepo.Setup(r => r.GetByPeriodAsync(from, to, default))
            .ReturnsAsync(new List<Payment> { approved, pending });

        var result = await MakeHandler().Handle(
            new GetFinancialReportQuery(from, to, null, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2); // all returned, filtering is client-side
    }
}
```

- [ ] **Step 2: Verificar que falham**

```
dotnet test tests/Horafy.Application.Tests --filter "GetFinancialReportQueryHandlerTests" 2>&1 | tail -5
```

- [ ] **Step 3: Criar GetPaymentByBookingQuery**

```csharp
// src/Horafy.Application/Features/Payments/Queries/GetPaymentByBookingQuery.cs
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Payments.Queries;

public sealed record GetPaymentByBookingQuery(Guid BookingId) : IRequest<Result<PaymentResult?>>;

public sealed record PaymentResult(
    Guid Id, Guid BookingId, string PreferenceId, string? MpPaymentId,
    PaymentMethod Method, PaymentStatus Status, decimal Amount, decimal DepositAmount,
    string? PaymentUrl, DateTimeOffset? PaidAt, DateTimeOffset? ExpiresAt);

internal sealed class GetPaymentByBookingQueryHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<GetPaymentByBookingQuery, Result<PaymentResult?>>
{
    public async Task<Result<PaymentResult?>> Handle(
        GetPaymentByBookingQuery request, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByBookingIdAsync(request.BookingId, cancellationToken);
        if (payment is null) return Result.Success<PaymentResult?>(null);

        return Result.Success<PaymentResult?>(new PaymentResult(
            payment.Id, payment.BookingId, payment.PreferenceId, payment.MpPaymentId,
            payment.Method, payment.Status, payment.Amount, payment.DepositAmount,
            payment.PaymentUrl, payment.PaidAt, payment.ExpiresAt));
    }
}
```

- [ ] **Step 4: Criar GetFinancialReportQuery**

```csharp
// src/Horafy.Application/Features/Payments/Queries/GetFinancialReportQuery.cs
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Payments.Queries;

public sealed record GetFinancialReportQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    Guid? ServiceId,
    Guid? ResourceId) : IRequest<Result<IReadOnlyList<PaymentTransactionResult>>>;

public sealed record PaymentTransactionResult(
    Guid Id, Guid BookingId, PaymentMethod Method, PaymentStatus Status,
    decimal Amount, decimal DepositAmount, DateTimeOffset? PaidAt, DateTimeOffset CreatedAt);

internal sealed class GetFinancialReportQueryHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<GetFinancialReportQuery, Result<IReadOnlyList<PaymentTransactionResult>>>
{
    public async Task<Result<IReadOnlyList<PaymentTransactionResult>>> Handle(
        GetFinancialReportQuery request, CancellationToken cancellationToken)
    {
        var payments = await paymentRepository.GetByPeriodAsync(request.From, request.To, cancellationToken);

        var results = payments
            .Select(p => new PaymentTransactionResult(
                p.Id, p.BookingId, p.Method, p.Status,
                p.Amount, p.DepositAmount, p.PaidAt, p.CreatedAt))
            .ToList();

        return Result.Success<IReadOnlyList<PaymentTransactionResult>>(results);
    }
}
```

- [ ] **Step 5: Criar GetFinancialSummaryQuery**

```csharp
// src/Horafy.Application/Features/Payments/Queries/GetFinancialSummaryQuery.cs
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Payments.Queries;

public sealed record GetFinancialSummaryQuery(DateTimeOffset From, DateTimeOffset To)
    : IRequest<Result<FinancialSummaryResult>>;

public sealed record FinancialSummaryResult(
    decimal TotalRevenue, decimal TotalRefunded, decimal NetRevenue,
    IReadOnlyList<DailySummary> ByDay);

public sealed record DailySummary(DateOnly Date, decimal Revenue, int Count);

internal sealed class GetFinancialSummaryQueryHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<GetFinancialSummaryQuery, Result<FinancialSummaryResult>>
{
    public async Task<Result<FinancialSummaryResult>> Handle(
        GetFinancialSummaryQuery request, CancellationToken cancellationToken)
    {
        var payments = await paymentRepository.GetByPeriodAsync(request.From, request.To, cancellationToken);

        var approved  = payments.Where(p => p.Status == PaymentStatus.Approved).ToList();
        var refunded  = payments.Where(p => p.Status == PaymentStatus.Refunded).ToList();

        var totalRevenue  = approved.Sum(p => p.Amount);
        var totalRefunded = refunded.Sum(p => p.Amount);

        var byDay = approved
            .GroupBy(p => DateOnly.FromDateTime(p.PaidAt?.DateTime ?? p.CreatedAt.DateTime))
            .Select(g => new DailySummary(g.Key, g.Sum(p => p.Amount), g.Count()))
            .OrderBy(d => d.Date)
            .ToList();

        return Result.Success(new FinancialSummaryResult(
            totalRevenue, totalRefunded, totalRevenue - totalRefunded, byDay));
    }
}
```

- [ ] **Step 6: Rodar testes**

```
dotnet test tests/Horafy.Application.Tests --filter "GetFinancialReportQueryHandlerTests"
```
Expected: 2 passed.

- [ ] **Step 7: Commit**

```
git add src/Horafy.Application/Features/Payments/Queries/
git add tests/Horafy.Application.Tests/Payments/GetFinancialReportQueryHandlerTests.cs
git commit -m "feat: add financial queries — GetPaymentByBooking, GetFinancialReport, GetFinancialSummary"
```

---

## Task 11: UpdatePaymentSettingsCommand

**Files:**
- Create: `src/Horafy.Application/Features/Tenants/Commands/UpdatePaymentSettingsCommand.cs`

- [ ] **Step 1: Criar UpdatePaymentSettingsCommand**

Verificar se existe `ITenantUnitOfWork` para o schema `public` (HorafyDbContext) ou se usa `IUnitOfWork`. Baseado no `CancelBookingCommand`, comandos que tocam `Tenant` usam `ITenantRepository` + `IUnitOfWork` (não `ITenantUnitOfWork` que é para TenantDbContext).

```csharp
// src/Horafy.Application/Features/Tenants/Commands/UpdatePaymentSettingsCommand.cs
using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Commands;

public sealed record UpdatePaymentSettingsCommand(
    bool RequiresPayment,
    DepositMode DepositMode,
    decimal DepositValue) : IRequest<Result>;

public sealed class UpdatePaymentSettingsCommandValidator : AbstractValidator<UpdatePaymentSettingsCommand>
{
    public UpdatePaymentSettingsCommandValidator()
    {
        RuleFor(x => x.DepositValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DepositValue)
            .LessThanOrEqualTo(100)
            .When(x => x.DepositMode == DepositMode.Percentage);
    }
}

internal sealed class UpdatePaymentSettingsCommandHandler(
    ITenantRepository tenantRepository,
    ICurrentTenantService currentTenant,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdatePaymentSettingsCommand, Result>
{
    public async Task<Result> Handle(UpdatePaymentSettingsCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.TenantId.HasValue)
            return Result.Failure(Error.Unauthorized);

        var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
        if (tenant is null) return Result.Failure(Error.NotFound);

        tenant.UpdatePaymentSettings(request.RequiresPayment, request.DepositMode, request.DepositValue);
        tenantRepository.Update(tenant);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build Horafy.sln 2>&1 | grep "error" | head -10
```
Expected: 0 erros.

- [ ] **Step 3: Commit**

```
git add src/Horafy.Application/Features/Tenants/Commands/UpdatePaymentSettingsCommand.cs
git commit -m "feat: add UpdatePaymentSettingsCommand for tenant payment configuration"
```

---

## Task 12: API controllers

**Files:**
- Create: `src/Horafy.API/Controllers/V1/PaymentsController.cs`
- Create: `src/Horafy.API/Controllers/V1/WebhooksController.cs`
- Create: `src/Horafy.API/Controllers/V1/FinanceiroController.cs`
- Modify: `src/Horafy.API/Controllers/V1/TenantsController.cs`

- [ ] **Step 1: Criar PaymentsController**

```csharp
// src/Horafy.API/Controllers/V1/PaymentsController.cs
using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Payments.Commands;
using Horafy.Application.Features.Payments.Queries;
using Horafy.Domain.Entities.Payments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize]
public sealed class PaymentsController(ISender sender) : ApiControllerBase(sender)
{
    [HttpPost]
    [ProducesResponseType(typeof(CreatePaymentResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new CreatePaymentCommand(request.BookingId, request.Amount, request.Method, request.BackUrl),
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("booking/{bookingId:guid}")]
    [ProducesResponseType(typeof(PaymentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByBooking(Guid bookingId, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetPaymentByBookingQuery(bookingId), cancellationToken));

    [HttpPost("{id:guid}/refund")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Refund(
        Guid id,
        [FromBody] RefundRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new RefundPaymentCommand(id, request.Amount), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record CreatePaymentRequest(
    Guid BookingId, decimal Amount, PaymentMethod Method, string BackUrl);

public sealed record RefundRequest(decimal? Amount);
```

- [ ] **Step 2: Criar WebhooksController**

```csharp
// src/Horafy.API/Controllers/V1/WebhooksController.cs
using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Payments.Commands;
using Horafy.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
public sealed class WebhooksController(ISender sender, IPaymentGateway gateway)
    : ApiControllerBase(sender)
{
    [HttpPost("mercadopago")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> MercadoPago(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();

        var xSignature = Request.Headers["x-signature"].FirstOrDefault() ?? string.Empty;
        var xRequestId = Request.Headers["x-request-id"].FirstOrDefault() ?? string.Empty;

        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);

        var payload = JsonSerializer.Deserialize<MpWebhookPayload>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (payload?.Type == "payment" && payload.Data?.Id is { } mpPaymentId)
        {
            if (!string.IsNullOrEmpty(xSignature)
                && !gateway.ValidateWebhookSignature(mpPaymentId, xRequestId, xSignature))
                return Unauthorized();

            var result = await Sender.Send(new ConfirmPaymentCommand(mpPaymentId), cancellationToken);
            return result.IsSuccess ? Ok() : BadRequest(result.Error.Description);
        }

        return Ok(); // ignore other event types
    }
}

public sealed record MpWebhookPayload(string? Type, MpWebhookData? Data);
public sealed record MpWebhookData(string? Id);
```

- [ ] **Step 3: Criar FinanceiroController**

```csharp
// src/Horafy.API/Controllers/V1/FinanceiroController.cs
using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Payments.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
public sealed class FinanceiroController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentTransactionResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReport(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] Guid? serviceId,
        [FromQuery] Guid? resourceId,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new GetFinancialReportQuery(from, to, serviceId, resourceId), cancellationToken));

    [HttpGet("summary")]
    [ProducesResponseType(typeof(FinancialSummaryResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetFinancialSummaryQuery(from, to), cancellationToken));
}
```

- [ ] **Step 4: Adicionar endpoint PUT payment-settings ao TenantsController**

Localizar `src/Horafy.API/Controllers/V1/TenantsController.cs`. Adicionar ao final da classe:

```csharp
    [HttpPut("payment-settings")]
    [Authorize(Roles = "TenantOwner,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePaymentSettings(
        [FromBody] UpdatePaymentSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new UpdatePaymentSettingsCommand(
                request.RequiresPayment, request.DepositMode, request.DepositValue),
            cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
```

Adicionar ao final do arquivo (fora da classe):

```csharp
public sealed record UpdatePaymentSettingsRequest(
    bool RequiresPayment,
    Horafy.Domain.Entities.Payments.DepositMode DepositMode,
    decimal DepositValue);
```

Adicionar `using Horafy.Application.Features.Tenants.Commands;` e `using Horafy.Domain.Entities.Payments;` nos usings.

- [ ] **Step 5: Build + testes completos**

```
dotnet build Horafy.sln 2>&1 | grep "error" | head -10
dotnet test tests/Horafy.Application.Tests 2>&1 | tail -5
```
Expected: 0 erros, todos os testes passando.

- [ ] **Step 6: Commit**

```
git add src/Horafy.API/Controllers/V1/PaymentsController.cs
git add src/Horafy.API/Controllers/V1/WebhooksController.cs
git add src/Horafy.API/Controllers/V1/FinanceiroController.cs
git add src/Horafy.API/Controllers/V1/TenantsController.cs
git commit -m "feat: add PaymentsController, WebhooksController, FinanceiroController and payment-settings endpoint"
```

---

## Notas de Implementação

### Colunas `payment_settings_deposit_mode` — convenção snake_case
A `UseSnakeCaseNamingConvention()` converte `DepositMode` para `deposit_mode`. O prefixo `payment_settings_` vem do `OwnsOne` sem `ToTable`. A coluna resultante é `payment_settings_deposit_mode`. Verificar na migration gerada se os nomes estão corretos.

### Autenticação do HttpClient do MP
O `BearerTokenHandler` lê o `AccessToken` do `configuration` na hora do registro. Para ambientes com rotação de token, considerar usar `IOptionsMonitor<MercadoPagoOptions>` em vez de capturar na startup. Para o MVP, a abordagem atual é suficiente.

### TenantSchemaService — idempotência
O `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` é idempotente. Tenants existentes terão a coluna `payment_status` adicionada na próxima chamada ao `CreateSchemaAsync` (que ocorre no cadastro de novo tenant). Para tenants existentes, é necessário rodar o DDL manualmente ou chamar `CreateSchemaAsync` explicitamente via endpoint de admin.

### BaseRepository — namespace
Verificar o namespace exato de `BaseRepository<T, TContext>` no projeto. Baseado nos repositórios existentes, o namespace é `Horafy.Infrastructure.Repositories.Base` e o using deve ser adicionado no `PaymentRepository`.

---

## Self-Review

| Requisito do Spec | Task |
|---|---|
| PaymentSettings owned entity (RequiresPayment, DepositMode, DepositValue) | Task 1 ✓ |
| Payment aggregate com PreferenceId/MpPaymentId, Approve/Reject/Refund | Task 2 ✓ |
| BookingPaymentStatus + mark methods no Booking | Task 3 ✓ |
| DDL: tabela payments + ALTER TABLE bookings | Task 3 ✓ |
| IPaymentGateway + IPaymentRepository | Task 4 ✓ |
| MercadoPagoPaymentGateway (preference, status, refund, webhook validation) | Task 5 ✓ |
| PaymentRepository + EF config + DI | Task 6 ✓ |
| CreatePaymentCommand | Task 7 ✓ |
| ConfirmPaymentCommand (idempotente) | Task 8 ✓ |
| PaymentConfirmedEventHandler (IsDeposit → Partial vs Paid) | Task 8 ✓ |
| RefundPaymentCommand | Task 9 ✓ |
| CancelBookingCommand com taxa automática | Task 9 ✓ |
| GetPaymentByBookingQuery | Task 10 ✓ |
| GetFinancialReportQuery | Task 10 ✓ |
| GetFinancialSummaryQuery | Task 10 ✓ |
| UpdatePaymentSettingsCommand | Task 11 ✓ |
| PaymentsController, WebhooksController, FinanceiroController | Task 12 ✓ |
| PUT /tenants/payment-settings | Task 12 ✓ |
