# Plano de Implementação — Módulo de Locação (aluguel multi-dia)

> Objetivo: habilitar o Horafy a atender **locação de itens** (ferramentas, brinquedos,
> itens em geral) onde o item fica alugado por **1 ou mais dias**, reaproveitando ao
> máximo a infraestrutura existente (multi-tenant, pagamentos, carteira, vouchers,
> fidelidade, notificações).

## Diagnóstico do estado atual

O Horafy é, na arquitetura, um sistema de **agendamento por horário** construído em
torno de três premissas que conflitam com locação:

1. **Duração em minutos, dentro de um único dia** — `Service.DurationMinutes` (int) e
   `AvailabilityRule` com `StartTime`/`EndTime` (`TimeOnly`, intra-dia, por dia da
   semana). O gerador `GetAvailableSlotsQuery` percorre a janela de **um dia**.
2. **Capacidade 1 por recurso** — `Booking.OverlapsWith` + `IBookingRepository.HasConflictAsync`
   bloqueiam o recurso em qualquer sobreposição. Não há estoque/quantidade.
3. **Preço fixo por reserva** — `Service.Price` é valor único, não diária.

O que **já joga a favor**:

- `Booking.ScheduledAt`/`EndsAt` são `DateTimeOffset`, e `OverlapsWith` faz sobreposição
  de intervalos que **funciona atravessando dias**.
- Reuso pronto: multi-tenant, pagamentos com depósito/sinal (`Payment.depositAmount`),
  carteira (`Wallet.RefundFromBooking`/`DebitPayment`), vouchers, fidelidade, e itens de
  linha por reserva (`BookingService` com preço snapshot).
- Estorno já existe: `IPaymentGateway.RefundAsync`.

## Decisão de arquitetura

**Tratar locação como um novo *tipo de produto* (`RentableItem`) + um *motor de
disponibilidade próprio*, reutilizando o agregado `Booking` como a reserva.**

Por quê: o `Booking` já carrega todo o encanamento transversal — `Payment.BookingId`,
`PaymentConfirmedEvent` → confirma a reserva, `BookingCompletedEvent` → credita
carteira/fidelidade, status, snapshot de cliente, vouchers. O que **não** se reaproveita
é `Service`/`Resource` (capacidade 1, preço fixo) nem `GetAvailableSlotsQuery`
(intra-dia) — esses ganham caminho paralelo.

> **Alternativa rejeitada para o MVP:** criar um contexto `Rental` totalmente separado
> (`RentalItem` + `RentalReservation`). Mais limpo conceitualmente, porém obriga a
> duplicar/generalizar pagamento, carteira, fidelidade e notificações — custo alto sem
> ganho no curto prazo.

### Gotcha de schema (vale para TODAS as fases com dados)

Mudanças de modelo exigem **dois** lugares:
1. Uma **migração EF** (`dotnet ef migrations add ...`).
2. O **DDL bruto** em `TenantSchemaService.cs` (bootstrap por-tenant via
   `CREATE TABLE ... IF NOT EXISTS` / `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`).

Precedente: o commit `AddBookingPriceAndResourceName` alterou os dois.

---

## Fases

### Fase 0 — Fundação de dados e modo de reserva
**Objetivo:** introduzir o item alugável e o "modo" da reserva sem quebrar agendamento.

- `Booking`: adicionar `BookingKind { Appointment, Rental }` (default `Appointment`).
- Nova entidade **`RentableItem`** (`Entities/Rentals/`): `Name`, `Description`,
  `Category`, `Quantity` (estoque), `DailyRate`, `SecurityDeposit`, `BufferDays`,
  `ImageUrl`, `IsActive`.
- Config EF + DDL em `TenantSchemaService` + migração EF + `IRentableItemRepository`.

