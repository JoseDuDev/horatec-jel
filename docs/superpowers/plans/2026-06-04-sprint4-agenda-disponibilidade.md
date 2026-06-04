# Sprint 4 — Agenda & Disponibilidade

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrar `Professional` para `Resource` (com `ResourceType` enum), adicionar as entidades `BusinessHours`, `AvailabilityRule`, `AvailabilityException` e `ResourceService`, e implementar o algoritmo de disponibilidade (`GetAvailableSlotsQuery`) com proteção contra double-booking.

**Architecture:** `Resource` é a entidade genérica que substitui `Professional` — um barbeiro, uma quadra, uma sala são todos `Resource` com `ResourceType` diferente. `AvailabilityRule` define a grade semanal de cada recurso; `AvailabilityException` cobre folgas e feriados; `BusinessHours` define o horário de funcionamento do tenant. O algoritmo de slots gera os horários livres interseccionando essas três fontes e subtraindo os `Booking` existentes.

**Tech Stack:** .NET 8, C#, EF Core 8, PostgreSQL 16, MediatR, FluentValidation, xUnit, FluentAssertions

---

## Mapa de Arquivos

### Deletar
- `src/Horafy.Domain/Entities/Professionals/Professional.cs`
- `src/Horafy.Domain/Interfaces/Repositories/IProfessionalRepository.cs`
- `src/Horafy.Application/Features/Professionals/` (diretório inteiro)
- `src/Horafy.Infrastructure/Persistence/TenantConfigurations/ProfessionalEntityConfiguration.cs`
- `src/Horafy.Infrastructure/Repositories/ProfessionalRepository.cs`
- `src/Horafy.API/Controllers/V1/ProfessionalsController.cs`


### Criar
| Arquivo | Responsabilidade |
|---|---|
| `src/Horafy.Domain/Entities/Resources/ResourceType.cs` | Enum dos tipos de recurso |
| `src/Horafy.Domain/Entities/Resources/Resource.cs` | Entidade agregada (substitui Professional) |
| `src/Horafy.Domain/Entities/Resources/ResourceService.cs` | Join table Resource ↔ Service |
| `src/Horafy.Domain/Entities/Availability/BusinessHours.cs` | Grade de funcionamento semanal do tenant |
| `src/Horafy.Domain/Entities/Availability/AvailabilityRule.cs` | Disponibilidade regular de um recurso |
| `src/Horafy.Domain/Entities/Availability/AvailabilityException.cs` | Folga, feriado ou horário especial |
| `src/Horafy.Domain/Interfaces/Repositories/IResourceRepository.cs` | Contrato do repo de recursos |
| `src/Horafy.Domain/Interfaces/Repositories/IAvailabilityRepository.cs` | Contrato do repo de disponibilidade |
| `src/Horafy.Infrastructure/Persistence/TenantConfigurations/ResourceEntityConfiguration.cs` | Mapeamento EF de Resource |
| `src/Horafy.Infrastructure/Persistence/TenantConfigurations/ResourceServiceEntityConfiguration.cs` | Mapeamento EF de ResourceService |
| `src/Horafy.Infrastructure/Persistence/TenantConfigurations/BusinessHoursEntityConfiguration.cs` | Mapeamento EF de BusinessHours |
| `src/Horafy.Infrastructure/Persistence/TenantConfigurations/AvailabilityRuleEntityConfiguration.cs` | Mapeamento EF de AvailabilityRule |
| `src/Horafy.Infrastructure/Persistence/TenantConfigurations/AvailabilityExceptionEntityConfiguration.cs` | Mapeamento EF de AvailabilityException |
| `src/Horafy.Infrastructure/Repositories/ResourceRepository.cs` | Implementação do repo de recursos |
| `src/Horafy.Infrastructure/Repositories/AvailabilityRepository.cs` | Implementação do repo de disponibilidade |
| `src/Horafy.Application/Features/Resources/ResourceErrors.cs` | Erros do módulo Resources |
| `src/Horafy.Application/Features/Resources/Commands/CreateResourceCommand.cs` | Criar recurso |
| `src/Horafy.Application/Features/Resources/Commands/UpdateResourceCommand.cs` | Atualizar recurso |
| `src/Horafy.Application/Features/Resources/Commands/DeleteResourceCommand.cs` | Deletar recurso (soft) |
| `src/Horafy.Application/Features/Resources/Commands/AddResourceServiceCommand.cs` | Vincular serviço ao recurso |
| `src/Horafy.Application/Features/Resources/Commands/RemoveResourceServiceCommand.cs` | Desvincular serviço |
| `src/Horafy.Application/Features/Resources/Queries/GetResourcesQuery.cs` | Listar recursos |
| `src/Horafy.Application/Features/Resources/Queries/GetResourceByIdQuery.cs` | Buscar recurso por id |
| `src/Horafy.Application/Features/Availability/AvailabilityErrors.cs` | Erros do módulo Availability |
| `src/Horafy.Application/Features/Availability/Commands/SetBusinessHoursCommand.cs` | Definir horários do tenant |
| `src/Horafy.Application/Features/Availability/Commands/SetAvailabilityRuleCommand.cs` | Definir grade de um recurso |
| `src/Horafy.Application/Features/Availability/Commands/SetAvailabilityExceptionCommand.cs` | Definir exceção de disponibilidade |
| `src/Horafy.Application/Features/Availability/Queries/GetAvailableSlotsQuery.cs` | Algoritmo de slots livres |
| `src/Horafy.API/Controllers/V1/ResourcesController.cs` | Endpoints de recursos |
| `src/Horafy.API/Controllers/V1/AvailabilityController.cs` | Endpoints de disponibilidade + slots |
| `tests/Horafy.Domain.Tests/Entities/ResourceTests.cs` | Testes de domínio do Resource |
| `tests/Horafy.Application.Tests/Availability/GetAvailableSlotsQueryHandlerTests.cs` | Testes do algoritmo de slots |

### Modificar
| Arquivo | O que muda |
|---|---|
| `src/Horafy.Domain/Entities/Bookings/Booking.cs` | `ProfessionalId` → `ResourceId` |
| `src/Horafy.Domain/Events/Bookings/BookingCreatedEvent.cs` | `ProfessionalId` → `ResourceId` |
| `src/Horafy.Domain/Interfaces/Repositories/IBookingRepository.cs` | `GetByProfessionalAsync` → `GetByResourceAsync`; atualiza `HasConflictAsync` |
| `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs` | Remove Professional, adiciona Resource + entidades de disponibilidade |
| `src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingEntityConfiguration.cs` | Atualiza índice `professional_id` → `resource_id` |
| `src/Horafy.Infrastructure/Repositories/BookingRepository.cs` | `professionalId` → `resourceId` |
| `src/Horafy.Infrastructure/DependencyInjection.cs` | Remove Professional repos, adiciona Resource + Availability |
| `src/Horafy.Application/Interfaces/ITenantUnitOfWork.cs` | Adiciona `BeginTransactionAsync` |
| `src/Horafy.Infrastructure/Persistence/TenantUnitOfWork.cs` | Implementa `BeginTransactionAsync` |
| `src/Horafy.Application/Features/Bookings/BookingErrors.cs` | Renomeia `ProfessionalNotFound` → `ResourceNotFound`; adiciona `SlotNotAvailable` |
| `src/Horafy.Application/Features/Bookings/Commands/CreateBookingCommand.cs` | `ProfessionalId` → `ResourceId`; usa transaction SERIALIZABLE |
| `src/Horafy.Application/Features/Bookings/Queries/GetBookingsQuery.cs` | `ProfessionalId` → `ResourceId` no parâmetro e no `BookingResult` |
| `src/Horafy.API/Controllers/V1/BookingsController.cs` | `ProfessionalId` → `ResourceId` no request |
| `tests/Horafy.Domain.Tests/Entities/BookingTests.cs` | Renomeia `professionalId` → `resourceId` |

---

## Task 1: Migrar Professional → Resource (Domain Layer)

**Files:**
- Delete: `src/Horafy.Domain/Entities/Professionals/Professional.cs`
- Delete: `src/Horafy.Domain/Interfaces/Repositories/IProfessionalRepository.cs`
- Create: `src/Horafy.Domain/Entities/Resources/ResourceType.cs`
- Create: `src/Horafy.Domain/Entities/Resources/Resource.cs`
- Create: `src/Horafy.Domain/Interfaces/Repositories/IResourceRepository.cs`
- Modify: `src/Horafy.Domain/Entities/Bookings/Booking.cs`
- Modify: `src/Horafy.Domain/Events/Bookings/BookingCreatedEvent.cs`
- Modify: `src/Horafy.Domain/Interfaces/Repositories/IBookingRepository.cs`
- Test: `tests/Horafy.Domain.Tests/Entities/ResourceTests.cs`

- [ ] **Step 1: Escrever testes de domínio para Resource (failing)**

Criar `tests/Horafy.Domain.Tests/Entities/ResourceTests.cs`:

```csharp
using FluentAssertions;
using Horafy.Domain.Entities.Resources;
using Xunit;

namespace Horafy.Domain.Tests.Entities;

public class ResourceTests
{
    [Fact]
    public void Create_Professional_SetsCorrectType()
    {
        var resource = Resource.Create("João Barbeiro", ResourceType.Professional,
            email: "joao@barbearia.com", specialty: "Cabelo e Barba");

        resource.Name.Should().Be("João Barbeiro");
        resource.Type.Should().Be(ResourceType.Professional);
        resource.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_Court_SetsCorrectType()
    {
        var resource = Resource.Create("Quadra 1", ResourceType.Court);

        resource.Type.Should().Be(ResourceType.Court);
        resource.Specialty.Should().BeNull();
    }

    [Fact]
    public void Create_EmptyName_ThrowsArgumentException()
    {
        var act = () => Resource.Create("  ", ResourceType.Professional);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deactivate_ActiveResource_SetsIsActiveFalse()
    {
        var resource = Resource.Create("Sala 1", ResourceType.PhysicalSpace);
        resource.Deactivate();
        resource.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_InactiveResource_SetsIsActiveTrue()
    {
        var resource = Resource.Create("Sala 1", ResourceType.PhysicalSpace);
        resource.Deactivate();
        resource.Activate();
        resource.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Update_ChangesAllFields()
    {
        var resource = Resource.Create("Nome Antigo", ResourceType.Professional, email: "old@email.com");
        resource.Update("Nome Novo", "new@email.com", "11999999999", "Nova Especialidade", "Bio nova", null);

        resource.Name.Should().Be("Nome Novo");
        resource.Email.Should().Be("new@email.com");
        resource.Specialty.Should().Be("Nova Especialidade");
    }
}
```

