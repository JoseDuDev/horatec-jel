# Sprint 6 — Pagamento: Design Spec

## Objetivo

Adicionar o módulo de pagamento ao Horafy: integração com Mercado Pago (Checkout Bricks), suporte a PIX, cartão de crédito/débito e boleto, três fluxos (imediato, link posterior, sinal/entrada), webhook idempotente, taxa de cancelamento automática e relatório financeiro.

## Decisões Arquiteturais

- **Gateway:** Mercado Pago Checkout Bricks (SDK JS frontend + Preferences API no backend)
- **Padrão:** Payment como agregado próprio com domain events → handlers atualizam Booking
- **Idempotência:** `ExternalPaymentId` com constraint UNIQUE — retentativas do MP são ignoradas naturalmente
- **Configuração por tenant:** `PaymentSettings` owned entity no `Tenant` — tenants sem pagamento seguem fluxo atual sem alteração
- **Relatório:** dois endpoints — transações brutas + agregados por período/serviço/recurso
- **Sem outbox neste sprint** — mensageria assíncrona (RabbitMQ) é escopo do Sprint 7

---

## 1. Modelo de Domínio

### 1.1 `PaymentSettings` — owned entity em `Tenant` (schema `public`)

```csharp
// src/Horafy.Domain/Entities/Tenants/PaymentSettings.cs
public sealed class PaymentSettings
{
    public bool    RequiresPayment { get; private set; } = false;
    public DepositMode DepositMode { get; private set; } = DepositMode.None;
    public decimal DepositValue   { get; private set; } = 0m;

    public static readonly PaymentSettings Default = new();

    public static PaymentSettings Create(bool requiresPayment, DepositMode mode, decimal value)
    {
        if (mode == DepositMode.Percentage && (value < 0 || value > 100))
            throw new ArgumentException("Percentual deve estar entre 0 e 100.");
        if (mode == DepositMode.FixedAmount && value < 0)
            throw new ArgumentException("Valor fixo não pode ser negativo.");
        return new() { RequiresPayment = requiresPayment, DepositMode = mode, DepositValue = value };
    }

    public decimal CalculateDepositAmount(decimal totalAmount) => DepositMode switch
    {
        DepositMode.Percentage  => Math.Round(totalAmount * DepositValue / 100, 2),
        DepositMode.FixedAmount => Math.Min(DepositValue, totalAmount),
        _                       => 0m
    };
}

public enum DepositMode { None = 0, Percentage = 1, FixedAmount = 2 }
```

`Tenant.cs` ganha:
```csharp
public PaymentSettings PaymentSettings { get; private set; } = PaymentSettings.Default;
public void UpdatePaymentSettings(bool requiresPayment, DepositMode mode, decimal value)
    => PaymentSettings = PaymentSettings.Create(requiresPayment, mode, value);
```

EF: `OwnsOne` em `TenantEntityConfiguration`, colunas `payment_settings_*` em `public.tenants`. EF migration obrigatória.

---

### 1.2 `Payment` — entidade tenant (schema `tenant_{slug}`)

**Dois IDs do Mercado Pago:** `PreferenceId` é criado ao iniciar o checkout; `MpPaymentId` chega pelo webhook quando o usuário conclui o pagamento. São distintos — a UNIQUE constraint de idempotência fica em `MpPaymentId`.

```csharp
// src/Horafy.Domain/Entities/Payments/Payment.cs
public sealed class Payment : BaseEntity
{
    private Payment() { }

    public Guid    BookingId     { get; private set; }
    public string  PreferenceId  { get; private set; } = default!; // MP preference_id (criado pelo backend)
    public string? MpPaymentId   { get; private set; }              // MP payment_id (chega pelo webhook)
    public PaymentMethod Method  { get; private set; }
    public PaymentStatus Status  { get; private set; } = PaymentStatus.Pending;
    public decimal Amount        { get; private set; }
    public decimal DepositAmount { get; private set; }  // 0 = pagamento integral
    public string? PaymentUrl    { get; private set; }  // Checkout Pro URL (para enviar link ao cliente)
    public DateTimeOffset? PaidAt    { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }

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

### 1.3 Enums

```csharp
// src/Horafy.Domain/Entities/Payments/PaymentMethod.cs
public enum PaymentMethod { Pix = 0, CreditCard = 1, DebitCard = 2, Boleto = 3 }

// src/Horafy.Domain/Entities/Payments/PaymentStatus.cs
public enum PaymentStatus { Pending = 0, Approved = 1, Rejected = 2, Refunded = 3, Cancelled = 4 }
```

### 1.4 `Booking` — campo adicionado

```csharp
public BookingPaymentStatus PaymentStatus { get; private set; } = BookingPaymentStatus.NotRequired;