**Entregáveis:** entidade + estoque persistidos; `Booking.Kind` disponível.
**Testes:** unit de domínio. **Risco:** baixo. **Esforço:** ~1–2 dias.
> Detalhamento completo na seção [Fase 0 — Detalhe de implementação](#fase-0--detalhe-de-implementação).

### Fase 1 — Motor de disponibilidade por intervalo de dias + estoque
**Objetivo:** responder "o item X tem unidade livre entre as datas A e B?".

- Query **`GetRentalAvailabilityQuery(itemId, startDate, endDate)`** (`Features/Rentals/Queries/`),
  paralela a `GetAvailableSlotsQuery` (não a altera).
- Disponível = `RentableItem.Quantity − reservas de locação sobrepostas a [A,B] em status ativo`.
- Reusa `Booking.OverlapsWith`; novo `IBookingRepository.CountOverlappingRentalsAsync(itemId, start, end)`.
- **Buffer** (limpeza/conferência): `BufferDays` aplicado na sobreposição.

**Testes:** estoque esgotado, sobreposição parcial, buffer, devolução libera estoque.
**Risco:** médio. **Esforço:** ~2–3 dias.

### Fase 2 — Precificação por diária + caução
**Objetivo:** total = diária × nº de dias (+ caução).

- VO **`RentalPricing.CalculateTotal(dailyRate, days, deposit, tiers?)`** — começar
  simples (diária × dias), gancho para tabela (diária/semanal).
- Reusar `BookingService.Price` como snapshot da diária × dias por item; adicionar
  `Quantity`/`Days` na linha se permitir múltiplas unidades.
- Caução via `Payment.depositAmount` (já existe); reembolso definido na Fase 5.

**Testes:** unit de precificação; ajustar `CreatePaymentWithDiscountsTests`.
**Risco:** baixo-médio. **Esforço:** ~2 dias.

### Fase 3 — Comando de reserva de locação + API
**Objetivo:** criar uma reserva de locação ponta a ponta no backend.

- **`CreateRentalBookingCommand(items[], startDate, endDate, notes)`** — espelha
  `CreateBookingCommand`; valida disponibilidade (F1), calcula preço (F2), seta
  `Kind=Rental`, `ScheduledAt=retirada`, `EndsAt=devolução`.
- **`RentalsController`**: `GET /rentals/items`, `GET /rentals/items/{id}/availability`,
  `POST /rentals/bookings`.
- Reusar fluxo de pagamento/webhook/confirmação **sem alteração**.

**Testes:** handler (conflito de estoque → falha) + integração de controller.
**Risco:** médio. **Esforço:** ~3 dias.

### Fase 4 — Frontend: catálogo + seleção por datas + checkout
**Objetivo:** UX de locação no portal.

- Fluxo paralelo ao `BookingWizard` (não reusar `WizardStepSlot`, intra-dia):
  catálogo (padrão `ServiceCard`) → date-range picker (retirada/devolução) com
  disponibilidade/estoque → confirmação reusando `WizardStepConfirm` +
  pagamento/carteira/voucher.
- `lib/api/` + tipos novos para rentals; reuso de `portalWalletApi` e vouchers.
- Admin: CRUD de `RentableItem` (estoque, diária, caução).

**Testes:** componentes-chave; preparar E2E (Fase 6).
**Risco:** médio-alto. **Esforço:** ~4–6 dias.

### Fase 5 — Ciclo de vida: retirada, devolução, atraso/multa, caução
**Objetivo:** operação real da locação.

- Estados além de Confirmed/Completed: **`PickedUp`**, **`Returned`**, **`Overdue`**.
- Ações admin: marcar retirada; marcar devolução (libera estoque imediatamente p/ F1).
- **Multa por atraso**: regra (valor/dia) → cobrança extra ao devolver fora do prazo.
- **Caução**: reembolso via carteira (`Wallet.RefundFromBooking`) ou estorno no gateway
  (`IPaymentGateway.RefundAsync`).
- **Concorrência/overbooking**: reserva atômica de estoque (lock otimista/constraint)
  quando estoque > 1 e reservas simultâneas.

**Testes:** estados/multa; teste de concorrência de estoque.
**Risco:** **alto** (concorrência + dinheiro). **Esforço:** ~4–5 dias.

### Fase 6 — Financeiro, notificações e E2E
**Objetivo:** fechar produto.

- **Financeiro** (`FinanceiroController`): receita de locação, caução retida vs.
  devolvida, multas.
- **Notificações**: lembrete de devolução (D-1), aviso de atraso (Quartz já no projeto).
- **E2E** (Playwright + docker-compose.e2e): "cliente aluga item por 3 dias → paga →
  admin marca retirada → devolução → caução estornada na carteira".

**Risco:** baixo-médio. **Esforço:** ~3–4 dias.

---

## Resumo

| Fase | Foco | Esforço | Risco |
|------|------|---------|-------|
| 0 | Item alugável + estoque + `Booking.Kind` | 1–2d | Baixo |
| 1 | Disponibilidade por intervalo + estoque | 2–3d | Médio |
| 2 | Diária + caução | 2d | Baixo-Médio |
| 3 | Comando de reserva + API | 3d | Médio |
| 4 | Frontend catálogo/datas/checkout | 4–6d | Médio-Alto |
| 5 | Retirada/devolução/multa/caução/concorrência | 4–5d | **Alto** |
| 6 | Financeiro + notificações + E2E | 3–4d | Baixo-Médio |

**Total estimado:** ~19–27 dias-dev (1 pessoa). Caminho crítico: **Fase 5**.

**MVP enxuto** (validar o negócio cedo): Fases 0–4 com estoque simples e **caução
manual** (sem multa automática) → ~12–16 dias. Fases 5–6 entram depois.

---

## Fase 0 — Detalhe de implementação

Meta: persistir o conceito de **item alugável com estoque** e marcar o **modo** da
reserva, **sem alterar** o fluxo de agendamento existente. Nenhuma regra de
disponibilidade/preço entra aqui — só fundação.

### Convenções observadas no projeto (a seguir à risca)

- Entidades herdam de `BaseEntity` (Id, auditoria, soft-delete, domain events) e têm
  construtor privado `{ }` para o EF + factory `Create(...)` com validação.
- Configs EF ficam em `Infrastructure/Persistence/TenantConfigurations/`, **sem**
  `HasSchema` (tabela resolvida pelo `search_path` do tenant), e são aplicadas via
  `ApplyConfigurationsFromAssembly(...)` no `TenantDbContext`.
- Toda tabela de tenant é criada **também** no DDL bruto de `TenantSchemaService.cs`.
- Repositórios: interface em `Domain/Interfaces/Repositories/`, impl `internal sealed`
  em `Infrastructure/Repositories/` herdando `BaseRepository<T, TenantDbContext>`,
  registrada em `DependencyInjection.cs`.
- Colunas em `snake_case`; `decimal` → `numeric(10,2)`.

### 0.1 — Domínio: enum `BookingKind`

Arquivo novo: `src/Horafy.Domain/Entities/Bookings/BookingKind.cs`

```csharp
namespace Horafy.Domain.Entities.Bookings;

public enum BookingKind
{
    Appointment = 0, // agendamento por horário (comportamento atual)
    Rental      = 1, // locação por período (1+ dias)
}
```

Em `Booking.cs` adicionar a propriedade (default mantém compatibilidade):

```csharp
public BookingKind Kind { get; private set; } = BookingKind.Appointment;
```

> Nesta fase **não** mudamos `Booking.Create`. A criação de locação virá na Fase 3 por
> uma factory dedicada (`Booking.CreateRental(...)`), evitando poluir o fluxo atual.

### 0.2 — Domínio: entidade `RentableItem`

Arquivo novo: `src/Horafy.Domain/Entities/Rentals/RentableItem.cs`

```csharp
using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Rentals;

/// <summary>
/// Item disponível para locação (ferramenta, brinquedo, etc.).
/// Reside no schema tenant_{slug}. Capacidade = Quantity (estoque).
/// </summary>
public sealed class RentableItem : BaseEntity
{
    private RentableItem() { } // EF Core

    public string  Name            { get; private set; } = default!;
    public string? Description     { get; private set; }
    public string? Category        { get; private set; }

    /// <summary>Estoque total de unidades idênticas.</summary>
    public int     Quantity        { get; private set; }

    /// <summary>Valor da diária (BRL).</summary>
    public decimal DailyRate       { get; private set; }

    /// <summary>Caução exigida por unidade (BRL). 0 = sem caução.</summary>
    public decimal SecurityDeposit { get; private set; }

    /// <summary>Dias de bloqueio após a devolução (limpeza/conferência).</summary>
    public int     BufferDays      { get; private set; }

    public string? ImageUrl        { get; private set; }
    public bool    IsActive        { get; private set; } = true;

    public static RentableItem Create(
        string name,
        int quantity,
        decimal dailyRate,
        decimal securityDeposit = 0,
        int bufferDays = 0,
        string? description = null,
        string? category = null,
        string? imageUrl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (quantity <= 0)
            throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(quantity));
        if (dailyRate < 0)
            throw new ArgumentException("Diária não pode ser negativa.", nameof(dailyRate));
        if (securityDeposit < 0)
            throw new ArgumentException("Caução não pode ser negativa.", nameof(securityDeposit));
        if (bufferDays < 0)
            throw new ArgumentException("Buffer não pode ser negativo.", nameof(bufferDays));

        return new RentableItem
        {
            Name            = name.Trim(),
            Quantity        = quantity,
            DailyRate       = dailyRate,
            SecurityDeposit = securityDeposit,
            BufferDays      = bufferDays,
            Description     = description?.Trim(),
            Category        = category?.Trim(),
            ImageUrl        = imageUrl?.Trim(),
        };
    }

    public void Update(string name, int quantity, decimal dailyRate, decimal securityDeposit,
        int bufferDays, string? description, string? category, string? imageUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (quantity <= 0) throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(quantity));
        if (dailyRate < 0) throw new ArgumentException("Diária não pode ser negativa.", nameof(dailyRate));

        Name            = name.Trim();
        Quantity        = quantity;
        DailyRate       = dailyRate;
        SecurityDeposit = securityDeposit;
        BufferDays      = bufferDays;
        Description     = description?.Trim();
        Category        = category?.Trim();
        ImageUrl        = imageUrl?.Trim();
        UpdatedAt       = DateTimeOffset.UtcNow;
    }

    public void Activate()   { IsActive = true;  UpdatedAt = DateTimeOffset.UtcNow; }
    public void Deactivate() { IsActive = false; UpdatedAt = DateTimeOffset.UtcNow; }
}
```

### 0.3 — Config EF

Arquivo novo:
`src/Horafy.Infrastructure/Persistence/TenantConfigurations/RentableItemEntityConfiguration.cs`

```csharp
using Horafy.Domain.Entities.Rentals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class RentableItemEntityConfiguration : IEntityTypeConfiguration<RentableItem>
{
    public void Configure(EntityTypeBuilder<RentableItem> builder)
    {
        builder.ToTable("rentable_items"); // sem HasSchema — search_path do tenant

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Name).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Description).HasMaxLength(1000);
        builder.Property(i => i.Category).HasMaxLength(100);
        builder.Property(i => i.Quantity);
        builder.Property(i => i.DailyRate).HasColumnType("numeric(10,2)");
        builder.Property(i => i.SecurityDeposit).HasColumnType("numeric(10,2)");
        builder.Property(i => i.BufferDays);
        builder.Property(i => i.ImageUrl).HasMaxLength(2000);
        builder.Property(i => i.IsActive);

        builder.HasIndex(i => i.Name).HasDatabaseName("ix_rentable_items_name");
        builder.HasIndex(i => i.IsActive).HasDatabaseName("ix_rentable_items_is_active");
    }
}
```

Em `TenantDbContext.cs` adicionar o `DbSet` (as configs já são auto-aplicadas pelo
`ApplyConfigurationsFromAssembly`, então **só** o DbSet é necessário):

```csharp
public DbSet<RentableItem> RentableItems => Set<RentableItem>();
```

> Adicionar o mesmo `DbSet<RentableItem>` em `HorafyDbContext` apenas se as migrações de
> tenant forem geradas a partir dele (confirmar qual contexto a migração usa — ver 0.5).
> O snapshot atual é `TenantDbContextModelSnapshot`, logo a migração sai do
> `TenantDbContext`.

### 0.4 — DDL no `TenantSchemaService`

Em `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs`, logo após o bloco
da tabela `services` (após o índice `ix_services_name`, ~linha 60), inserir:

```sql
        -- ── Itens de Locação ───────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.rentable_items (
            id                UUID          NOT NULL DEFAULT gen_random_uuid(),
            name              VARCHAR(200)  NOT NULL,
            description       TEXT,
            category          VARCHAR(100),
            quantity          INT           NOT NULL DEFAULT 1,
            daily_rate        NUMERIC(10,2) NOT NULL DEFAULT 0,
            security_deposit  NUMERIC(10,2) NOT NULL DEFAULT 0,
            buffer_days       INT           NOT NULL DEFAULT 0,
            image_url         VARCHAR(2000),
            is_active         BOOLEAN       NOT NULL DEFAULT TRUE,
            created_at        TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
            updated_at        TIMESTAMPTZ,
            created_by        VARCHAR(256),
            updated_by        VARCHAR(256),
            is_deleted        BOOLEAN       NOT NULL DEFAULT FALSE,
            deleted_at        TIMESTAMPTZ,
            deleted_by        VARCHAR(256),
            CONSTRAINT pk_rentable_items PRIMARY KEY (id)
        );

        CREATE INDEX IF NOT EXISTS ix_rentable_items_name
            ON {s}.rentable_items (name);
```

E, no bloco de `ALTER TABLE ... IF NOT EXISTS` (migração idempotente para tenants já
existentes — onde hoje ficam os ALTERs de `payment_status`/`resource_name`/`price`),
acrescentar a coluna `kind` em `bookings`:

```sql
        ALTER TABLE {s}.bookings
            ADD COLUMN IF NOT EXISTS kind INT NOT NULL DEFAULT 0;
```

### 0.5 — Migração EF

Mapear `Booking.Kind` na config de booking (`BookingEntityConfiguration`):

```csharp
builder.Property(b => b.Kind).HasColumnName("kind").HasDefaultValue(BookingKind.Appointment);
```

Gerar a migração (contexto do tenant — o que possui `TenantDbContextModelSnapshot`):

```bash
dotnet ef migrations add AddRentableItemsAndBookingKind \
  --project src/Horafy.Infrastructure \
  --startup-project src/Horafy.API \
  --context TenantDbContext
```

Conferir no `Up()`: `CreateTable("rentable_items", ...)` + `AddColumn<int>("kind",
"bookings", defaultValue: 0)`. O `Down()` deve `DropTable`/`DropColumn`
correspondentes. Validar que o `TenantDbContextModelSnapshot` foi atualizado.

> **Importante:** o `defaultValue` do `kind` (0 = Appointment) garante que reservas
> existentes continuem como agendamento.

### 0.6 — Repositório

Interface — `src/Horafy.Domain/Interfaces/Repositories/IRentableItemRepository.cs`:

```csharp
using Horafy.Domain.Entities.Rentals;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IRentableItemRepository : IRepository<RentableItem>
{
    Task<IReadOnlyList<RentableItem>> GetActiveAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RentableItem>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}
```

Implementação — `src/Horafy.Infrastructure/Repositories/RentableItemRepository.cs`
(espelha `ServiceRepository`):

```csharp
using Horafy.Domain.Entities.Rentals;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class RentableItemRepository(TenantDbContext context)
    : BaseRepository<RentableItem, TenantDbContext>(context), IRentableItemRepository
{
    public async Task<IReadOnlyList<RentableItem>> GetActiveAsync(CancellationToken ct = default) =>
        await DbSet.AsNoTracking().Where(i => i.IsActive).OrderBy(i => i.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<RentableItem>> GetByIdsAsync(
        IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        return await DbSet.AsNoTracking().Where(i => idList.Contains(i.Id)).ToListAsync(ct);
    }
}
```

Registro em `src/Horafy.Infrastructure/DependencyInjection.cs` (junto aos demais
`AddScoped<I...Repository, ...>`):

```csharp
services.AddScoped<IRentableItemRepository, RentableItemRepository>();
```

### 0.7 — Testes da Fase 0

`tests/Horafy.Domain.Tests/Rentals/RentableItemTests.cs`:

- `Create` válido popula campos e `IsActive=true`.
- `Create` lança em `name` vazio, `quantity <= 0`, `dailyRate < 0`, `securityDeposit < 0`.
- `Update` altera campos e seta `UpdatedAt`.
- `Activate`/`Deactivate` alternam `IsActive`.
- `Booking` novo nasce com `Kind == Appointment` (garantia de compatibilidade).

### Checklist de conclusão da Fase 0

- [ ] `BookingKind.cs` + `Booking.Kind` (default Appointment)
- [ ] `RentableItem` (entidade + factory + Update/Activate/Deactivate)
- [ ] `RentableItemEntityConfiguration` + `DbSet` no `TenantDbContext`
- [ ] `Booking.Kind` mapeado em `BookingEntityConfiguration`
- [ ] DDL de `rentable_items` + `ALTER bookings ADD kind` no `TenantSchemaService`
- [ ] Migração `AddRentableItemsAndBookingKind` (Up/Down + snapshot)
- [ ] `IRentableItemRepository` + impl + registro DI
- [ ] Testes de domínio verdes
- [ ] `dotnet build` + `dotnet test` verdes; suíte E2E ainda 7/7 (sem regressão)

> Ao fim da Fase 0 **nada muda para o usuário** — é fundação. O valor aparece a partir
> da Fase 1 (disponibilidade) e, de forma visível, na Fase 4 (UX).