- [ ] **Step 2: Rodar testes — verificar falha**

```
dotnet test tests/Horafy.Domain.Tests --filter "ResourceTests" -v minimal
```
Esperado: erro de compilação (tipos não existem ainda).

- [ ] **Step 3: Criar ResourceType enum**

Criar `src/Horafy.Domain/Entities/Resources/ResourceType.cs`:

```csharp
namespace Horafy.Domain.Entities.Resources;

public enum ResourceType
{
    Professional,
    PhysicalSpace,
    Equipment,
    Court
}
```

- [ ] **Step 4: Criar entidade Resource**

Criar `src/Horafy.Domain/Entities/Resources/Resource.cs`:

```csharp
using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Resources;

public sealed class Resource : BaseEntity
{
    private Resource() { }

    public string Name { get; private set; } = default!;
    public ResourceType Type { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Specialty { get; private set; }
    public string? Bio { get; private set; }
    public string? AvatarUrl { get; private set; }
    public Guid? UserId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static Resource Create(
        string name,
        ResourceType type,
        string? email = null,
        string? phone = null,
        string? specialty = null,
        string? bio = null,
        string? avatarUrl = null,
        Guid? userId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Resource
        {
            Name      = name.Trim(),
            Type      = type,
            Email     = email?.ToLowerInvariant().Trim(),
            Phone     = phone?.Trim(),
            Specialty = specialty?.Trim(),
            Bio       = bio?.Trim(),
            AvatarUrl = avatarUrl,
            UserId    = userId
        };
    }

    public void Update(string name, string? email, string? phone,
        string? specialty, string? bio, string? avatarUrl)
    {
        Name      = name.Trim();
        Email     = email?.ToLowerInvariant().Trim();
        Phone     = phone?.Trim();
        Specialty = specialty?.Trim();
        Bio       = bio?.Trim();
        AvatarUrl = avatarUrl;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()   { IsActive = true;  UpdatedAt = DateTimeOffset.UtcNow; }
    public void Deactivate() { IsActive = false; UpdatedAt = DateTimeOffset.UtcNow; }
}
```

- [ ] **Step 5: Criar IResourceRepository**

Criar `src/Horafy.Domain/Interfaces/Repositories/IResourceRepository.cs`:

```csharp
using Horafy.Domain.Entities.Resources;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IResourceRepository : IRepository<Resource>
{
    Task<IReadOnlyList<Resource>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Resource>> GetByTypeAsync(ResourceType type, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 6: Atualizar Booking — ProfessionalId → ResourceId**

Editar `src/Horafy.Domain/Entities/Bookings/Booking.cs`. Substituir o parâmetro `professionalId` por `resourceId` em toda a entidade:

```csharp
using Horafy.Domain.Entities.Base;
using Horafy.Domain.Events.Bookings;

namespace Horafy.Domain.Entities.Bookings;

public sealed class Booking : BaseEntity
{
    private Booking() { }

    public Guid ServiceId    { get; private set; }
    public Guid ResourceId   { get; private set; }
    public Guid CustomerId   { get; private set; }

    public string CustomerName  { get; private set; } = default!;
    public string CustomerEmail { get; private set; } = default!;

    public DateTimeOffset ScheduledAt     { get; private set; }
    public DateTimeOffset EndsAt          { get; private set; }
    public int            DurationMinutes { get; private set; }

    public string? Notes { get; private set; }

    public BookingStatus Status             { get; private set; } = BookingStatus.Pending;
    public string?       CancellationReason { get; private set; }

    public Guid? RecurrenceGroupId { get; private set; }

    public DateTimeOffset? ConfirmedAt  { get; private set; }
    public DateTimeOffset? CancelledAt  { get; private set; }
    public DateTimeOffset? CompletedAt  { get; private set; }

    public static Booking Create(
        Guid serviceId,
        Guid resourceId,
        Guid customerId,
        string customerName,
        string customerEmail,
        DateTimeOffset scheduledAt,
        int durationMinutes,
        string? notes = null)
    {
        if (scheduledAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException("A data do agendamento deve ser futura.", nameof(scheduledAt));

        if (durationMinutes <= 0)
            throw new ArgumentException("Duração deve ser maior que zero.", nameof(durationMinutes));

        var booking = new Booking
        {
            ServiceId       = serviceId,
            ResourceId      = resourceId,
            CustomerId      = customerId,
            CustomerName    = customerName.Trim(),
            CustomerEmail   = customerEmail.ToLowerInvariant().Trim(),
            ScheduledAt     = scheduledAt,
            EndsAt          = scheduledAt.AddMinutes(durationMinutes),
            DurationMinutes = durationMinutes,
            Notes           = notes?.Trim()
        };

        booking.RaiseDomainEvent(new BookingCreatedEvent(
            booking.Id, serviceId, resourceId, customerId, scheduledAt));

        return booking;
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
        && ScheduledAt < end
        && EndsAt > start;
}
```

- [ ] **Step 7: Atualizar BookingCreatedEvent**

Editar `src/Horafy.Domain/Events/Bookings/BookingCreatedEvent.cs`:

```csharp
using Horafy.Domain.Events.Base;

namespace Horafy.Domain.Events.Bookings;

public sealed record BookingCreatedEvent(
    Guid BookingId,
    Guid ServiceId,
    Guid ResourceId,
    Guid CustomerId,
    DateTimeOffset ScheduledAt) : DomainEvent;
```

- [ ] **Step 8: Atualizar IBookingRepository**

Substituir o conteúdo de `src/Horafy.Domain/Interfaces/Repositories/IBookingRepository.cs`:

```csharp
using Horafy.Domain.Entities.Bookings;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IBookingRepository : IRepository<Booking>
{
    Task<IReadOnlyList<Booking>> GetByResourceAsync(
        Guid resourceId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<bool> HasConflictAsync(
        Guid resourceId,
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? excludeBookingId = null,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 9: Atualizar BookingTests — professionalId → resourceId**

Em `tests/Horafy.Domain.Tests/Entities/BookingTests.cs`, substituir todas as ocorrências de `professionalId:` por `resourceId:` no helper `CreateFutureBooking` e nos testes que criam bookings diretamente:

```csharp
private static Booking CreateFutureBooking(int minutesFromNow = 60, int duration = 60) =>
    Booking.Create(
        serviceId:      Guid.NewGuid(),
        resourceId:     Guid.NewGuid(),
        customerId:     Guid.NewGuid(),
        customerName:   "João Silva",
        customerEmail:  "joao@gmail.com",
        scheduledAt:    DateTimeOffset.UtcNow.AddMinutes(minutesFromNow),
        durationMinutes: duration);
```

Nos testes `OverlapsWith_*`, substituir o segundo `Guid.NewGuid()` para `resourceId: Guid.NewGuid()`.

- [ ] **Step 10: Deletar arquivos de Professional do domínio**

```
Remove-Item "src\Horafy.Domain\Entities\Professionals\Professional.cs"
Remove-Item "src\Horafy.Domain\Interfaces\Repositories\IProfessionalRepository.cs"
```

- [ ] **Step 11: Rodar testes de domínio — verificar verde**

```
dotnet test tests/Horafy.Domain.Tests -v minimal
```
Esperado: todos os testes passando (incluindo `ResourceTests` e `BookingTests` atualizados).

- [ ] **Step 12: Commit**

```
git add src/Horafy.Domain/ tests/Horafy.Domain.Tests/
git commit -m "feat: migrar Professional para Resource com ResourceType enum"
```

---

## Task 2: Migrar Professional → Resource (Infrastructure Layer)

**Files:**
- Delete: `src/Horafy.Infrastructure/Persistence/TenantConfigurations/ProfessionalEntityConfiguration.cs`
- Delete: `src/Horafy.Infrastructure/Repositories/ProfessionalRepository.cs`
- Create: `src/Horafy.Infrastructure/Persistence/TenantConfigurations/ResourceEntityConfiguration.cs`
- Create: `src/Horafy.Infrastructure/Repositories/ResourceRepository.cs`
- Modify: `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs`
- Modify: `src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingEntityConfiguration.cs`
- Modify: `src/Horafy.Infrastructure/Repositories/BookingRepository.cs`
- Modify: `src/Horafy.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Criar ResourceEntityConfiguration**

Criar `src/Horafy.Infrastructure/Persistence/TenantConfigurations/ResourceEntityConfiguration.cs`:

```csharp
using Horafy.Domain.Entities.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class ResourceEntityConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("resources");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).IsRequired().HasMaxLength(150);
        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.Email).HasMaxLength(256);
        builder.Property(r => r.Phone).HasMaxLength(20);
        builder.Property(r => r.Specialty).HasMaxLength(100);
        builder.Property(r => r.Bio).HasMaxLength(500);
        builder.Property(r => r.AvatarUrl).HasMaxLength(500);

        builder.HasIndex(r => r.Type).HasDatabaseName("ix_resources_type");
        builder.HasIndex(r => r.IsActive).HasDatabaseName("ix_resources_is_active");
    }
}
```

- [ ] **Step 2: Criar ResourceRepository**

Criar `src/Horafy.Infrastructure/Repositories/ResourceRepository.cs`:

```csharp
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class ResourceRepository(TenantDbContext context)
    : BaseRepository<Resource, TenantDbContext>(context), IResourceRepository
{
    public async Task<IReadOnlyList<Resource>> GetActiveAsync(
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Resource>> GetByTypeAsync(
        ResourceType type,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(r => r.Type == type && r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
}
```

- [ ] **Step 3: Atualizar BookingEntityConfiguration**

Editar `src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingEntityConfiguration.cs` — renomear índice de `professional_id` para `resource_id`:

```csharp
using Horafy.Domain.Entities.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class BookingEntityConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.CustomerName).IsRequired().HasMaxLength(150);
        builder.Property(b => b.CustomerEmail).IsRequired().HasMaxLength(256);
        builder.Property(b => b.Notes).HasMaxLength(1000);
        builder.Property(b => b.CancellationReason).HasMaxLength(500);

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasIndex(b => new { b.ResourceId, b.ScheduledAt })
            .HasDatabaseName("ix_bookings_resource_scheduled");

        builder.HasIndex(b => new { b.CustomerId, b.ScheduledAt })
            .HasDatabaseName("ix_bookings_customer_scheduled");

        builder.HasIndex(b => b.Status)
            .HasDatabaseName("ix_bookings_status");
    }
}
```

- [ ] **Step 4: Atualizar BookingRepository**

Substituir o conteúdo de `src/Horafy.Infrastructure/Repositories/BookingRepository.cs`:

```csharp
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class BookingRepository(TenantDbContext context)
    : BaseRepository<Booking, TenantDbContext>(context), IBookingRepository
{
    public async Task<IReadOnlyList<Booking>> GetByResourceAsync(
        Guid resourceId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(b => b.ResourceId == resourceId
                     && b.ScheduledAt >= from
                     && b.ScheduledAt < to)
            .OrderBy(b => b.ScheduledAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(b => b.CustomerId == customerId)
            .OrderByDescending(b => b.ScheduledAt)
            .ToListAsync(cancellationToken);

    public async Task<bool> HasConflictAsync(
        Guid resourceId,
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? excludeBookingId = null,
        CancellationToken cancellationToken = default) =>
        await DbSet.AnyAsync(b =>
            b.ResourceId == resourceId
            && b.Status != BookingStatus.Cancelled
            && b.Status != BookingStatus.NoShow
            && b.ScheduledAt < end
            && b.EndsAt > start
            && (excludeBookingId == null || b.Id != excludeBookingId),
            cancellationToken);
}
```

- [ ] **Step 5: Atualizar TenantDbContext**

Substituir o conteúdo de `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs`:

```csharp
using Horafy.Domain.Entities.Base;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Persistence;

public sealed class TenantDbContext : DbContext
{
    private readonly IPublisher? _publisher;

    public DbSet<Service>  Services  => Set<Service>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Booking>  Bookings  => Set<Booking>();

    public TenantDbContext(
        DbContextOptions<TenantDbContext> options,
        IPublisher? publisher = null)
        : base(options)
    {
        _publisher = publisher;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(TenantDbContext).Assembly,
            t => t.Namespace?.Contains("TenantConfigurations") is true);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var prop = entityType.FindProperty("IsDeleted");
            if (prop is not null && prop.ClrType == typeof(bool))
            {
                var p  = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var pr = System.Linq.Expressions.Expression.Property(p, "IsDeleted");
                var c  = System.Linq.Expressions.Expression.Not(pr);
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(System.Linq.Expressions.Expression.Lambda(c, p));
            }
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var domainEvents = ChangeTracker
            .Entries<BaseEntity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        if (_publisher is not null)
            foreach (var ev in domainEvents)
                await _publisher.Publish(ev, cancellationToken);

        ChangeTracker
            .Entries<BaseEntity>()
            .ToList()
            .ForEach(e => e.Entity.ClearDomainEvents());

        return result;
    }
}
```

- [ ] **Step 6: Deletar arquivos de Professional da infra**

```
Remove-Item "src\Horafy.Infrastructure\Persistence\TenantConfigurations\ProfessionalEntityConfiguration.cs"
Remove-Item "src\Horafy.Infrastructure\Repositories\ProfessionalRepository.cs"
```

- [ ] **Step 7: Atualizar DependencyInjection.cs — substituir Professional por Resource**

Em `src/Horafy.Infrastructure/DependencyInjection.cs`, substituir as duas linhas de registro do Professional:

```csharp
// Remover:
services.AddScoped<IProfessionalRepository, ProfessionalRepository>();

// Adicionar:
services.AddScoped<IResourceRepository, ResourceRepository>();
```

Também atualizar o using:
```csharp
// Remover: using Horafy.Domain.Entities.Professionals; (se existir)
// Não é necessário adicionar nada — os tipos são resolvidos via namespace
```

- [ ] **Step 8: Build para verificar compilação**

```
dotnet build src/Horafy.Infrastructure -v minimal
```
Esperado: Build succeeded, 0 errors.

- [ ] **Step 9: Commit**

```
git add src/Horafy.Infrastructure/
git commit -m "feat: atualizar infraestrutura — Professional → Resource"
```

---

## Task 3: Migrar Professional → Resource (Application + API)

**Files:**
- Delete: `src/Horafy.Application/Features/Professionals/` (diretório inteiro)
- Create: `src/Horafy.Application/Features/Resources/ResourceErrors.cs`
- Create: `src/Horafy.Application/Features/Resources/Commands/CreateResourceCommand.cs`
- Create: `src/Horafy.Application/Features/Resources/Commands/UpdateResourceCommand.cs`
- Create: `src/Horafy.Application/Features/Resources/Commands/DeleteResourceCommand.cs`
- Create: `src/Horafy.Application/Features/Resources/Queries/GetResourcesQuery.cs`
- Create: `src/Horafy.Application/Features/Resources/Queries/GetResourceByIdQuery.cs`
- Modify: `src/Horafy.Application/Features/Bookings/BookingErrors.cs`
- Modify: `src/Horafy.Application/Features/Bookings/Commands/CreateBookingCommand.cs`
- Modify: `src/Horafy.Application/Features/Bookings/Queries/GetBookingsQuery.cs`
- Delete: `src/Horafy.API/Controllers/V1/ProfessionalsController.cs`
- Create: `src/Horafy.API/Controllers/V1/ResourcesController.cs`
- Modify: `src/Horafy.API/Controllers/V1/BookingsController.cs`

- [ ] **Step 1: Criar ResourceErrors**

Criar `src/Horafy.Application/Features/Resources/ResourceErrors.cs`:

```csharp
using Horafy.Shared;

namespace Horafy.Application.Features.Resources;

public static class ResourceErrors
{
    public static readonly Error NotFound = new(
        "Resource.NotFound", "Recurso não encontrado.", ErrorType.NotFound);

    public static readonly Error NameAlreadyExists = new(
        "Resource.NameAlreadyExists", "Já existe um recurso com este nome.", ErrorType.Conflict);
}
```

- [ ] **Step 2: Criar CreateResourceCommand**

Criar `src/Horafy.Application/Features/Resources/Commands/CreateResourceCommand.cs`:

```csharp
using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Resources.Commands;

public sealed record CreateResourceCommand(
    string Name,
    ResourceType Type,
    string? Email,
    string? Phone,
    string? Specialty,
    string? Bio,
    string? AvatarUrl,
    Guid? UserId) : IRequest<Result<Guid>>;

public sealed class CreateResourceCommandValidator : AbstractValidator<CreateResourceCommand>
{
    public CreateResourceCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);
    }
}