public void MarkPaymentPending()   => PaymentStatus = BookingPaymentStatus.Pending;
public void MarkPaymentPaid()      => PaymentStatus = BookingPaymentStatus.Paid;
public void MarkPaymentPartial()   => PaymentStatus = BookingPaymentStatus.PartiallyPaid;
public void MarkPaymentRefunded()  => PaymentStatus = BookingPaymentStatus.Refunded;
```

```csharp
// src/Horafy.Domain/Entities/Bookings/BookingPaymentStatus.cs
public enum BookingPaymentStatus { NotRequired = 0, Pending = 1, Paid = 2, PartiallyPaid = 3, Refunded = 4 }
```

Coluna `payment_status VARCHAR(32)` adicionada à tabela `bookings` no DDL (TenantSchemaService).

---

### 1.5 Domain Events

```csharp
// src/Horafy.Domain/Events/Payments/
public sealed record PaymentCreatedEvent(Guid PaymentId, Guid BookingId, decimal Amount, PaymentMethod Method) : DomainEvent;
// IsDeposit = true → pagamento de sinal; handler marca booking como PartiallyPaid em vez de Paid
public sealed record PaymentConfirmedEvent(Guid PaymentId, Guid BookingId, bool IsDeposit) : DomainEvent;
public sealed record PaymentFailedEvent(Guid PaymentId, Guid BookingId) : DomainEvent;
```

---

## 2. Application Layer

### 2.1 `IPaymentGateway`

```csharp
// src/Horafy.Application/Interfaces/IPaymentGateway.cs
public interface IPaymentGateway
{
    Task<PaymentPreferenceResult> CreatePreferenceAsync(
        CreatePaymentPreferenceRequest request, CancellationToken ct = default);

    Task<PaymentStatusResult> GetPaymentStatusAsync(string externalPaymentId, CancellationToken ct = default);

    Task<RefundResult> RefundAsync(string mpPaymentId, decimal amount, CancellationToken ct = default);
}

public sealed record CreatePaymentPreferenceRequest(
    Guid BookingId, decimal Amount, decimal DepositAmount,
    PaymentMethod Method, string CustomerEmail,
    string BackUrl, string WebhookUrl);

public sealed record PaymentPreferenceResult(
    string PreferenceId,   // para inicializar Bricks no frontend
    string PaymentUrl,     // Checkout Pro URL para envio de link
    DateTimeOffset? ExpiresAt);

// GetPaymentStatusAsync retorna também o PreferenceId (via metadata do MP) para correlacionar com Payment
public sealed record PaymentStatusResult(
    string MpPaymentId, string PreferenceId,
    PaymentStatus Status, DateTimeOffset? PaidAt);