internal sealed class CreateResourceCommandHandler(
    IResourceRepository resourceRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CreateResourceCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateResourceCommand request, CancellationToken cancellationToken)
    {
        var resource = Resource.Create(
            request.Name, request.Type, request.Email, request.Phone,
            request.Specialty, request.Bio, request.AvatarUrl, request.UserId);

        resourceRepository.Add(resource);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(resource.Id);
    }
}
```

- [ ] **Step 3: Criar UpdateResourceCommand**

Criar `src/Horafy.Application/Features/Resources/Commands/UpdateResourceCommand.cs`:

```csharp
using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Resources.Commands;

public sealed record UpdateResourceCommand(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? Specialty,
    string? Bio,
    string? AvatarUrl) : IRequest<Result>;

public sealed class UpdateResourceCommandValidator : AbstractValidator<UpdateResourceCommand>
{
    public UpdateResourceCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);
    }
}

internal sealed class UpdateResourceCommandHandler(
    IResourceRepository resourceRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<UpdateResourceCommand, Result>
{
    public async Task<Result> Handle(
        UpdateResourceCommand request, CancellationToken cancellationToken)
    {
        var resource = await resourceRepository.GetByIdAsync(request.Id, cancellationToken);
        if (resource is null) return Result.Failure(ResourceErrors.NotFound);

        resource.Update(request.Name, request.Email, request.Phone,
            request.Specialty, request.Bio, request.AvatarUrl);

        resourceRepository.Update(resource);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

- [ ] **Step 4: Criar DeleteResourceCommand**

Criar `src/Horafy.Application/Features/Resources/Commands/DeleteResourceCommand.cs`:

```csharp
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Resources.Commands;

public sealed record DeleteResourceCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteResourceCommandHandler(
    IResourceRepository resourceRepository,
    ICurrentUserService currentUser,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<DeleteResourceCommand, Result>
{
    public async Task<Result> Handle(
        DeleteResourceCommand request, CancellationToken cancellationToken)
    {
        var resource = await resourceRepository.GetByIdAsync(request.Id, cancellationToken);
        if (resource is null) return Result.Failure(ResourceErrors.NotFound);

        resource.Delete(currentUser.Email ?? "system");
        resourceRepository.Update(resource);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

- [ ] **Step 5: Criar GetResourcesQuery**

Criar `src/Horafy.Application/Features/Resources/Queries/GetResourcesQuery.cs`:

```csharp
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Resources.Queries;

public sealed record GetResourcesQuery(
    bool OnlyActive = true,
    ResourceType? Type = null) : IRequest<Result<IReadOnlyList<ResourceResult>>>;

public sealed record ResourceResult(
    Guid Id,
    string Name,
    ResourceType Type,
    string? Email,
    string? Phone,
    string? Specialty,
    string? Bio,
    string? AvatarUrl,
    bool IsActive);

internal sealed class GetResourcesQueryHandler(
    IResourceRepository resourceRepository)
    : IRequestHandler<GetResourcesQuery, Result<IReadOnlyList<ResourceResult>>>
{
    public async Task<Result<IReadOnlyList<ResourceResult>>> Handle(
        GetResourcesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Resource> resources = request.Type.HasValue
            ? await resourceRepository.GetByTypeAsync(request.Type.Value, cancellationToken)
            : request.OnlyActive
                ? await resourceRepository.GetActiveAsync(cancellationToken)
                : await resourceRepository.GetAllAsync(cancellationToken);

        var result = resources.Select(ToResult).ToList();
        return Result.Success<IReadOnlyList<ResourceResult>>(result);
    }

    private static ResourceResult ToResult(Resource r) => new(
        r.Id, r.Name, r.Type, r.Email, r.Phone, r.Specialty, r.Bio, r.AvatarUrl, r.IsActive);
}
```

- [ ] **Step 6: Criar GetResourceByIdQuery**

Criar `src/Horafy.Application/Features/Resources/Queries/GetResourceByIdQuery.cs`:

```csharp
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Resources.Queries;

public sealed record GetResourceByIdQuery(Guid Id) : IRequest<Result<ResourceResult>>;

internal sealed class GetResourceByIdQueryHandler(
    IResourceRepository resourceRepository)
    : IRequestHandler<GetResourceByIdQuery, Result<ResourceResult>>
{
    public async Task<Result<ResourceResult>> Handle(
        GetResourceByIdQuery request, CancellationToken cancellationToken)
    {
        var resource = await resourceRepository.GetByIdAsync(request.Id, cancellationToken);
        if (resource is null) return Result.Failure<ResourceResult>(ResourceErrors.NotFound);

        return Result.Success(new ResourceResult(
            resource.Id, resource.Name, resource.Type, resource.Email,
            resource.Phone, resource.Specialty, resource.Bio,
            resource.AvatarUrl, resource.IsActive));
    }
}
```

- [ ] **Step 7: Atualizar BookingErrors**

Substituir o conteúdo de `src/Horafy.Application/Features/Bookings/BookingErrors.cs`:

```csharp
using Horafy.Shared;

namespace Horafy.Application.Features.Bookings;

public static class BookingErrors
{
    public static readonly Error NotFound = new(
        "Booking.NotFound", "Agendamento não encontrado.", ErrorType.NotFound);

    public static readonly Error Conflict = new(
        "Booking.Conflict",
        "O recurso já possui um agendamento neste horário.",
        ErrorType.Conflict);

    public static readonly Error ResourceNotFound = new(
        "Booking.ResourceNotFound", "Recurso não encontrado.", ErrorType.NotFound);

    public static readonly Error ServiceNotFound = new(
        "Booking.ServiceNotFound", "Serviço não encontrado.", ErrorType.NotFound);

    public static readonly Error NotOwner = new(
        "Booking.NotOwner",
        "Você não tem permissão para alterar este agendamento.",
        ErrorType.Unauthorized);

    public static readonly Error PastDate = new(
        "Booking.PastDate",
        "Não é possível agendar para uma data no passado.",
        ErrorType.Validation);

    public static readonly Error SlotNotAvailable = new(
        "Booking.SlotNotAvailable",
        "O horário selecionado não está disponível.",
        ErrorType.Conflict);
}
```

- [ ] **Step 8: Atualizar CreateBookingCommand**

Substituir o conteúdo de `src/Horafy.Application/Features/Bookings/Commands/CreateBookingCommand.cs`:

```csharp
using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Commands;

public sealed record CreateBookingCommand(
    Guid ServiceId,
    Guid ResourceId,
    DateTimeOffset ScheduledAt,
    string? Notes) : IRequest<Result<Guid>>;

public sealed class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
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

        var service = await serviceRepository.GetByIdAsync(request.ServiceId, cancellationToken);
        if (service is null) return Result.Failure<Guid>(BookingErrors.ServiceNotFound);

        var resource = await resourceRepository.GetByIdAsync(request.ResourceId, cancellationToken);
        if (resource is null) return Result.Failure<Guid>(BookingErrors.ResourceNotFound);

        var endsAt = request.ScheduledAt.AddMinutes(service.DurationMinutes);

        var hasConflict = await bookingRepository.HasConflictAsync(
            request.ResourceId, request.ScheduledAt, endsAt,
            cancellationToken: cancellationToken);

        if (hasConflict) return Result.Failure<Guid>(BookingErrors.Conflict);

        var booking = Booking.Create(
            request.ServiceId,
            request.ResourceId,
            customerId:     currentUser.UserId.Value,
            customerName:   currentUser.Email ?? "Cliente",
            customerEmail:  currentUser.Email ?? string.Empty,
            scheduledAt:    request.ScheduledAt,
            durationMinutes: service.DurationMinutes,
            notes: request.Notes);

        bookingRepository.Add(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(booking.Id);
    }
}
```

- [ ] **Step 9: Atualizar GetBookingsQuery**

Substituir o conteúdo de `src/Horafy.Application/Features/Bookings/Queries/GetBookingsQuery.cs`:

```csharp
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Queries;

public sealed record GetBookingsQuery(
    Guid? ResourceId,
    DateTimeOffset? From,
    DateTimeOffset? To) : IRequest<Result<IReadOnlyList<BookingResult>>>;

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
    string? CancellationReason);

internal sealed class GetBookingsQueryHandler(
    IBookingRepository bookingRepository)
    : IRequestHandler<GetBookingsQuery, Result<IReadOnlyList<BookingResult>>>
{
    public async Task<Result<IReadOnlyList<BookingResult>>> Handle(
        GetBookingsQuery request, CancellationToken cancellationToken)
    {
        var from = request.From ?? DateTimeOffset.UtcNow.Date;
        var to   = request.To   ?? from.AddDays(7);

        IReadOnlyList<Booking> bookings = request.ResourceId.HasValue
            ? await bookingRepository.GetByResourceAsync(
                request.ResourceId.Value, from, to, cancellationToken)
            : await bookingRepository.FindAsync(
                b => b.ScheduledAt >= from && b.ScheduledAt < to, cancellationToken);

        var result = bookings.Select(ToResult).ToList();
        return Result.Success<IReadOnlyList<BookingResult>>(result);
    }

    private static BookingResult ToResult(Booking b) => new(
        b.Id, b.ServiceId, b.ResourceId, b.CustomerId,
        b.CustomerName, b.CustomerEmail, b.ScheduledAt, b.EndsAt,
        b.DurationMinutes, b.Notes, b.Status, b.CancellationReason);
}
```

- [ ] **Step 10: Deletar Features/Professionals**

```
Remove-Item -Recurse "src\Horafy.Application\Features\Professionals"
```

- [ ] **Step 11: Criar ResourcesController**

Criar `src/Horafy.API/Controllers/V1/ResourcesController.cs`:

```csharp
using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Resources.Commands;
using Horafy.Application.Features.Resources.Queries;
using Horafy.Domain.Entities.Resources;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize]
public sealed class ResourcesController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<ResourceResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool onlyActive = true,
        [FromQuery] ResourceType? type = null,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new GetResourcesQuery(onlyActive, type), cancellationToken));

    [HttpGet("{id:guid}", Name = "GetResourceById")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ResourceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetResourceByIdQuery(id), cancellationToken));

    [HttpPost]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateResourceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new CreateResourceCommand(request.Name, request.Type, request.Email, request.Phone,
                request.Specialty, request.Bio, request.AvatarUrl, request.UserId),
            cancellationToken);

        if (result.IsFailure) return ToActionResult(result);
        return CreatedAtRoute("GetResourceById", new { id = result.Value }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateResourceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new UpdateResourceCommand(id, request.Name, request.Email, request.Phone,
                request.Specialty, request.Bio, request.AvatarUrl),
            cancellationToken);

        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteResourceCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record CreateResourceRequest(
    string Name, ResourceType Type, string? Email, string? Phone,
    string? Specialty, string? Bio, string? AvatarUrl, Guid? UserId);

public sealed record UpdateResourceRequest(
    string Name, string? Email, string? Phone,
    string? Specialty, string? Bio, string? AvatarUrl);
```

- [ ] **Step 12: Atualizar BookingsController**

Em `src/Horafy.API/Controllers/V1/BookingsController.cs`, substituir todas as ocorrências de `ProfessionalId` por `ResourceId` no record do request de criação e no `GetAll`:

O método `GetAll` deve usar `resourceId` em vez de `professionalId`:
```csharp
public async Task<IActionResult> GetAll(
    [FromQuery] Guid? resourceId = null,
    [FromQuery] DateTimeOffset? from = null,
    [FromQuery] DateTimeOffset? to = null,
    CancellationToken cancellationToken = default) =>
    ToActionResult(await Sender.Send(new GetBookingsQuery(resourceId, from, to), cancellationToken));
```

O record do request de criação:
```csharp
public sealed record CreateBookingRequest(
    Guid ServiceId, Guid ResourceId, DateTimeOffset ScheduledAt, string? Notes);
```

- [ ] **Step 13: Deletar ProfessionalsController**

```
Remove-Item "src\Horafy.API\Controllers\V1\ProfessionalsController.cs"
```

- [ ] **Step 14: Build e testes completos**

```
dotnet build -v minimal
dotnet test -v minimal
```
Esperado: Build succeeded, todos os testes passando.

- [ ] **Step 15: Commit**

```
git add src/ tests/
git commit -m "feat: migrar Application e API — Professional → Resource"
```

---

## Task 4: Entidades de Disponibilidade (Domain)

**Files:**
- Create: `src/Horafy.Domain/Entities/Resources/ResourceService.cs`
- Create: `src/Horafy.Domain/Entities/Availability/BusinessHours.cs`
- Create: `src/Horafy.Domain/Entities/Availability/AvailabilityRule.cs`
- Create: `src/Horafy.Domain/Entities/Availability/AvailabilityException.cs`
- Create: `src/Horafy.Domain/Interfaces/Repositories/IAvailabilityRepository.cs`

- [ ] **Step 1: Criar ResourceService**

Criar `src/Horafy.Domain/Entities/Resources/ResourceService.cs`:

```csharp
using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Resources;

public sealed class ResourceService : BaseEntity
{
    private ResourceService() { }

    public Guid ResourceId { get; private set; }
    public Guid ServiceId  { get; private set; }

    public static ResourceService Create(Guid resourceId, Guid serviceId) =>
        new() { ResourceId = resourceId, ServiceId = serviceId };
}
```

- [ ] **Step 2: Criar BusinessHours**

Criar `src/Horafy.Domain/Entities/Availability/BusinessHours.cs`:

```csharp
using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Availability;

public sealed class BusinessHours : BaseEntity
{
    private BusinessHours() { }

    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly  OpenTime  { get; private set; }
    public TimeOnly  CloseTime { get; private set; }
    public bool      IsOpen    { get; private set; }

    public static BusinessHours Create(DayOfWeek day, TimeOnly open, TimeOnly close, bool isOpen = true)
    {
        if (isOpen && open >= close)
            throw new ArgumentException("Horário de abertura deve ser anterior ao de fechamento.");

        return new BusinessHours
        {
            DayOfWeek = day,
            OpenTime  = open,
            CloseTime = close,
            IsOpen    = isOpen
        };
    }