public sealed record RefundResult(bool Success, string? ErrorMessage);
```

### 2.2 `IPaymentRepository`

```csharp
// src/Horafy.Domain/Interfaces/Repositories/IPaymentRepository.cs
public interface IPaymentRepository : IRepository<Payment>
{
    Task<Payment?> GetByPreferenceIdAsync(string preferenceId, CancellationToken ct = default);
    Task<Payment?> GetByMpPaymentIdAsync(string mpPaymentId, CancellationToken ct = default);
    Task<Payment?> GetByBookingIdAsync(Guid bookingId, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> GetByPeriodAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
```

### 2.3 Comandos

**`CreatePaymentCommand`** — cria preferência no MP e salva `Payment` com status Pending.
```
Command: (Guid BookingId, PaymentMethod Method, string BackUrl)
Returns: Result<CreatePaymentResult>  // { PaymentId, PaymentUrl }
Handler: busca Booking + Tenant.PaymentSettings → calcula Amount + DepositAmount → IPaymentGateway.CreatePreferenceAsync → Payment.Create → booking.MarkPaymentPending → salva
```

**`ConfirmPaymentCommand`** — chamado pelo webhook handler após validar evento do MP.
```
Command: (string MpPaymentId)
Returns: Result
Handler:
  1. Idempotência: se já existe Payment com MpPaymentId = este, retorna Success
  2. Chama IPaymentGateway.GetPaymentStatusAsync(MpPaymentId) → obtém PreferenceId e status
  3. Busca Payment por PreferenceId
  4. Se status MP = approved → payment.Approve(MpPaymentId) → salva
     Se status MP = rejected/cancelled → payment.Reject(MpPaymentId) → salva
  5. Domain events disparam BookingConfirmationHandler / BookingPaymentFailedHandler
```

**`RefundPaymentCommand`** — estorno manual ou automático por cancelamento fora do prazo.
```
Command: (Guid PaymentId, decimal? Amount)  // Amount null = estorno total
Returns: Result
Handler: busca Payment → IPaymentGateway.RefundAsync → payment.Refund() → salva
```

### 2.4 Queries

**`GetPaymentByBookingQuery`** — `(Guid BookingId) → Result<PaymentResult?>`

**`GetFinancialReportQuery`** — transações brutas.
```
Query: (DateTimeOffset From, DateTimeOffset To, Guid? ServiceId, Guid? ResourceId)
Returns: Result<IReadOnlyList<PaymentTransactionResult>>
```

**`GetFinancialSummaryQuery`** — agregados.
```
Query: (DateTimeOffset From, DateTimeOffset To)
Returns: Result<FinancialSummaryResult>
// FinancialSummaryResult: TotalRevenue, TotalRefunded, NetRevenue,
//   ByDay: IReadOnlyList<DailySummary>,
//   ByService: IReadOnlyList<ServiceSummary>,
//   ByResource: IReadOnlyList<ResourceSummary>
```

### 2.5 Event Handlers

**`PaymentConfirmedEventHandler`** (`INotificationHandler<PaymentConfirmedEvent>`)
- Se `IsDeposit = false`: busca booking → `booking.Confirm()` + `booking.MarkPaymentPaid()` → salva
- Se `IsDeposit = true`: busca booking → `booking.Confirm()` + `booking.MarkPaymentPartial()` → salva (saldo restante exige segundo pagamento)

**`PaymentFailedEventHandler`** (`INotificationHandler<PaymentFailedEvent>`)
- Busca booking → se PaymentStatus for Pending → `booking.MarkPaymentPending()` mantém aberto (cliente pode tentar de novo)

### 2.6 `CancelBookingCommand` — atualização

Se tenant tem `CancellationPolicy.CancellationFeePercent > 0` e cancelamento está fora do prazo:
1. Busca `Payment` do booking
2. Se existe payment Approved → calcula taxa → `IPaymentGateway.RefundAsync(amount - fee)` → `payment.Refund()`
3. Aplica cancelamento normalmente

---

## 3. Infrastructure Layer

### 3.1 `MercadoPagoPaymentGateway`

```csharp
// src/Horafy.Infrastructure/Gateways/MercadoPagoPaymentGateway.cs
internal sealed class MercadoPagoPaymentGateway(
    IOptions<MercadoPagoOptions> options,
    ILogger<MercadoPagoPaymentGateway> logger,
    HttpClient httpClient) : IPaymentGateway
```

Configuração via `MercadoPagoOptions` (AccessToken, WebhookSecret). Registrada via `AddHttpClient<MercadoPagoPaymentGateway>`.

Endpoints MP utilizados:
- `POST /checkout/preferences` — cria preferência (PIX, cartão, boleto)
- `GET /v1/payments/{id}` — consulta status
- `POST /v1/payments/{id}/refunds` — estorno

Validação de webhook: header `x-signature` + `x-request-id` com HMAC-SHA256 usando `WebhookSecret`.

### 3.2 `PaymentRepository`

```csharp
// src/Horafy.Infrastructure/Repositories/PaymentRepository.cs
internal sealed class PaymentRepository(TenantDbContext context)
    : RepositoryBase<Payment>(context), IPaymentRepository
```

`GetByPeriodAsync` usa `AsNoTracking().Where(p => p.CreatedAt >= from && p.CreatedAt <= to)`.

### 3.3 DDL — `TenantSchemaService`

Adicionar ao `BuildSchemaScript`:

```sql
-- ── Pagamentos ─────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS {s}.payments (
    id                   UUID          NOT NULL DEFAULT gen_random_uuid(),
    booking_id           UUID          NOT NULL,
    method               VARCHAR(32)   NOT NULL,
    status               VARCHAR(32)   NOT NULL DEFAULT 'Pending',
    amount               NUMERIC(10,2) NOT NULL,
    deposit_amount       NUMERIC(10,2) NOT NULL DEFAULT 0,
    payment_url          VARCHAR(500),
    paid_at              TIMESTAMPTZ,
    expires_at           TIMESTAMPTZ,
    created_at           TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at           TIMESTAMPTZ,
    created_by           VARCHAR(256),
    updated_by           VARCHAR(256),
    is_deleted           BOOLEAN       NOT NULL DEFAULT FALSE,
    deleted_at           TIMESTAMPTZ,
    deleted_by           VARCHAR(256),
    CONSTRAINT pk_payments PRIMARY KEY (id),
    preference_id        VARCHAR(100)  NOT NULL,
    mp_payment_id        VARCHAR(100),
    CONSTRAINT fk_payments_bookings FOREIGN KEY (booking_id) REFERENCES {s}.bookings (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_payments_mp_payment_id
    ON {s}.payments (mp_payment_id)
    WHERE mp_payment_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_payments_booking_id
    ON {s}.payments (booking_id);

CREATE INDEX IF NOT EXISTS ix_payments_status_created
    ON {s}.payments (status, created_at DESC)
    WHERE is_deleted = FALSE;
```

Também adicionar coluna `payment_status` à tabela `bookings`:
```sql
ALTER TABLE IF EXISTS {s}.bookings
    ADD COLUMN IF NOT EXISTS payment_status VARCHAR(32) NOT NULL DEFAULT 'NotRequired';
```

### 3.4 EF Configuration

```csharp
// src/Horafy.Infrastructure/Persistence/TenantConfigurations/PaymentEntityConfiguration.cs
internal sealed class PaymentEntityConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PreferenceId).IsRequired().HasMaxLength(100);
        builder.Property(p => p.MpPaymentId).HasMaxLength(100);
        // UNIQUE parcial em MpPaymentId: garante idempotência sem conflito em payments ainda pendentes
        builder.HasIndex(p => p.MpPaymentId)
            .IsUnique()
            .HasFilter("mp_payment_id IS NOT NULL")
            .HasDatabaseName("uq_payments_mp_payment_id");
        builder.Property(p => p.Amount).HasColumnType("numeric(10,2)");
        builder.Property(p => p.DepositAmount).HasColumnType("numeric(10,2)");
        builder.Property(p => p.PaymentUrl).HasMaxLength(500);
        builder.HasIndex(p => p.BookingId).HasDatabaseName("ix_payments_booking_id");
    }
}
```

`TenantDbContext` ganha `DbSet<Payment> Payments`.

`BookingEntityConfiguration` atualizado para mapear `PaymentStatus`.

### 3.5 `appsettings.json` — nova seção

```json
"MercadoPago": {
  "AccessToken": "",
  "WebhookSecret": "",
  "NotificationUrl": "https://{tenant-slug}.horafy.com.br/webhooks/mercadopago"
}
```

### 3.6 DI Registration

```csharp
services.Configure<MercadoPagoOptions>(configuration.GetSection("MercadoPago"));
services.AddHttpClient<IPaymentGateway, MercadoPagoPaymentGateway>();
services.AddScoped<IPaymentRepository, PaymentRepository>();
```

---

## 4. API Layer

### 4.1 `PaymentsController`

```
POST   /api/v1/payments                    → CreatePaymentCommand      [Authorize]
GET    /api/v1/payments/booking/{bookingId} → GetPaymentByBookingQuery  [Authorize]
POST   /api/v1/payments/{id}/refund        → RefundPaymentCommand       [Authorize(Roles="TenantOwner,TenantAdmin,PlatformAdmin")]
```

### 4.2 `WebhooksController`

```
POST   /api/v1/webhooks/mercadopago   → sem [Authorize], valida HMAC interno
```

Handler valida assinatura → extrai `data.id` do payload → chama `ConfirmPaymentCommand`.

### 4.3 `FinanceiroController`

```
GET    /api/v1/financeiro              → GetFinancialReportQuery    [Authorize(Roles="TenantOwner,TenantAdmin,PlatformAdmin")]
GET    /api/v1/financeiro/summary      → GetFinancialSummaryQuery   [Authorize(Roles="TenantOwner,TenantAdmin,PlatformAdmin")]
```

Query params: `from`, `to`, `serviceId?`, `resourceId?`

### 4.4 `TenantController` — endpoint adicional

```
PUT    /api/v1/tenants/payment-settings  → UpdatePaymentSettingsCommand  [Authorize(Roles="TenantOwner,PlatformAdmin")]
```

---

## 5. Configuração do Tenant — `UpdatePaymentSettingsCommand`

```
Command: (bool RequiresPayment, DepositMode DepositMode, decimal DepositValue)
Handler: busca Tenant → tenant.UpdatePaymentSettings(...) → salva (HorafyDbContext)
```

---

## 6. Testes

Todos os handlers testados com Moq + xUnit (mesma convenção das sprints anteriores — classes `sealed`):

| Teste | Cenários |
|---|---|
| `CreatePaymentCommandHandlerTests` | sucesso PIX, sucesso cartão, booking não encontrado, gateway falha |
| `ConfirmPaymentCommandHandlerTests` | aprovação normal, idempotência (já Approved), payment não encontrado |
| `RefundPaymentCommandHandlerTests` | estorno total, estorno parcial, payment não aprovado |
| `PaymentConfirmedEventHandlerTests` | booking confirmado, booking já confirmado (idempotente) |
| `GetFinancialReportQueryHandlerTests` | retorno filtrado por período |
| `GetFinancialSummaryQueryHandlerTests` | totais corretos, agrupamentos por dia/serviço/recurso |
| `MercadoPagoWebhookValidationTests` | assinatura válida, assinatura inválida → 401 |

---

## 7. Fora do Escopo deste Sprint

- Notificações por WhatsApp/e-mail com link de pagamento (Sprint 7)
- UI de checkout (Sprint 8)
- Relatório exportável em CSV/PDF (Sprint 11)
- Gestão de planos SaaS / billing da plataforma (Sprint 12)