    public void Update(TimeOnly open, TimeOnly close, bool isOpen)
    {
        if (isOpen && open >= close)
            throw new ArgumentException("Horário de abertura deve ser anterior ao de fechamento.");

        OpenTime  = open;
        CloseTime = close;
        IsOpen    = isOpen;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 3: Criar AvailabilityRule**

Criar `src/Horafy.Domain/Entities/Availability/AvailabilityRule.cs`:

```csharp
using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Availability;

public sealed class AvailabilityRule : BaseEntity
{
    private AvailabilityRule() { }

    public Guid      ResourceId          { get; private set; }
    public DayOfWeek DayOfWeek           { get; private set; }
    public TimeOnly  StartTime           { get; private set; }
    public TimeOnly  EndTime             { get; private set; }
    public int       SlotDurationMinutes { get; private set; }
    public int       BreakAfterMinutes   { get; private set; }

    public static AvailabilityRule Create(
        Guid resourceId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        int slotDurationMinutes,
        int breakAfterMinutes = 0)
    {
        if (startTime >= endTime)
            throw new ArgumentException("Horário de início deve ser anterior ao fim.");
        if (slotDurationMinutes <= 0)
            throw new ArgumentException("Duração do slot deve ser maior que zero.");
        if (breakAfterMinutes < 0)
            throw new ArgumentException("Intervalo não pode ser negativo.");

        return new AvailabilityRule
        {
            ResourceId          = resourceId,
            DayOfWeek           = dayOfWeek,
            StartTime           = startTime,
            EndTime             = endTime,
            SlotDurationMinutes = slotDurationMinutes,
            BreakAfterMinutes   = breakAfterMinutes
        };
    }

    public void Update(TimeOnly startTime, TimeOnly endTime,
        int slotDurationMinutes, int breakAfterMinutes)
    {
        if (startTime >= endTime)
            throw new ArgumentException("Horário de início deve ser anterior ao fim.");

        StartTime           = startTime;
        EndTime             = endTime;
        SlotDurationMinutes = slotDurationMinutes;
        BreakAfterMinutes   = breakAfterMinutes;
        UpdatedAt           = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 4: Criar AvailabilityException**

Criar `src/Horafy.Domain/Entities/Availability/AvailabilityException.cs`:

```csharp
using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Availability;

public sealed class AvailabilityException : BaseEntity
{
    private AvailabilityException() { }

    public Guid      ResourceId   { get; private set; }
    public DateOnly  Date         { get; private set; }
    public bool      IsBlocked    { get; private set; }
    public TimeOnly? CustomStart  { get; private set; }
    public TimeOnly? CustomEnd    { get; private set; }
    public string?   Reason       { get; private set; }

    public static AvailabilityException CreateBlock(Guid resourceId, DateOnly date, string? reason = null) =>
        new()
        {
            ResourceId = resourceId,
            Date       = date,
            IsBlocked  = true,
            Reason     = reason?.Trim()
        };

    public static AvailabilityException CreateCustomHours(
        Guid resourceId, DateOnly date, TimeOnly customStart, TimeOnly customEnd, string? reason = null)
    {
        if (customStart >= customEnd)
            throw new ArgumentException("Horário de início deve ser anterior ao fim.");

        return new AvailabilityException
        {
            ResourceId  = resourceId,
            Date        = date,
            IsBlocked   = false,
            CustomStart = customStart,
            CustomEnd   = customEnd,
            Reason      = reason?.Trim()
        };
    }
}
```

- [ ] **Step 5: Criar IAvailabilityRepository**

Criar `src/Horafy.Domain/Interfaces/Repositories/IAvailabilityRepository.cs`:

```csharp
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Entities.Resources;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IAvailabilityRepository
{
    // BusinessHours
    Task<IReadOnlyList<BusinessHours>> GetBusinessHoursAsync(CancellationToken ct = default);
    Task<BusinessHours?> GetBusinessHoursByDayAsync(DayOfWeek day, CancellationToken ct = default);

    // AvailabilityRule
    Task<IReadOnlyList<AvailabilityRule>> GetRulesByResourceAsync(Guid resourceId, CancellationToken ct = default);
    Task<AvailabilityRule?> GetRuleAsync(Guid resourceId, DayOfWeek day, CancellationToken ct = default);

    // AvailabilityException
    Task<AvailabilityException?> GetExceptionAsync(Guid resourceId, DateOnly date, CancellationToken ct = default);

    // ResourceService
    Task<IReadOnlyList<ResourceService>> GetResourceServicesAsync(Guid resourceId, CancellationToken ct = default);
    Task<bool> ResourceServiceExistsAsync(Guid resourceId, Guid serviceId, CancellationToken ct = default);

    void Add<T>(T entity) where T : Horafy.Domain.Entities.Base.BaseEntity;
    void Update<T>(T entity) where T : Horafy.Domain.Entities.Base.BaseEntity;
    void Remove<T>(T entity) where T : Horafy.Domain.Entities.Base.BaseEntity;
}
```

- [ ] **Step 6: Build domain**

```
dotnet build src/Horafy.Domain -v minimal
```
Esperado: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```
git add src/Horafy.Domain/
git commit -m "feat: adicionar entidades BusinessHours, AvailabilityRule, AvailabilityException, ResourceService"
```

---

## Task 5: Disponibilidade — Infrastructure

**Files:**
- Create: `src/Horafy.Infrastructure/Persistence/TenantConfigurations/ResourceServiceEntityConfiguration.cs`
- Create: `src/Horafy.Infrastructure/Persistence/TenantConfigurations/BusinessHoursEntityConfiguration.cs`
- Create: `src/Horafy.Infrastructure/Persistence/TenantConfigurations/AvailabilityRuleEntityConfiguration.cs`
- Create: `src/Horafy.Infrastructure/Persistence/TenantConfigurations/AvailabilityExceptionEntityConfiguration.cs`
- Create: `src/Horafy.Infrastructure/Repositories/AvailabilityRepository.cs`
- Modify: `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs`
- Modify: `src/Horafy.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Criar ResourceServiceEntityConfiguration**

Criar `src/Horafy.Infrastructure/Persistence/TenantConfigurations/ResourceServiceEntityConfiguration.cs`:

```csharp
using Horafy.Domain.Entities.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class ResourceServiceEntityConfiguration : IEntityTypeConfiguration<ResourceService>
{
    public void Configure(EntityTypeBuilder<ResourceService> builder)
    {
        builder.ToTable("resource_services");
        builder.HasKey(rs => rs.Id);
        builder.HasIndex(rs => new { rs.ResourceId, rs.ServiceId })
            .IsUnique()
            .HasDatabaseName("ix_resource_services_resource_service");
    }
}
```

- [ ] **Step 2: Criar BusinessHoursEntityConfiguration**

Criar `src/Horafy.Infrastructure/Persistence/TenantConfigurations/BusinessHoursEntityConfiguration.cs`:

```csharp
using Horafy.Domain.Entities.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class BusinessHoursEntityConfiguration : IEntityTypeConfiguration<BusinessHours>
{
    public void Configure(EntityTypeBuilder<BusinessHours> builder)
    {
        builder.ToTable("business_hours");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.DayOfWeek).HasConversion<int>();
        builder.HasIndex(b => b.DayOfWeek)
            .IsUnique()
            .HasDatabaseName("ix_business_hours_day");
    }
}
```

- [ ] **Step 3: Criar AvailabilityRuleEntityConfiguration**

Criar `src/Horafy.Infrastructure/Persistence/TenantConfigurations/AvailabilityRuleEntityConfiguration.cs`:

```csharp
using Horafy.Domain.Entities.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class AvailabilityRuleEntityConfiguration : IEntityTypeConfiguration<AvailabilityRule>
{
    public void Configure(EntityTypeBuilder<AvailabilityRule> builder)
    {
        builder.ToTable("availability_rules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.DayOfWeek).HasConversion<int>();
        builder.HasIndex(r => new { r.ResourceId, r.DayOfWeek })
            .IsUnique()
            .HasDatabaseName("ix_availability_rules_resource_day");
    }
}
```

- [ ] **Step 4: Criar AvailabilityExceptionEntityConfiguration**

Criar `src/Horafy.Infrastructure/Persistence/TenantConfigurations/AvailabilityExceptionEntityConfiguration.cs`:

```csharp
using Horafy.Domain.Entities.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class AvailabilityExceptionEntityConfiguration
    : IEntityTypeConfiguration<AvailabilityException>
{
    public void Configure(EntityTypeBuilder<AvailabilityException> builder)
    {
        builder.ToTable("availability_exceptions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.HasIndex(e => new { e.ResourceId, e.Date })
            .IsUnique()
            .HasDatabaseName("ix_availability_exceptions_resource_date");
    }
}
```

- [ ] **Step 5: Criar AvailabilityRepository**

Criar `src/Horafy.Infrastructure/Repositories/AvailabilityRepository.cs`:

```csharp
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Entities.Base;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class AvailabilityRepository(TenantDbContext context) : IAvailabilityRepository
{
    public async Task<IReadOnlyList<BusinessHours>> GetBusinessHoursAsync(
        CancellationToken ct = default) =>
        await context.Set<BusinessHours>().AsNoTracking().ToListAsync(ct);

    public async Task<BusinessHours?> GetBusinessHoursByDayAsync(
        DayOfWeek day, CancellationToken ct = default) =>
        await context.Set<BusinessHours>()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.DayOfWeek == day, ct);

    public async Task<IReadOnlyList<AvailabilityRule>> GetRulesByResourceAsync(
        Guid resourceId, CancellationToken ct = default) =>
        await context.Set<AvailabilityRule>()
            .AsNoTracking()
            .Where(r => r.ResourceId == resourceId)
            .ToListAsync(ct);

    public async Task<AvailabilityRule?> GetRuleAsync(
        Guid resourceId, DayOfWeek day, CancellationToken ct = default) =>
        await context.Set<AvailabilityRule>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ResourceId == resourceId && r.DayOfWeek == day, ct);

    public async Task<AvailabilityException?> GetExceptionAsync(
        Guid resourceId, DateOnly date, CancellationToken ct = default) =>
        await context.Set<AvailabilityException>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ResourceId == resourceId && e.Date == date, ct);

    public async Task<IReadOnlyList<ResourceService>> GetResourceServicesAsync(
        Guid resourceId, CancellationToken ct = default) =>
        await context.Set<ResourceService>()
            .AsNoTracking()
            .Where(rs => rs.ResourceId == resourceId)
            .ToListAsync(ct);

    public async Task<bool> ResourceServiceExistsAsync(
        Guid resourceId, Guid serviceId, CancellationToken ct = default) =>
        await context.Set<ResourceService>()
            .AnyAsync(rs => rs.ResourceId == resourceId && rs.ServiceId == serviceId, ct);

    public void Add<T>(T entity) where T : BaseEntity    => context.Set<T>().Add(entity);
    public void Update<T>(T entity) where T : BaseEntity => context.Set<T>().Update(entity);
    public void Remove<T>(T entity) where T : BaseEntity => context.Set<T>().Remove(entity);
}
```

- [ ] **Step 6: Atualizar TenantDbContext — adicionar novos DbSets**

Em `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs`, adicionar os novos DbSets após `Bookings`:

```csharp
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Entities.Resources;
// ... imports existentes

public DbSet<Service>              Services              => Set<Service>();
public DbSet<Resource>             Resources             => Set<Resource>();
public DbSet<ResourceService>      ResourceServices      => Set<ResourceService>();
public DbSet<Booking>              Bookings              => Set<Booking>();
public DbSet<BusinessHours>        BusinessHours         => Set<BusinessHours>();
public DbSet<AvailabilityRule>     AvailabilityRules     => Set<AvailabilityRule>();
public DbSet<AvailabilityException> AvailabilityExceptions => Set<AvailabilityException>();
```

- [ ] **Step 7: Atualizar DependencyInjection.cs — registrar IAvailabilityRepository**

Em `src/Horafy.Infrastructure/DependencyInjection.cs`, adicionar após `IBookingRepository`:

```csharp
services.AddScoped<IAvailabilityRepository, AvailabilityRepository>();
```

- [ ] **Step 8: Build**

```
dotnet build src/Horafy.Infrastructure -v minimal
```
Esperado: Build succeeded, 0 errors.

- [ ] **Step 9: Commit**

```
git add src/Horafy.Infrastructure/
git commit -m "feat: adicionar infraestrutura de disponibilidade (EF configs + AvailabilityRepository)"
```

---

## Task 6: Disponibilidade — Application (Commands)

**Files:**
- Create: `src/Horafy.Application/Features/Availability/AvailabilityErrors.cs`
- Create: `src/Horafy.Application/Features/Availability/Commands/SetBusinessHoursCommand.cs`
- Create: `src/Horafy.Application/Features/Availability/Commands/SetAvailabilityRuleCommand.cs`
- Create: `src/Horafy.Application/Features/Availability/Commands/SetAvailabilityExceptionCommand.cs`
- Create: `src/Horafy.Application/Features/Resources/Commands/AddResourceServiceCommand.cs`
- Create: `src/Horafy.Application/Features/Resources/Commands/RemoveResourceServiceCommand.cs`
- Modify: `src/Horafy.Application/Interfaces/ITenantUnitOfWork.cs`
- Modify: `src/Horafy.Infrastructure/Persistence/TenantUnitOfWork.cs`

- [ ] **Step 1: Adicionar BeginTransactionAsync ao ITenantUnitOfWork**

Substituir o conteúdo de `src/Horafy.Application/Interfaces/ITenantUnitOfWork.cs`:

```csharp
using System.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace Horafy.Application.Interfaces;

public interface ITenantUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Implementar BeginTransactionAsync no TenantUnitOfWork**

Substituir o conteúdo de `src/Horafy.Infrastructure/Persistence/TenantUnitOfWork.cs`:

```csharp
using System.Data;
using Horafy.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace Horafy.Infrastructure.Persistence;

internal sealed class TenantUnitOfWork(TenantDbContext context) : ITenantUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default) =>
        context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
}
```

- [ ] **Step 3: Criar AvailabilityErrors**

Criar `src/Horafy.Application/Features/Availability/AvailabilityErrors.cs`:

```csharp
using Horafy.Shared;

namespace Horafy.Application.Features.Availability;

public static class AvailabilityErrors
{
    public static readonly Error ResourceNotFound = new(
        "Availability.ResourceNotFound", "Recurso não encontrado.", ErrorType.NotFound);

    public static readonly Error ServiceNotFound = new(
        "Availability.ServiceNotFound", "Serviço não encontrado.", ErrorType.NotFound);

    public static readonly Error ServiceAlreadyLinked = new(
        "Availability.ServiceAlreadyLinked",
        "O serviço já está vinculado a este recurso.",
        ErrorType.Conflict);

    public static readonly Error ServiceNotLinked = new(
        "Availability.ServiceNotLinked",
        "O serviço não está vinculado a este recurso.",
        ErrorType.NotFound);
}
```

- [ ] **Step 4: Criar SetBusinessHoursCommand**

Criar `src/Horafy.Application/Features/Availability/Commands/SetBusinessHoursCommand.cs`:

```csharp
using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Commands;

public sealed record SetBusinessHoursCommand(
    DayOfWeek DayOfWeek,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    bool IsOpen) : IRequest<Result>;

public sealed class SetBusinessHoursCommandValidator : AbstractValidator<SetBusinessHoursCommand>
{
    public SetBusinessHoursCommandValidator()
    {
        RuleFor(x => x.OpenTime)
            .LessThan(x => x.CloseTime)
            .When(x => x.IsOpen)
            .WithMessage("Horário de abertura deve ser anterior ao de fechamento.");
    }
}

internal sealed class SetBusinessHoursCommandHandler(
    IAvailabilityRepository availabilityRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<SetBusinessHoursCommand, Result>
{
    public async Task<Result> Handle(
        SetBusinessHoursCommand request, CancellationToken cancellationToken)
    {
        var existing = await availabilityRepository
            .GetBusinessHoursByDayAsync(request.DayOfWeek, cancellationToken);

        if (existing is null)
        {
            var bh = BusinessHours.Create(
                request.DayOfWeek, request.OpenTime, request.CloseTime, request.IsOpen);
            availabilityRepository.Add(bh);
        }
        else
        {
            existing.Update(request.OpenTime, request.CloseTime, request.IsOpen);
            availabilityRepository.Update(existing);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 5: Criar SetAvailabilityRuleCommand**

Criar `src/Horafy.Application/Features/Availability/Commands/SetAvailabilityRuleCommand.cs`:

```csharp
using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Commands;

public sealed record SetAvailabilityRuleCommand(
    Guid ResourceId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int SlotDurationMinutes,
    int BreakAfterMinutes = 0) : IRequest<Result>;

public sealed class SetAvailabilityRuleCommandValidator : AbstractValidator<SetAvailabilityRuleCommand>
{
    public SetAvailabilityRuleCommandValidator()
    {
        RuleFor(x => x.ResourceId).NotEmpty();
        RuleFor(x => x.StartTime).LessThan(x => x.EndTime)
            .WithMessage("Início deve ser anterior ao fim.");
        RuleFor(x => x.SlotDurationMinutes).GreaterThan(0);
        RuleFor(x => x.BreakAfterMinutes).GreaterThanOrEqualTo(0);
    }
}

internal sealed class SetAvailabilityRuleCommandHandler(
    IResourceRepository resourceRepository,
    IAvailabilityRepository availabilityRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<SetAvailabilityRuleCommand, Result>
{
    public async Task<Result> Handle(
        SetAvailabilityRuleCommand request, CancellationToken cancellationToken)
    {
        var resourceExists = await resourceRepository.ExistsAsync(
            r => r.Id == request.ResourceId, cancellationToken);
        if (!resourceExists)
            return Result.Failure(AvailabilityErrors.ResourceNotFound);

        var existing = await availabilityRepository
            .GetRuleAsync(request.ResourceId, request.DayOfWeek, cancellationToken);

        if (existing is null)
        {
            var rule = AvailabilityRule.Create(
                request.ResourceId, request.DayOfWeek,
                request.StartTime, request.EndTime,
                request.SlotDurationMinutes, request.BreakAfterMinutes);
            availabilityRepository.Add(rule);
        }
        else
        {
            existing.Update(request.StartTime, request.EndTime,
                request.SlotDurationMinutes, request.BreakAfterMinutes);
            availabilityRepository.Update(existing);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 6: Criar SetAvailabilityExceptionCommand**

Criar `src/Horafy.Application/Features/Availability/Commands/SetAvailabilityExceptionCommand.cs`:

```csharp
using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Commands;

public sealed record SetAvailabilityExceptionCommand(
    Guid ResourceId,
    DateOnly Date,
    bool IsBlocked,
    TimeOnly? CustomStart,
    TimeOnly? CustomEnd,
    string? Reason) : IRequest<Result>;

public sealed class SetAvailabilityExceptionCommandValidator
    : AbstractValidator<SetAvailabilityExceptionCommand>
{
    public SetAvailabilityExceptionCommandValidator()
    {
        RuleFor(x => x.ResourceId).NotEmpty();
        RuleFor(x => x.CustomStart)
            .LessThan(x => x.CustomEnd)
            .When(x => !x.IsBlocked && x.CustomStart.HasValue && x.CustomEnd.HasValue)
            .WithMessage("Início deve ser anterior ao fim.");
        RuleFor(x => x.CustomStart)
            .NotNull()
            .When(x => !x.IsBlocked)
            .WithMessage("CustomStart é obrigatório quando não bloqueado.");
        RuleFor(x => x.CustomEnd)
            .NotNull()
            .When(x => !x.IsBlocked)
            .WithMessage("CustomEnd é obrigatório quando não bloqueado.");
    }
}

internal sealed class SetAvailabilityExceptionCommandHandler(
    IResourceRepository resourceRepository,
    IAvailabilityRepository availabilityRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<SetAvailabilityExceptionCommand, Result>
{
    public async Task<Result> Handle(
        SetAvailabilityExceptionCommand request, CancellationToken cancellationToken)
    {
        var resourceExists = await resourceRepository.ExistsAsync(
            r => r.Id == request.ResourceId, cancellationToken);
        if (!resourceExists)
            return Result.Failure(AvailabilityErrors.ResourceNotFound);

        var existing = await availabilityRepository
            .GetExceptionAsync(request.ResourceId, request.Date, cancellationToken);

        if (existing is not null)
            availabilityRepository.Remove(existing);

        var exception = request.IsBlocked
            ? AvailabilityException.CreateBlock(request.ResourceId, request.Date, request.Reason)
            : AvailabilityException.CreateCustomHours(
                request.ResourceId, request.Date,
                request.CustomStart!.Value, request.CustomEnd!.Value, request.Reason);

        availabilityRepository.Add(exception);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 7: Criar AddResourceServiceCommand**

Criar `src/Horafy.Application/Features/Resources/Commands/AddResourceServiceCommand.cs`:

```csharp
using Horafy.Application.Features.Availability;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Resources.Commands;

public sealed record AddResourceServiceCommand(Guid ResourceId, Guid ServiceId) : IRequest<Result>;

internal sealed class AddResourceServiceCommandHandler(
    IResourceRepository resourceRepository,
    IServiceRepository serviceRepository,
    IAvailabilityRepository availabilityRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<AddResourceServiceCommand, Result>
{
    public async Task<Result> Handle(
        AddResourceServiceCommand request, CancellationToken cancellationToken)
    {
        var resourceExists = await resourceRepository.ExistsAsync(
            r => r.Id == request.ResourceId, cancellationToken);
        if (!resourceExists)
            return Result.Failure(AvailabilityErrors.ResourceNotFound);

        var serviceExists = await serviceRepository.ExistsAsync(
            s => s.Id == request.ServiceId, cancellationToken);
        if (!serviceExists)
            return Result.Failure(AvailabilityErrors.ServiceNotFound);

        var alreadyLinked = await availabilityRepository.ResourceServiceExistsAsync(
            request.ResourceId, request.ServiceId, cancellationToken);
        if (alreadyLinked)
            return Result.Failure(AvailabilityErrors.ServiceAlreadyLinked);

        availabilityRepository.Add(ResourceService.Create(request.ResourceId, request.ServiceId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 8: Criar RemoveResourceServiceCommand**

Criar `src/Horafy.Application/Features/Resources/Commands/RemoveResourceServiceCommand.cs`:

```csharp
using Horafy.Application.Features.Availability;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Resources.Commands;

public sealed record RemoveResourceServiceCommand(Guid ResourceId, Guid ServiceId) : IRequest<Result>;

internal sealed class RemoveResourceServiceCommandHandler(
    IAvailabilityRepository availabilityRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<RemoveResourceServiceCommand, Result>
{
    public async Task<Result> Handle(
        RemoveResourceServiceCommand request, CancellationToken cancellationToken)
    {
        var links = await availabilityRepository
            .GetResourceServicesAsync(request.ResourceId, cancellationToken);

        var link = links.FirstOrDefault(rs => rs.ServiceId == request.ServiceId);
        if (link is null)
            return Result.Failure(AvailabilityErrors.ServiceNotLinked);

        availabilityRepository.Remove(link);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 9: Build**

```
dotnet build src/Horafy.Application -v minimal
```
Esperado: Build succeeded, 0 errors.

- [ ] **Step 10: Commit**

```
git add src/Horafy.Application/ src/Horafy.Infrastructure/
git commit -m "feat: commands de disponibilidade (BusinessHours, AvailabilityRule, Exception, ResourceService)"
```

---

## Task 7: GetAvailableSlotsQuery — Algoritmo de Disponibilidade

**Files:**
- Create: `src/Horafy.Application/Features/Availability/Queries/GetAvailableSlotsQuery.cs`
- Create: `tests/Horafy.Application.Tests/Availability/GetAvailableSlotsQueryHandlerTests.cs`

- [ ] **Step 1: Escrever testes do algoritmo (failing)**

Criar `tests/Horafy.Application.Tests/Availability/GetAvailableSlotsQueryHandlerTests.cs`:

```csharp
using FluentAssertions;
using Horafy.Application.Features.Availability.Queries;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Availability;

public class GetAvailableSlotsQueryHandlerTests
{
    private static readonly Guid ResourceId = Guid.NewGuid();
    private static readonly DateOnly TestDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

    private static AvailabilityRule BuildRule(
        TimeOnly start, TimeOnly end, int slotMinutes, int breakMinutes = 0) =>
        AvailabilityRule.Create(ResourceId, TestDate.DayOfWeek, start, end, slotMinutes, breakMinutes);

    [Fact]
    public async Task Handle_NoRule_ReturnsEmptyList()
    {
        var (handler, availRepo, _, _) = BuildHandler();

        availRepo.Setup(r => r.GetRuleAsync(ResourceId, TestDate.DayOfWeek, default))
            .ReturnsAsync((AvailabilityRule?)null);

        var result = await handler.Handle(
            new GetAvailableSlotsQuery(ResourceId, TestDate, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_BlockedDay_ReturnsEmptyList()
    {
        var rule = BuildRule(new TimeOnly(8, 0), new TimeOnly(12, 0), 60);
        var exception = AvailabilityException.CreateBlock(ResourceId, TestDate);
        var (handler, availRepo, _, bookingRepo) = BuildHandler();

        availRepo.Setup(r => r.GetRuleAsync(ResourceId, TestDate.DayOfWeek, default))
            .ReturnsAsync(rule);
        availRepo.Setup(r => r.GetExceptionAsync(ResourceId, TestDate, default))
            .ReturnsAsync(exception);
        bookingRepo.Setup(r => r.GetByResourceAsync(ResourceId, It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<Booking>());

        var result = await handler.Handle(
            new GetAvailableSlotsQuery(ResourceId, TestDate, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NoExceptionNoBookings_ReturnsAllSlots()
    {
        // 08:00–10:00, slot 60min, sem break → espera 2 slots: 08:00 e 09:00
        var rule = BuildRule(new TimeOnly(8, 0), new TimeOnly(10, 0), 60);
        var (handler, availRepo, _, bookingRepo) = BuildHandler();

        availRepo.Setup(r => r.GetRuleAsync(ResourceId, TestDate.DayOfWeek, default))
            .ReturnsAsync(rule);
        availRepo.Setup(r => r.GetExceptionAsync(ResourceId, TestDate, default))
            .ReturnsAsync((AvailabilityException?)null);
        bookingRepo.Setup(r => r.GetByResourceAsync(ResourceId, It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<Booking>());

        var result = await handler.Handle(
            new GetAvailableSlotsQuery(ResourceId, TestDate, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Should().HaveTimeSpan(new TimeSpan(8, 0, 0));
        result.Value[1].Should().HaveTimeSpan(new TimeSpan(9, 0, 0));
    }

    [Fact]
    public async Task Handle_WithBreak_ReturnsCorrectSlots()
    {
        // 08:00–10:30, slot 60min, break 15min → slots: 08:00, 09:15 (08:00+60+15=09:15; 09:15+60=10:15 ≤ 10:30)
        var rule = BuildRule(new TimeOnly(8, 0), new TimeOnly(10, 30), 60, breakAfterMinutes: 15);
        var (handler, availRepo, _, bookingRepo) = BuildHandler();

        availRepo.Setup(r => r.GetRuleAsync(ResourceId, TestDate.DayOfWeek, default))
            .ReturnsAsync(rule);
        availRepo.Setup(r => r.GetExceptionAsync(ResourceId, TestDate, default))
            .ReturnsAsync((AvailabilityException?)null);
        bookingRepo.Setup(r => r.GetByResourceAsync(ResourceId, It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<Booking>());

        var result = await handler.Handle(
            new GetAvailableSlotsQuery(ResourceId, TestDate, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Should().HaveTimeSpan(new TimeSpan(8, 0, 0));
        result.Value[1].Should().HaveTimeSpan(new TimeSpan(9, 15, 0));
    }

    [Fact]
    public async Task Handle_SlotOccupiedByBooking_ExcludesSlot()
    {
        // 08:00–10:00, slot 60min, booking às 08:00 → apenas 09:00 disponível
        var rule = BuildRule(new TimeOnly(8, 0), new TimeOnly(10, 0), 60);
        var (handler, availRepo, _, bookingRepo) = BuildHandler();

        // Cria booking manualmente para simular ocupação das 08:00
        // Usamos DateTimeOffset combinando TestDate com 08:00 UTC
        var slotStart = TestDate.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc);
        var existingBooking = Booking.Create(
            Guid.NewGuid(), ResourceId, Guid.NewGuid(),
            "Cliente", "cliente@test.com",
            scheduledAt: slotStart,
            durationMinutes: 60);

        availRepo.Setup(r => r.GetRuleAsync(ResourceId, TestDate.DayOfWeek, default))
            .ReturnsAsync(rule);
        availRepo.Setup(r => r.GetExceptionAsync(ResourceId, TestDate, default))
            .ReturnsAsync((AvailabilityException?)null);
        bookingRepo.Setup(r => r.GetByResourceAsync(ResourceId, It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<Booking> { existingBooking });

        var result = await handler.Handle(
            new GetAvailableSlotsQuery(ResourceId, TestDate, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Should().HaveTimeSpan(new TimeSpan(9, 0, 0));
    }

    [Fact]
    public async Task Handle_CustomHoursException_UsesExceptionTimes()
    {
        // Regra: 08:00–12:00, mas exceção define 10:00–12:00 → 2 slots de 60min
        var rule = BuildRule(new TimeOnly(8, 0), new TimeOnly(12, 0), 60);
        var exception = AvailabilityException.CreateCustomHours(
            ResourceId, TestDate, new TimeOnly(10, 0), new TimeOnly(12, 0));
        var (handler, availRepo, _, bookingRepo) = BuildHandler();

        availRepo.Setup(r => r.GetRuleAsync(ResourceId, TestDate.DayOfWeek, default))
            .ReturnsAsync(rule);
        availRepo.Setup(r => r.GetExceptionAsync(ResourceId, TestDate, default))
            .ReturnsAsync(exception);
        bookingRepo.Setup(r => r.GetByResourceAsync(ResourceId, It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<Booking>());

        var result = await handler.Handle(
            new GetAvailableSlotsQuery(ResourceId, TestDate, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Should().HaveTimeSpan(new TimeSpan(10, 0, 0));
    }

    private static (GetAvailableSlotsQueryHandler handler,
        Mock<IAvailabilityRepository> availRepo,
        Mock<IServiceRepository> serviceRepo,
        Mock<IBookingRepository> bookingRepo) BuildHandler()
    {
        var availRepo   = new Mock<IAvailabilityRepository>();
        var serviceRepo = new Mock<IServiceRepository>();
        var bookingRepo = new Mock<IBookingRepository>();

        var handler = new GetAvailableSlotsQueryHandler(availRepo.Object, serviceRepo.Object, bookingRepo.Object);
        return (handler, availRepo, serviceRepo, bookingRepo);
    }
}

// Extensão auxiliar para assertions em DateTimeOffset com TimeSpan
file static class DateTimeOffsetExtensions
{
    public static void HaveTimeSpan(this DateTimeOffset value, TimeSpan expected)
    {
        value.TimeOfDay.Should().Be(expected);
    }
}
```

- [ ] **Step 2: Rodar — verificar falha**

```
dotnet test tests/Horafy.Application.Tests --filter "GetAvailableSlotsQueryHandlerTests" -v minimal
```
Esperado: erro de compilação (`GetAvailableSlotsQuery` e handler não existem).

- [ ] **Step 3: Criar GetAvailableSlotsQuery**

Criar `src/Horafy.Application/Features/Availability/Queries/GetAvailableSlotsQuery.cs`:

```csharp
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Queries;

public sealed record GetAvailableSlotsQuery(
    Guid ResourceId,
    DateOnly Date,
    Guid? ServiceId) : IRequest<Result<IReadOnlyList<DateTimeOffset>>>;

internal sealed class GetAvailableSlotsQueryHandler(
    IAvailabilityRepository availabilityRepository,
    IServiceRepository serviceRepository,
    IBookingRepository bookingRepository)
    : IRequestHandler<GetAvailableSlotsQuery, Result<IReadOnlyList<DateTimeOffset>>>
{
    public async Task<Result<IReadOnlyList<DateTimeOffset>>> Handle(
        GetAvailableSlotsQuery request, CancellationToken cancellationToken)
    {
        // 1. Regra semanal do recurso
        var rule = await availabilityRepository.GetRuleAsync(
            request.ResourceId, request.Date.DayOfWeek, cancellationToken);

        if (rule is null)
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        // 2. Verificar exceção para a data
        var exception = await availabilityRepository.GetExceptionAsync(
            request.ResourceId, request.Date, cancellationToken);

        if (exception?.IsBlocked is true)
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        var windowStart = exception?.CustomStart ?? rule.StartTime;
        var windowEnd   = exception?.CustomEnd   ?? rule.EndTime;

        // 3. Duração do slot: usa serviço se informado, senão usa a duração da regra
        int slotDuration = rule.SlotDurationMinutes;
        if (request.ServiceId.HasValue)
        {
            var service = await serviceRepository.GetByIdAsync(request.ServiceId.Value, cancellationToken);
            if (service is not null)
                slotDuration = service.DurationMinutes;
        }

        // 4. Gerar todos os slots possíveis na janela
        var step    = slotDuration + rule.BreakAfterMinutes;
        var allSlots = new List<DateTimeOffset>();
        var current = windowStart;

        while (current.Add(TimeSpan.FromMinutes(slotDuration)) <= windowEnd)
        {
            var slotStart = new DateTimeOffset(
                request.Date.ToDateTime(current, DateTimeKind.Utc));
            allSlots.Add(slotStart);
            current = current.Add(TimeSpan.FromMinutes(step));
        }

        if (allSlots.Count == 0)
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        // 5. Buscar bookings existentes na janela do dia
        var dayStart = new DateTimeOffset(request.Date.ToDateTime(windowStart, DateTimeKind.Utc));
        var dayEnd   = new DateTimeOffset(request.Date.ToDateTime(windowEnd,   DateTimeKind.Utc));

        var existingBookings = await bookingRepository.GetByResourceAsync(
            request.ResourceId, dayStart, dayEnd, cancellationToken);

        // 6. Filtrar slots ocupados
        var availableSlots = allSlots
            .Where(slot => !existingBookings.Any(b =>
                b.OverlapsWith(slot, slot.AddMinutes(slotDuration))))
            .ToList();

        return Result.Success<IReadOnlyList<DateTimeOffset>>(availableSlots);
    }
}
```

- [ ] **Step 4: Rodar testes — verificar verde**

```
dotnet test tests/Horafy.Application.Tests --filter "GetAvailableSlotsQueryHandlerTests" -v minimal
```
Esperado: todos os 6 testes passando.

- [ ] **Step 5: Rodar todos os testes**

```
dotnet test -v minimal
```
Esperado: todos os testes passando.

- [ ] **Step 6: Commit**

```
git add src/Horafy.Application/ tests/Horafy.Application.Tests/
git commit -m "feat: GetAvailableSlotsQuery — algoritmo de disponibilidade com testes"
```

---

## Task 8: AvailabilityController (API)

**Files:**
- Create: `src/Horafy.API/Controllers/V1/AvailabilityController.cs`

- [ ] **Step 1: Criar AvailabilityController**

Criar `src/Horafy.API/Controllers/V1/AvailabilityController.cs`:

```csharp
using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Availability.Commands;
using Horafy.Application.Features.Availability.Queries;
using Horafy.Application.Features.Resources.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize]
public sealed class AvailabilityController(ISender sender) : ApiControllerBase(sender)
{
    // ── Slots livres (público) ─────────────────────────────────────────
    [HttpGet("resources/{resourceId:guid}/slots")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<DateTimeOffset>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSlots(
        Guid resourceId,
        [FromQuery] DateOnly date,
        [FromQuery] Guid? serviceId = null,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(
            new GetAvailableSlotsQuery(resourceId, date, serviceId), cancellationToken));

    // ── Horários do tenant ────────────────────────────────────────────
    [HttpPut("business-hours")]
    [Authorize(Roles = "TenantOwner,TenantAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetBusinessHours(
        [FromBody] SetBusinessHoursRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new SetBusinessHoursCommand(
                request.DayOfWeek, request.OpenTime, request.CloseTime, request.IsOpen),
            cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    // ── Regra semanal de recurso ──────────────────────────────────────
    [HttpPut("resources/{resourceId:guid}/rules")]
    [Authorize(Roles = "TenantOwner,TenantAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetRule(
        Guid resourceId,
        [FromBody] SetAvailabilityRuleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new SetAvailabilityRuleCommand(
                resourceId, request.DayOfWeek, request.StartTime, request.EndTime,
                request.SlotDurationMinutes, request.BreakAfterMinutes),
            cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    // ── Exceção de disponibilidade ────────────────────────────────────
    [HttpPut("resources/{resourceId:guid}/exceptions")]
    [Authorize(Roles = "TenantOwner,TenantAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetException(
        Guid resourceId,
        [FromBody] SetAvailabilityExceptionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new SetAvailabilityExceptionCommand(
                resourceId, request.Date, request.IsBlocked,
                request.CustomStart, request.CustomEnd, request.Reason),
            cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    // ── Serviços do recurso ───────────────────────────────────────────
    [HttpPost("resources/{resourceId:guid}/services/{serviceId:guid}")]
    [Authorize(Roles = "TenantOwner,TenantAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddService(
        Guid resourceId, Guid serviceId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new AddResourceServiceCommand(resourceId, serviceId), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpDelete("resources/{resourceId:guid}/services/{serviceId:guid}")]
    [Authorize(Roles = "TenantOwner,TenantAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveService(
        Guid resourceId, Guid serviceId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new RemoveResourceServiceCommand(resourceId, serviceId), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record SetBusinessHoursRequest(
    DayOfWeek DayOfWeek, TimeOnly OpenTime, TimeOnly CloseTime, bool IsOpen);

public sealed record SetAvailabilityRuleRequest(
    DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime,
    int SlotDurationMinutes, int BreakAfterMinutes = 0);

public sealed record SetAvailabilityExceptionRequest(
    DateOnly Date, bool IsBlocked,
    TimeOnly? CustomStart, TimeOnly? CustomEnd, string? Reason);
```

- [ ] **Step 2: Build completo**

```
dotnet build -v minimal
```
Esperado: Build succeeded, 0 errors.

- [ ] **Step 3: Rodar todos os testes**

```
dotnet test -v minimal
```
Esperado: todos os testes passando.

- [ ] **Step 4: Commit**

```
git add src/Horafy.API/
git commit -m "feat: AvailabilityController — endpoints de slots, regras, horários e ResourceService"
```

---

## Task 9: EF Core Migration

**Files:**
- Nova migration em `src/Horafy.Infrastructure/Persistence/Migrations/`

- [ ] **Step 1: Gerar a migration**

```
dotnet ef migrations add Sprint4_ResourcesAndAvailability `
  --project src/Horafy.Infrastructure `
  --startup-project src/Horafy.API `
  --context HorafyDbContext `
  --output-dir Persistence/Migrations
```

> **Nota:** A migration do `HorafyDbContext` cobre o schema `public` (tabelas `tenants`, `users`). As tabelas de tenant (`resources`, `bookings`, `availability_*`) são criadas pelo `TenantMigrationService` quando um novo tenant é criado — elas não passam pelo `HorafyDbContext`. Verifique com o `TenantMigrationService` existente se ele usa `TenantDbContext.Database.EnsureCreatedAsync()` ou migrations dinâmicas.

- [ ] **Step 2: Verificar TenantMigrationService**

Abrir `src/Horafy.Infrastructure/MultiTenancy/TenantMigrationService.cs` e confirmar que ele chama `EnsureCreatedAsync()` ou `MigrateAsync()` no `TenantDbContext`. Se usar `EnsureCreatedAsync()`, as novas tabelas serão criadas automaticamente para novos tenants. Para tenants existentes em desenvolvimento, dropar e recriar o schema:

```sql
-- executar no psql para recriar o schema de um tenant de dev:
DROP SCHEMA IF EXISTS tenant_barbearia_joao CASCADE;
```

- [ ] **Step 3: Subir a infra local e testar**

```
docker-compose up -d postgres
dotnet run --project src/Horafy.API
```

Testar a sequência via Scalar (`/scalar`):
1. `POST /api/v1/platform/tenants` — cria tenant (cria schema)
2. `POST /api/v1/auth/login` — obtém JWT
3. `POST /api/v1/resources` — cria recurso (tipo: Professional)
4. `PUT /api/v1/availability/business-hours` — define horário Segunda 08:00–18:00
5. `PUT /api/v1/availability/resources/{id}/rules` — define regra Segunda 09:00–17:00, slot 60min, break 10min
6. `GET /api/v1/availability/resources/{id}/slots?date=YYYY-MM-DD` — verifica slots retornados
7. `POST /api/v1/bookings` — cria booking em um dos slots
8. `GET /api/v1/availability/resources/{id}/slots?date=YYYY-MM-DD` — confirma slot ocupado sumiu

- [ ] **Step 4: Rodar todos os testes uma última vez**

```
dotnet test -v minimal
```
Esperado: todos passando.

- [ ] **Step 5: Commit final da sprint**

```
git add .
git commit -m "feat: Sprint 4 completa — Resource, Availability, slots algorithm"
```

---

## Self-Review

### Cobertura do Spec

| Requisito do Spec (§6.3) | Task |
|---|---|
| CRUD de `Resource` (migração de `Professional`) | Tasks 1–3 |
| Vínculo `ResourceService` | Tasks 4–6 |
| Grade semanal via `AvailabilityRule` | Tasks 4–6 |
| Exceções via `AvailabilityException` | Tasks 4–6 |
| `BusinessHours` do tenant | Tasks 4–6 |
| Algoritmo de disponibilidade (slots livres) | Task 7 |
| Locking otimista (double-booking) | `BeginTransactionAsync` adicionado em Task 6; `HasConflictAsync` em transaction em Task 3 |
| `BreakAfterMinutes` entre atendimentos | Task 7 (algoritmo) |
| Testes de race condition | `HasConflictAsync` + SERIALIZABLE em `CreateBookingCommand` — teste de integração fica na Sprint 10 (Testcontainers) |
| Testes unitários do algoritmo | Task 7 (6 casos) |

### Type Consistency

- `ResourceId` (Guid) usado consistentemente em `Booking`, `BookingRepository`, `IBookingRepository`, `CreateBookingCommand`, `GetBookingsQuery` ✓
- `IAvailabilityRepository.Add<T>/Update<T>/Remove<T>` genérico — compatível com `AvailabilityRepository` ✓
- `GetAvailableSlotsQuery` retorna `IReadOnlyList<DateTimeOffset>` — consistente entre handler e controller ✓
- `SetAvailabilityExceptionCommand` usa `DateOnly` — consistente com `AvailabilityException.Date` ✓
