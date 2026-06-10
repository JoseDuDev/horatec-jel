# Admin Disponibilidade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Criar a página `/admin/disponibilidade` que permite ao proprietário gerenciar horários de funcionamento globais, grade de horários por recurso e exceções (folgas/feriados).

**Architecture:** Backend: 3 novas queries (GetBusinessHours, GetResourceRules, GetResourceExceptions) + 1 novo comando (DeleteAvailabilityException) + 4 endpoints GET/DELETE no AvailabilityController. Frontend: page com 3 abas + 3 componentes de edição. Os endpoints de escrita (PUT) já existem — só faltam os GETs e o DELETE.

**Tech Stack:** .NET 9 / MediatR / EF Core / xUnit + Moq + FluentAssertions; Next.js 15 / TypeScript / Shadcn UI / React

---

## Arquivos criados/modificados

| Arquivo | Ação |
|---|---|
| `src/Horafy.Domain/Interfaces/Repositories/IAvailabilityRepository.cs` | Modificar: adicionar `GetExceptionsByResourceAsync` |
| `src/Horafy.Infrastructure/Repositories/AvailabilityRepository.cs` | Modificar: implementar `GetExceptionsByResourceAsync` |
| `src/Horafy.Application/Features/Availability/AvailabilityErrors.cs` | Modificar: adicionar `ExceptionNotFound` |
| `src/Horafy.Application/Features/Availability/Queries/GetBusinessHoursQuery.cs` | Criar |
| `src/Horafy.Application/Features/Availability/Queries/GetResourceRulesQuery.cs` | Criar |
| `src/Horafy.Application/Features/Availability/Queries/GetResourceExceptionsQuery.cs` | Criar |
| `src/Horafy.Application/Features/Availability/Commands/DeleteAvailabilityExceptionCommand.cs` | Criar |
| `src/Horafy.API/Controllers/V1/AvailabilityController.cs` | Modificar: 4 novos endpoints |
| `tests/Horafy.Application.Tests/Availability/GetBusinessHoursQueryHandlerTests.cs` | Criar |
| `tests/Horafy.Application.Tests/Availability/GetResourceRulesQueryHandlerTests.cs` | Criar |
| `tests/Horafy.Application.Tests/Availability/GetResourceExceptionsQueryHandlerTests.cs` | Criar |
| `tests/Horafy.Application.Tests/Availability/DeleteAvailabilityExceptionCommandHandlerTests.cs` | Criar |
| `frontend/lib/types/availability.ts` | Criar |
| `frontend/lib/api/availability.ts` | Criar |
| `frontend/components/availability/BusinessHoursEditor.tsx` | Criar |
| `frontend/components/availability/ResourceRulesEditor.tsx` | Criar |
| `frontend/components/availability/ExceptionsEditor.tsx` | Criar |
| `frontend/app/(admin)/admin/disponibilidade/page.tsx` | Criar |
| `frontend/components/admin/Sidebar.tsx` | Modificar: adicionar link Disponibilidade |

---

## Task 1: Estender repositório e erros de disponibilidade

**Files:**
- Modify: `src/Horafy.Domain/Interfaces/Repositories/IAvailabilityRepository.cs`
- Modify: `src/Horafy.Infrastructure/Repositories/AvailabilityRepository.cs`
- Modify: `src/Horafy.Application/Features/Availability/AvailabilityErrors.cs`

- [ ] **Step 1: Adicionar `GetExceptionsByResourceAsync` à interface**

Em `src/Horafy.Domain/Interfaces/Repositories/IAvailabilityRepository.cs`, adicionar após `GetExceptionAsync`:

```csharp
Task<IReadOnlyList<AvailabilityException>> GetExceptionsByResourceAsync(
    Guid resourceId, DateOnly from, DateOnly to, CancellationToken ct = default);
```

- [ ] **Step 2: Implementar o método no repositório**

Em `src/Horafy.Infrastructure/Repositories/AvailabilityRepository.cs`, adicionar após o método `GetExceptionAsync`:

```csharp
public async Task<IReadOnlyList<AvailabilityException>> GetExceptionsByResourceAsync(
    Guid resourceId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
    await context.Set<AvailabilityException>()
        .AsNoTracking()
        .Where(e => e.ResourceId == resourceId && e.Date >= from && e.Date <= to)
        .OrderBy(e => e.Date)
        .ToListAsync(ct);
```

- [ ] **Step 3: Adicionar `ExceptionNotFound` em `AvailabilityErrors`**

Em `src/Horafy.Application/Features/Availability/AvailabilityErrors.cs`, adicionar após `ServiceNotLinked`:

```csharp
public static readonly Error ExceptionNotFound = new(
    "Availability.ExceptionNotFound", "Exceção não encontrada.", ErrorType.NotFound);
```

- [ ] **Step 4: Compilar para garantir que não há erros**

```bash
dotnet build src/Horafy.sln
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
git add src/Horafy.Domain/Interfaces/Repositories/IAvailabilityRepository.cs
git add src/Horafy.Infrastructure/Repositories/AvailabilityRepository.cs
git add src/Horafy.Application/Features/Availability/AvailabilityErrors.cs
git commit -m "feat: add GetExceptionsByResourceAsync to availability repository"
```

---

## Task 2: GetBusinessHoursQuery

**Files:**
- Create: `src/Horafy.Application/Features/Availability/Queries/GetBusinessHoursQuery.cs`
- Create: `tests/Horafy.Application.Tests/Availability/GetBusinessHoursQueryHandlerTests.cs`

- [ ] **Step 1: Escrever o teste que deve falhar**

Criar `tests/Horafy.Application.Tests/Availability/GetBusinessHoursQueryHandlerTests.cs`:

```csharp
using FluentAssertions;
using Horafy.Application.Features.Availability.Queries;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Availability;

public sealed class GetBusinessHoursQueryHandlerTests
{
    private readonly Mock<IAvailabilityRepository> _repo = new();

    private GetBusinessHoursQueryHandler MakeHandler() =>
        new(_repo.Object);

    [Fact]
    public async Task Handle_NenhumHorarioCadastrado_RetornaSeteDiasComPadraoFechado()
    {
        _repo.Setup(r => r.GetBusinessHoursAsync(default))
            .ReturnsAsync(new List<BusinessHours>());

        var result = await MakeHandler().Handle(new GetBusinessHoursQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(7);
        result.Value.Should().AllSatisfy(bh => bh.IsOpen.Should().BeFalse());
    }

    [Fact]
    public async Task Handle_HorarioSegundaCadastrado_RetornaValoresCadastradosParaSegunda()
    {
        var segunda = BusinessHours.Create(
            DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0), isOpen: true);
        _repo.Setup(r => r.GetBusinessHoursAsync(default))
            .ReturnsAsync(new List<BusinessHours> { segunda });

        var result = await MakeHandler().Handle(new GetBusinessHoursQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(7);

        var seg = result.Value.First(b => b.DayOfWeek == DayOfWeek.Monday);
        seg.IsOpen.Should().BeTrue();
        seg.OpenTime.Should().Be(new TimeOnly(8, 0));
        seg.CloseTime.Should().Be(new TimeOnly(17, 0));

        result.Value.Where(b => b.DayOfWeek != DayOfWeek.Monday)
            .Should().AllSatisfy(b => b.IsOpen.Should().BeFalse());
    }
}
```

- [ ] **Step 2: Rodar o teste para confirmar que falha**

```bash
dotnet test --filter "GetBusinessHoursQueryHandlerTests"
```

Expected: falha com `CS0246: The type or namespace name 'GetBusinessHoursQueryHandler' could not be found`

- [ ] **Step 3: Criar a query e o handler**

Criar `src/Horafy.Application/Features/Availability/Queries/GetBusinessHoursQuery.cs`:

```csharp
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Queries;

public sealed record GetBusinessHoursQuery : IRequest<Result<IReadOnlyList<BusinessHoursResult>>>;

public sealed record BusinessHoursResult(
    DayOfWeek DayOfWeek,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    bool IsOpen);

internal sealed class GetBusinessHoursQueryHandler(
    IAvailabilityRepository repository)
    : IRequestHandler<GetBusinessHoursQuery, Result<IReadOnlyList<BusinessHoursResult>>>
{
    public async Task<Result<IReadOnlyList<BusinessHoursResult>>> Handle(
        GetBusinessHoursQuery request, CancellationToken cancellationToken)
    {
        var stored = await repository.GetBusinessHoursAsync(cancellationToken);

        var result = Enum.GetValues<DayOfWeek>()
            .Select(day =>
            {
                var bh = stored.FirstOrDefault(b => b.DayOfWeek == day);
                return bh is not null
                    ? new BusinessHoursResult(bh.DayOfWeek, bh.OpenTime, bh.CloseTime, bh.IsOpen)
                    : new BusinessHoursResult(day, new TimeOnly(9, 0), new TimeOnly(18, 0), false);
            })
            .ToList();

        return Result.Success<IReadOnlyList<BusinessHoursResult>>(result);
    }
}
```

- [ ] **Step 4: Rodar o teste para confirmar que passa**

```bash
dotnet test --filter "GetBusinessHoursQueryHandlerTests"
```

Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 5: Commit**

```bash
git add src/Horafy.Application/Features/Availability/Queries/GetBusinessHoursQuery.cs
git add tests/Horafy.Application.Tests/Availability/GetBusinessHoursQueryHandlerTests.cs
git commit -m "feat: add GetBusinessHoursQuery handler"
```

---

## Task 3: GetResourceRulesQuery

**Files:**
- Create: `src/Horafy.Application/Features/Availability/Queries/GetResourceRulesQuery.cs`
- Create: `tests/Horafy.Application.Tests/Availability/GetResourceRulesQueryHandlerTests.cs`

- [ ] **Step 1: Escrever o teste que deve falhar**

Criar `tests/Horafy.Application.Tests/Availability/GetResourceRulesQueryHandlerTests.cs`:

```csharp
using FluentAssertions;
using Horafy.Application.Features.Availability.Queries;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Availability;

public sealed class GetResourceRulesQueryHandlerTests
{
    private static readonly Guid ResourceId = Guid.NewGuid();
    private readonly Mock<IAvailabilityRepository> _repo = new();

    private GetResourceRulesQueryHandler MakeHandler() =>
        new(_repo.Object);

    [Fact]
    public async Task Handle_SemRegras_RetornaListaVazia()
    {
        _repo.Setup(r => r.GetRulesByResourceAsync(ResourceId, default))
            .ReturnsAsync(new List<AvailabilityRule>());

        var result = await MakeHandler().Handle(new GetResourceRulesQuery(ResourceId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ComRegra_RetornaDadosMapeados()
    {
        var rule = AvailabilityRule.Create(
            ResourceId, DayOfWeek.Monday,
            new TimeOnly(9, 0), new TimeOnly(17, 0), 60, breakAfterMinutes: 10);
        _repo.Setup(r => r.GetRulesByResourceAsync(ResourceId, default))
            .ReturnsAsync(new List<AvailabilityRule> { rule });

        var result = await MakeHandler().Handle(new GetResourceRulesQuery(ResourceId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].DayOfWeek.Should().Be(DayOfWeek.Monday);
        result.Value[0].StartTime.Should().Be(new TimeOnly(9, 0));
        result.Value[0].EndTime.Should().Be(new TimeOnly(17, 0));
        result.Value[0].SlotDurationMinutes.Should().Be(60);
        result.Value[0].BreakAfterMinutes.Should().Be(10);
    }
}
```

- [ ] **Step 2: Rodar o teste para confirmar que falha**

```bash
dotnet test --filter "GetResourceRulesQueryHandlerTests"
```

Expected: falha com `CS0246: 'GetResourceRulesQueryHandler' could not be found`

- [ ] **Step 3: Criar a query e o handler**

Criar `src/Horafy.Application/Features/Availability/Queries/GetResourceRulesQuery.cs`:

```csharp
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Queries;

public sealed record GetResourceRulesQuery(Guid ResourceId)
    : IRequest<Result<IReadOnlyList<AvailabilityRuleResult>>>;

public sealed record AvailabilityRuleResult(
    Guid Id,
    Guid ResourceId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int SlotDurationMinutes,
    int BreakAfterMinutes);

internal sealed class GetResourceRulesQueryHandler(
    IAvailabilityRepository repository)
    : IRequestHandler<GetResourceRulesQuery, Result<IReadOnlyList<AvailabilityRuleResult>>>
{
    public async Task<Result<IReadOnlyList<AvailabilityRuleResult>>> Handle(
        GetResourceRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await repository.GetRulesByResourceAsync(request.ResourceId, cancellationToken);

        var result = rules.Select(r => new AvailabilityRuleResult(
            r.Id, r.ResourceId, r.DayOfWeek,
            r.StartTime, r.EndTime,
            r.SlotDurationMinutes, r.BreakAfterMinutes)).ToList();

        return Result.Success<IReadOnlyList<AvailabilityRuleResult>>(result);
    }
}
```

- [ ] **Step 4: Rodar o teste para confirmar que passa**

```bash
dotnet test --filter "GetResourceRulesQueryHandlerTests"
```

Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 5: Commit**

```bash
git add src/Horafy.Application/Features/Availability/Queries/GetResourceRulesQuery.cs
git add tests/Horafy.Application.Tests/Availability/GetResourceRulesQueryHandlerTests.cs
git commit -m "feat: add GetResourceRulesQuery handler"
```

---

## Task 4: GetResourceExceptionsQuery

**Files:**
- Create: `src/Horafy.Application/Features/Availability/Queries/GetResourceExceptionsQuery.cs`
- Create: `tests/Horafy.Application.Tests/Availability/GetResourceExceptionsQueryHandlerTests.cs`

- [ ] **Step 1: Escrever o teste que deve falhar**

Criar `tests/Horafy.Application.Tests/Availability/GetResourceExceptionsQueryHandlerTests.cs`:

```csharp
using FluentAssertions;
using Horafy.Application.Features.Availability.Queries;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Availability;

public sealed class GetResourceExceptionsQueryHandlerTests
{
    private static readonly Guid ResourceId = Guid.NewGuid();
    private static readonly DateOnly From = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly To = From.AddDays(30);

    private readonly Mock<IAvailabilityRepository> _repo = new();

    private GetResourceExceptionsQueryHandler MakeHandler() =>
        new(_repo.Object);

    [Fact]
    public async Task Handle_SemExcecoes_RetornaListaVazia()
    {
        _repo.Setup(r => r.GetExceptionsByResourceAsync(ResourceId, From, To, default))
            .ReturnsAsync(new List<AvailabilityException>());

        var result = await MakeHandler().Handle(
            new GetResourceExceptionsQuery(ResourceId, From, To), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ComExcecaoBloqueada_RetornaDadosMapeados()
    {
        var date = From.AddDays(5);
        var excecao = AvailabilityException.CreateBlock(ResourceId, date, "Feriado Nacional");
        _repo.Setup(r => r.GetExceptionsByResourceAsync(ResourceId, From, To, default))
            .ReturnsAsync(new List<AvailabilityException> { excecao });

        var result = await MakeHandler().Handle(
            new GetResourceExceptionsQuery(ResourceId, From, To), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Date.Should().Be(date);
        result.Value[0].IsBlocked.Should().BeTrue();
        result.Value[0].Reason.Should().Be("Feriado Nacional");
        result.Value[0].CustomStart.Should().BeNull();
        result.Value[0].CustomEnd.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ComExcecaoHorarioCustom_RetornaHorarioCustom()
    {
        var date = From.AddDays(3);
        var excecao = AvailabilityException.CreateCustomHours(
            ResourceId, date, new TimeOnly(10, 0), new TimeOnly(14, 0), "Expediente reduzido");
        _repo.Setup(r => r.GetExceptionsByResourceAsync(ResourceId, From, To, default))
            .ReturnsAsync(new List<AvailabilityException> { excecao });

        var result = await MakeHandler().Handle(
            new GetResourceExceptionsQuery(ResourceId, From, To), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].IsBlocked.Should().BeFalse();
        result.Value[0].CustomStart.Should().Be(new TimeOnly(10, 0));
        result.Value[0].CustomEnd.Should().Be(new TimeOnly(14, 0));
    }
}
```

- [ ] **Step 2: Rodar o teste para confirmar que falha**

```bash
dotnet test --filter "GetResourceExceptionsQueryHandlerTests"
```

Expected: falha com `CS0246: 'GetResourceExceptionsQueryHandler' could not be found`

- [ ] **Step 3: Criar a query e o handler**

Criar `src/Horafy.Application/Features/Availability/Queries/GetResourceExceptionsQuery.cs`:

```csharp
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Queries;

public sealed record GetResourceExceptionsQuery(Guid ResourceId, DateOnly From, DateOnly To)
    : IRequest<Result<IReadOnlyList<AvailabilityExceptionResult>>>;

public sealed record AvailabilityExceptionResult(
    Guid Id,
    Guid ResourceId,
    DateOnly Date,
    bool IsBlocked,
    TimeOnly? CustomStart,
    TimeOnly? CustomEnd,
    string? Reason);

internal sealed class GetResourceExceptionsQueryHandler(
    IAvailabilityRepository repository)
    : IRequestHandler<GetResourceExceptionsQuery, Result<IReadOnlyList<AvailabilityExceptionResult>>>
{
    public async Task<Result<IReadOnlyList<AvailabilityExceptionResult>>> Handle(
        GetResourceExceptionsQuery request, CancellationToken cancellationToken)
    {
        var exceptions = await repository.GetExceptionsByResourceAsync(
            request.ResourceId, request.From, request.To, cancellationToken);

        var result = exceptions.Select(e => new AvailabilityExceptionResult(
            e.Id, e.ResourceId, e.Date,
            e.IsBlocked, e.CustomStart, e.CustomEnd, e.Reason)).ToList();

        return Result.Success<IReadOnlyList<AvailabilityExceptionResult>>(result);
    }
}
```

- [ ] **Step 4: Rodar o teste para confirmar que passa**

```bash
dotnet test --filter "GetResourceExceptionsQueryHandlerTests"
```

Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 5: Commit**

```bash
git add src/Horafy.Application/Features/Availability/Queries/GetResourceExceptionsQuery.cs
git add tests/Horafy.Application.Tests/Availability/GetResourceExceptionsQueryHandlerTests.cs
git commit -m "feat: add GetResourceExceptionsQuery handler"
```

---

## Task 5: DeleteAvailabilityExceptionCommand

**Files:**
- Create: `src/Horafy.Application/Features/Availability/Commands/DeleteAvailabilityExceptionCommand.cs`
- Create: `tests/Horafy.Application.Tests/Availability/DeleteAvailabilityExceptionCommandHandlerTests.cs`

- [ ] **Step 1: Escrever o teste que deve falhar**

Criar `tests/Horafy.Application.Tests/Availability/DeleteAvailabilityExceptionCommandHandlerTests.cs`:

```csharp
using FluentAssertions;
using Horafy.Application.Features.Availability;
using Horafy.Application.Features.Availability.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Availability;

public sealed class DeleteAvailabilityExceptionCommandHandlerTests
{
    private static readonly Guid ResourceId = Guid.NewGuid();
    private readonly Mock<IAvailabilityRepository> _repo = new();
    private readonly Mock<ITenantUnitOfWork> _uow = new();

    private DeleteAvailabilityExceptionCommandHandler MakeHandler() =>
        new(_repo.Object, _uow.Object);

    [Fact]
    public async Task Handle_ExcecaoExiste_RemoveERetornaSucesso()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        var excecao = AvailabilityException.CreateBlock(ResourceId, date);
        _repo.Setup(r => r.GetExceptionAsync(ResourceId, date, default)).ReturnsAsync(excecao);

        var result = await MakeHandler().Handle(
            new DeleteAvailabilityExceptionCommand(ResourceId, date), default);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.Remove(excecao), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_ExcecaoNaoEncontrada_RetornaFalha()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        _repo.Setup(r => r.GetExceptionAsync(ResourceId, date, default))
            .ReturnsAsync((AvailabilityException?)null);

        var result = await MakeHandler().Handle(
            new DeleteAvailabilityExceptionCommand(ResourceId, date), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AvailabilityErrors.ExceptionNotFound);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 2: Rodar o teste para confirmar que falha**

```bash
dotnet test --filter "DeleteAvailabilityExceptionCommandHandlerTests"
```

Expected: falha com `CS0246: 'DeleteAvailabilityExceptionCommandHandler' could not be found`

- [ ] **Step 3: Criar o comando e o handler**

Criar `src/Horafy.Application/Features/Availability/Commands/DeleteAvailabilityExceptionCommand.cs`:

```csharp
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Commands;

public sealed record DeleteAvailabilityExceptionCommand(
    Guid ResourceId,
    DateOnly Date) : IRequest<Result>;

internal sealed class DeleteAvailabilityExceptionCommandHandler(
    IAvailabilityRepository availabilityRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<DeleteAvailabilityExceptionCommand, Result>
{
    public async Task<Result> Handle(
        DeleteAvailabilityExceptionCommand request, CancellationToken cancellationToken)
    {
        var existing = await availabilityRepository
            .GetExceptionAsync(request.ResourceId, request.Date, cancellationToken);

        if (existing is null)
            return Result.Failure(AvailabilityErrors.ExceptionNotFound);

        availabilityRepository.Remove(existing);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 4: Rodar o teste para confirmar que passa**

```bash
dotnet test --filter "DeleteAvailabilityExceptionCommandHandlerTests"
```

Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 5: Rodar todos os testes de disponibilidade**

```bash
dotnet test --filter "Namespace~Horafy.Application.Tests.Availability"
```

Expected: todos passam.

- [ ] **Step 6: Commit**

```bash
git add src/Horafy.Application/Features/Availability/Commands/DeleteAvailabilityExceptionCommand.cs
git add tests/Horafy.Application.Tests/Availability/DeleteAvailabilityExceptionCommandHandlerTests.cs
git commit -m "feat: add DeleteAvailabilityExceptionCommand handler"
```

---

## Task 6: Endpoints no AvailabilityController

**Files:**
- Modify: `src/Horafy.API/Controllers/V1/AvailabilityController.cs`

- [ ] **Step 1: Adicionar os 4 novos endpoints**

Em `src/Horafy.API/Controllers/V1/AvailabilityController.cs`:

1. Adicionar os usings que faltam no topo do arquivo (logo após os existentes):

```csharp
using Horafy.Application.Features.Availability.Commands;
using Horafy.Application.Features.Availability.Queries;
```

2. Adicionar os 4 endpoints abaixo do método `GetSlots` existente, antes do método `SetBusinessHours`:

```csharp
[HttpGet("business-hours")]
[Authorize(Roles = "TenantOwner,TenantAdmin")]
[ProducesResponseType(typeof(IReadOnlyList<BusinessHoursResult>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetBusinessHours(CancellationToken cancellationToken) =>
    ToActionResult(await Sender.Send(new GetBusinessHoursQuery(), cancellationToken));

[HttpGet("resources/{resourceId:guid}/rules")]
[Authorize(Roles = "TenantOwner,TenantAdmin")]
[ProducesResponseType(typeof(IReadOnlyList<AvailabilityRuleResult>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetResourceRules(
    Guid resourceId, CancellationToken cancellationToken) =>
    ToActionResult(await Sender.Send(new GetResourceRulesQuery(resourceId), cancellationToken));

[HttpGet("resources/{resourceId:guid}/exceptions")]
[Authorize(Roles = "TenantOwner,TenantAdmin")]
[ProducesResponseType(typeof(IReadOnlyList<AvailabilityExceptionResult>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetResourceExceptions(
    Guid resourceId,
    [FromQuery] DateOnly from,
    [FromQuery] DateOnly to,
    CancellationToken cancellationToken) =>
    ToActionResult(await Sender.Send(
        new GetResourceExceptionsQuery(resourceId, from, to), cancellationToken));

[HttpDelete("resources/{resourceId:guid}/exceptions/{date}")]
[Authorize(Roles = "TenantOwner,TenantAdmin")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> DeleteException(
    Guid resourceId,
    DateOnly date,
    CancellationToken cancellationToken)
{
    var result = await Sender.Send(
        new DeleteAvailabilityExceptionCommand(resourceId, date), cancellationToken);
    return result.IsSuccess ? NoContent() : ToActionResult(result);
}
```

- [ ] **Step 2: Compilar para garantir que não há erros**

```bash
dotnet build src/Horafy.sln
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Rodar todos os testes**

```bash
dotnet test
```

Expected: todos passam.

- [ ] **Step 4: Commit**

```bash
git add src/Horafy.API/Controllers/V1/AvailabilityController.cs
git commit -m "feat: add GET business-hours, GET/DELETE resource rules and exceptions endpoints"
```

---

## Task 7: Frontend — tipos e API client

**Files:**
- Create: `frontend/lib/types/availability.ts`
- Create: `frontend/lib/api/availability.ts`

- [ ] **Step 1: Criar os tipos de disponibilidade**

Criar `frontend/lib/types/availability.ts`:

```typescript
export interface BusinessHoursDto {
  dayOfWeek: number   // 0 = Domingo … 6 = Sábado
  openTime: string    // "HH:mm:ss"
  closeTime: string   // "HH:mm:ss"
  isOpen: boolean
}

export interface AvailabilityRuleDto {
  id: string
  resourceId: string
  dayOfWeek: number
  startTime: string           // "HH:mm:ss"
  endTime: string             // "HH:mm:ss"
  slotDurationMinutes: number
  breakAfterMinutes: number
}

export interface AvailabilityExceptionDto {
  id: string
  resourceId: string
  date: string                // "yyyy-MM-dd"
  isBlocked: boolean
  customStart?: string        // "HH:mm:ss"
  customEnd?: string          // "HH:mm:ss"
  reason?: string
}

export interface SetResourceRuleRequest {
  dayOfWeek: number
  startTime: string           // "HH:mm:ss"
  endTime: string             // "HH:mm:ss"
  slotDurationMinutes: number
  breakAfterMinutes: number
}

export interface SetResourceExceptionRequest {
  date: string                // "yyyy-MM-dd"
  isBlocked: boolean
  customStart?: string        // "HH:mm:ss"
  customEnd?: string          // "HH:mm:ss"
  reason?: string
}
```

- [ ] **Step 2: Criar o API client**

Criar `frontend/lib/api/availability.ts`:

```typescript
import { apiFetch } from './client'
import type {
  BusinessHoursDto,
  AvailabilityRuleDto,
  AvailabilityExceptionDto,
  SetResourceRuleRequest,
  SetResourceExceptionRequest,
} from '../types/availability'

export const availabilityApi = {
  getBusinessHours: () =>
    apiFetch<BusinessHoursDto[]>('/api/v1/availability/business-hours'),

  setBusinessHours: (
    dayOfWeek: number,
    isOpen: boolean,
    openTime: string,
    closeTime: string
  ) =>
    apiFetch<void>('/api/v1/availability/business-hours', {
      method: 'PUT',
      body: JSON.stringify({ dayOfWeek, isOpen, openTime, closeTime }),
    }),

  getResourceRules: (resourceId: string) =>
    apiFetch<AvailabilityRuleDto[]>(
      `/api/v1/availability/resources/${resourceId}/rules`
    ),

  setResourceRule: (resourceId: string, data: SetResourceRuleRequest) =>
    apiFetch<void>(`/api/v1/availability/resources/${resourceId}/rules`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),

  getResourceExceptions: (resourceId: string, from: string, to: string) =>
    apiFetch<AvailabilityExceptionDto[]>(
      `/api/v1/availability/resources/${resourceId}/exceptions?from=${from}&to=${to}`
    ),

  setResourceException: (resourceId: string, data: SetResourceExceptionRequest) =>
    apiFetch<void>(`/api/v1/availability/resources/${resourceId}/exceptions`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),

  deleteResourceException: (resourceId: string, date: string) =>
    apiFetch<void>(
      `/api/v1/availability/resources/${resourceId}/exceptions/${date}`,
      { method: 'DELETE' }
    ),
}
```

- [ ] **Step 3: Verificar tipos com TypeScript**

```bash
cd frontend && npx tsc --noEmit
```

Expected: `0 errors`

- [ ] **Step 4: Commit**

```bash
git add frontend/lib/types/availability.ts frontend/lib/api/availability.ts
git commit -m "feat: add availability types and API client"
```

---

## Task 8: BusinessHoursEditor component

**Files:**
- Create: `frontend/components/availability/BusinessHoursEditor.tsx`

- [ ] **Step 1: Criar o componente**

Criar `frontend/components/availability/BusinessHoursEditor.tsx`:

```tsx
'use client'

import { useEffect, useState } from 'react'
import { availabilityApi } from '@/lib/api/availability'
import type { BusinessHoursDto } from '@/lib/types/availability'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

const DAY_LABELS = ['Domingo', 'Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado']

export function BusinessHoursEditor() {
  const [schedule, setSchedule] = useState<BusinessHoursDto[]>([])
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    availabilityApi.getBusinessHours().then(setSchedule)
  }, [])

  const update = (
    dayOfWeek: number,
    field: keyof BusinessHoursDto,
    value: string | boolean
  ) => {
    setSchedule(s =>
      s.map(d => (d.dayOfWeek === dayOfWeek ? { ...d, [field]: value } : d))
    )
  }

  const handleSave = async () => {
    setSaving(true)
    try {
      await Promise.all(
        schedule.map(d =>
          availabilityApi.setBusinessHours(d.dayOfWeek, d.isOpen, d.openTime, d.closeTime)
        )
      )
      setSaved(true)
      setTimeout(() => setSaved(false), 3000)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="space-y-4 max-w-lg">
      {schedule.map(d => (
        <div key={d.dayOfWeek} className="flex items-center gap-3">
          <label className="flex items-center gap-2 w-28 shrink-0 cursor-pointer">
            <input
              type="checkbox"
              checked={d.isOpen}
              onChange={e => update(d.dayOfWeek, 'isOpen', e.target.checked)}
              className="rounded"
            />
            <span className="text-sm font-medium">{DAY_LABELS[d.dayOfWeek]}</span>
          </label>
          {d.isOpen ? (
            <>
              <Input
                type="time"
                value={d.openTime.slice(0, 5)}
                onChange={e => update(d.dayOfWeek, 'openTime', `${e.target.value}:00`)}
                className="w-28"
              />
              <span className="text-slate-400 text-sm">até</span>
              <Input
                type="time"
                value={d.closeTime.slice(0, 5)}
                onChange={e => update(d.dayOfWeek, 'closeTime', `${e.target.value}:00`)}
                className="w-28"
              />
            </>
          ) : (
            <span className="text-sm text-slate-400">Fechado</span>
          )}
        </div>
      ))}
      <div className="flex items-center gap-4 pt-2">
        <Button onClick={handleSave} disabled={saving || schedule.length === 0}>
          {saving ? 'Salvando...' : 'Salvar'}
        </Button>
        {saved && <span className="text-sm text-green-600">Salvo com sucesso!</span>}
      </div>
    </div>
  )
}
```

- [ ] **Step 2: Verificar tipos**

```bash
cd frontend && npx tsc --noEmit
```

Expected: `0 errors`

- [ ] **Step 3: Commit**

```bash
git add frontend/components/availability/BusinessHoursEditor.tsx
git commit -m "feat: add BusinessHoursEditor component"
```

---

## Task 9: ResourceRulesEditor component

**Files:**
- Create: `frontend/components/availability/ResourceRulesEditor.tsx`

- [ ] **Step 1: Criar o componente**

Criar `frontend/components/availability/ResourceRulesEditor.tsx`:

```tsx
'use client'

import { useEffect, useState } from 'react'
import { availabilityApi } from '@/lib/api/availability'
import type { Resource } from '@/lib/types/resource'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

const DAY_LABELS = ['Domingo', 'Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado']

interface DayRow {
  dayOfWeek: number
  enabled: boolean
  startTime: string
  endTime: string
  slotDurationMinutes: number
  breakAfterMinutes: number
}

const DEFAULT_ROWS: DayRow[] = Array.from({ length: 7 }, (_, i) => ({
  dayOfWeek: i,
  enabled: i >= 1 && i <= 5,
  startTime: '09:00',
  endTime: '18:00',
  slotDurationMinutes: 60,
  breakAfterMinutes: 0,
}))

interface Props {
  resources: Resource[]
}

export function ResourceRulesEditor({ resources }: Props) {
  const [resourceId, setResourceId] = useState<string>('')
  const [rows, setRows] = useState<DayRow[]>(DEFAULT_ROWS)
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    if (!resourceId) return
    availabilityApi.getResourceRules(resourceId).then(rules => {
      setRows(
        DEFAULT_ROWS.map(d => {
          const rule = rules.find(r => r.dayOfWeek === d.dayOfWeek)
          if (!rule) return { ...d, enabled: false }
          return {
            dayOfWeek: d.dayOfWeek,
            enabled: true,
            startTime: rule.startTime.slice(0, 5),
            endTime: rule.endTime.slice(0, 5),
            slotDurationMinutes: rule.slotDurationMinutes,
            breakAfterMinutes: rule.breakAfterMinutes,
          }
        })
      )
    })
  }, [resourceId])

  const update = (
    dayOfWeek: number,
    field: keyof DayRow,
    value: string | boolean | number
  ) => {
    setRows(r =>
      r.map(d => (d.dayOfWeek === dayOfWeek ? { ...d, [field]: value } : d))
    )
  }

  const handleSave = async () => {
    if (!resourceId) return
    setSaving(true)
    try {
      await Promise.all(
        rows
          .filter(d => d.enabled)
          .map(d =>
            availabilityApi.setResourceRule(resourceId, {
              dayOfWeek: d.dayOfWeek,
              startTime: `${d.startTime}:00`,
              endTime: `${d.endTime}:00`,
              slotDurationMinutes: d.slotDurationMinutes,
              breakAfterMinutes: d.breakAfterMinutes,
            })
          )
      )
      setSaved(true)
      setTimeout(() => setSaved(false), 3000)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="space-y-6 max-w-3xl">
      <div className="max-w-xs">
        <Label>Recurso</Label>
        <Select value={resourceId} onValueChange={setResourceId}>
          <SelectTrigger className="mt-1">
            <SelectValue placeholder="Selecione um recurso..." />
          </SelectTrigger>
          <SelectContent>
            {resources.map(r => (
              <SelectItem key={r.id} value={r.id}>
                {r.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {resourceId && (
        <>
          <div className="space-y-2">
            <div className="grid grid-cols-[9rem_1fr_1fr_6rem_6rem] gap-2 text-xs text-slate-500 font-medium px-1">
              <span>Dia</span>
              <span>Início</span>
              <span>Fim</span>
              <span>Slot (min)</span>
              <span>Break (min)</span>
            </div>
            {rows.map(d => (
              <div
                key={d.dayOfWeek}
                className="grid grid-cols-[9rem_1fr_1fr_6rem_6rem] gap-2 items-center"
              >
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={d.enabled}
                    onChange={e => update(d.dayOfWeek, 'enabled', e.target.checked)}
                    className="rounded"
                  />
                  <span className="text-sm font-medium">{DAY_LABELS[d.dayOfWeek]}</span>
                </label>
                {d.enabled ? (
                  <>
                    <Input
                      type="time"
                      value={d.startTime}
                      onChange={e => update(d.dayOfWeek, 'startTime', e.target.value)}
                    />
                    <Input
                      type="time"
                      value={d.endTime}
                      onChange={e => update(d.dayOfWeek, 'endTime', e.target.value)}
                    />
                    <Input
                      type="number"
                      min={5}
                      max={480}
                      value={d.slotDurationMinutes}
                      onChange={e =>
                        update(d.dayOfWeek, 'slotDurationMinutes', parseInt(e.target.value))
                      }
                    />
                    <Input
                      type="number"
                      min={0}
                      max={120}
                      value={d.breakAfterMinutes}
                      onChange={e =>
                        update(d.dayOfWeek, 'breakAfterMinutes', parseInt(e.target.value))
                      }
                    />
                  </>
                ) : (
                  <span className="text-sm text-slate-400 col-span-4">Sem atendimento</span>
                )}
              </div>
            ))}
          </div>

          <div className="flex items-center gap-4">
            <Button onClick={handleSave} disabled={saving}>
              {saving ? 'Salvando...' : 'Salvar grade'}
            </Button>
            {saved && <span className="text-sm text-green-600">Salvo!</span>}
          </div>
        </>
      )}
    </div>
  )
}
```

- [ ] **Step 2: Verificar tipos**

```bash
cd frontend && npx tsc --noEmit
```

Expected: `0 errors`

- [ ] **Step 3: Commit**

```bash
git add frontend/components/availability/ResourceRulesEditor.tsx
git commit -m "feat: add ResourceRulesEditor component"
```

---

## Task 10: ExceptionsEditor component

**Files:**
- Create: `frontend/components/availability/ExceptionsEditor.tsx`

- [ ] **Step 1: Criar o componente**

Criar `frontend/components/availability/ExceptionsEditor.tsx`:

```tsx
'use client'

import { useEffect, useState } from 'react'
import { format, addDays } from 'date-fns'
import { availabilityApi } from '@/lib/api/availability'
import type { AvailabilityExceptionDto } from '@/lib/types/availability'
import type { Resource } from '@/lib/types/resource'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Trash2 } from 'lucide-react'

interface Props {
  resources: Resource[]
}

interface ExceptionForm {
  date: string
  isBlocked: boolean
  customStart: string
  customEnd: string
  reason: string
}

export function ExceptionsEditor({ resources }: Props) {
  const [resourceId, setResourceId] = useState<string>('')
  const [exceptions, setExceptions] = useState<AvailabilityExceptionDto[]>([])
  const [form, setForm] = useState<ExceptionForm>({
    date: format(addDays(new Date(), 1), 'yyyy-MM-dd'),
    isBlocked: true,
    customStart: '09:00',
    customEnd: '12:00',
    reason: '',
  })
  const [saving, setSaving] = useState(false)

  const loadExceptions = (id: string) => {
    const from = format(new Date(), 'yyyy-MM-dd')
    const to = format(addDays(new Date(), 90), 'yyyy-MM-dd')
    availabilityApi.getResourceExceptions(id, from, to).then(setExceptions)
  }

  useEffect(() => {
    if (resourceId) loadExceptions(resourceId)
  }, [resourceId])

  const handleAdd = async () => {
    if (!resourceId) return
    setSaving(true)
    try {
      await availabilityApi.setResourceException(resourceId, {
        date: form.date,
        isBlocked: form.isBlocked,
        customStart: form.isBlocked ? undefined : `${form.customStart}:00`,
        customEnd: form.isBlocked ? undefined : `${form.customEnd}:00`,
        reason: form.reason || undefined,
      })
      loadExceptions(resourceId)
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (date: string) => {
    if (!resourceId) return
    await availabilityApi.deleteResourceException(resourceId, date)
    loadExceptions(resourceId)
  }

  return (
    <div className="space-y-8 max-w-xl">
      <div className="max-w-xs">
        <Label>Recurso</Label>
        <Select value={resourceId} onValueChange={setResourceId}>
          <SelectTrigger className="mt-1">
            <SelectValue placeholder="Selecione um recurso..." />
          </SelectTrigger>
          <SelectContent>
            {resources.map(r => (
              <SelectItem key={r.id} value={r.id}>
                {r.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {resourceId && (
        <>
          <div className="border rounded-lg p-4 space-y-4">
            <h3 className="font-medium text-sm text-slate-700">Nova exceção</h3>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Data</Label>
                <Input
                  type="date"
                  value={form.date}
                  min={format(new Date(), 'yyyy-MM-dd')}
                  onChange={e => setForm(f => ({ ...f, date: e.target.value }))}
                  className="mt-1"
                />
              </div>
              <div className="flex items-end pb-1">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={form.isBlocked}
                    onChange={e => setForm(f => ({ ...f, isBlocked: e.target.checked }))}
                    className="rounded"
                  />
                  <span className="text-sm font-medium">Dia bloqueado</span>
                </label>
              </div>
            </div>

            {!form.isBlocked && (
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <Label>Início</Label>
                  <Input
                    type="time"
                    value={form.customStart}
                    onChange={e => setForm(f => ({ ...f, customStart: e.target.value }))}
                    className="mt-1"
                  />
                </div>
                <div>
                  <Label>Fim</Label>
                  <Input
                    type="time"
                    value={form.customEnd}
                    onChange={e => setForm(f => ({ ...f, customEnd: e.target.value }))}
                    className="mt-1"
                  />
                </div>
              </div>
            )}

            <div>
              <Label>Motivo (opcional)</Label>
              <Input
                value={form.reason}
                onChange={e => setForm(f => ({ ...f, reason: e.target.value }))}
                placeholder="Ex: Feriado, folga, manutenção..."
                className="mt-1"
              />
            </div>

            <Button onClick={handleAdd} disabled={saving}>
              {saving ? 'Salvando...' : 'Adicionar exceção'}
            </Button>
          </div>

          <div className="space-y-3">
            <h3 className="font-medium text-sm text-slate-700">
              Exceções nos próximos 90 dias
            </h3>
            {exceptions.length === 0 ? (
              <p className="text-sm text-slate-400">Nenhuma exceção cadastrada.</p>
            ) : (
              exceptions.map(e => (
                <div
                  key={e.id}
                  className="flex items-center justify-between p-3 border rounded-lg"
                >
                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-medium">{e.date}</span>
                      <Badge variant={e.isBlocked ? 'destructive' : 'secondary'}>
                        {e.isBlocked
                          ? 'Bloqueado'
                          : `${e.customStart?.slice(0, 5)} – ${e.customEnd?.slice(0, 5)}`}
                      </Badge>
                    </div>
                    {e.reason && (
                      <p className="text-xs text-slate-500">{e.reason}</p>
                    )}
                  </div>
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={() => handleDelete(e.date)}
                  >
                    <Trash2 className="h-4 w-4 text-red-500" />
                  </Button>
                </div>
              ))
            )}
          </div>
        </>
      )}
    </div>
  )
}
```

- [ ] **Step 2: Verificar tipos**

```bash
cd frontend && npx tsc --noEmit
```

Expected: `0 errors`

- [ ] **Step 3: Commit**

```bash
git add frontend/components/availability/ExceptionsEditor.tsx
git commit -m "feat: add ExceptionsEditor component"
```

---

## Task 11: Página e sidebar

**Files:**
- Create: `frontend/app/(admin)/admin/disponibilidade/page.tsx`
- Modify: `frontend/components/admin/Sidebar.tsx`

- [ ] **Step 1: Criar a página**

Criar `frontend/app/(admin)/admin/disponibilidade/page.tsx`:

```tsx
'use client'

import { useEffect, useState } from 'react'
import { resourcesApi } from '@/lib/api/resources'
import type { Resource } from '@/lib/types/resource'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { BusinessHoursEditor } from '@/components/availability/BusinessHoursEditor'
import { ResourceRulesEditor } from '@/components/availability/ResourceRulesEditor'
import { ExceptionsEditor } from '@/components/availability/ExceptionsEditor'

export default function DisponibilidadePage() {
  const [resources, setResources] = useState<Resource[]>([])

  useEffect(() => {
    resourcesApi.list().then(setResources)
  }, [])

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Disponibilidade</h1>

      <Tabs defaultValue="horarios">
        <TabsList>
          <TabsTrigger value="horarios">Horários Globais</TabsTrigger>
          <TabsTrigger value="grade">Grade por Recurso</TabsTrigger>
          <TabsTrigger value="excecoes">Exceções</TabsTrigger>
        </TabsList>

        <TabsContent value="horarios" className="mt-6">
          <p className="text-sm text-slate-500 mb-6">
            Horários padrão de funcionamento do negócio (se aplica a todos os
            recursos sem grade própria).
          </p>
          <BusinessHoursEditor />
        </TabsContent>

        <TabsContent value="grade" className="mt-6">
          <p className="text-sm text-slate-500 mb-6">
            Grade semanal de cada profissional ou recurso. Quando configurada,
            substitui os horários globais para aquele recurso.
          </p>
          <ResourceRulesEditor resources={resources} />
        </TabsContent>

        <TabsContent value="excecoes" className="mt-6">
          <p className="text-sm text-slate-500 mb-6">
            Bloqueie datas específicas ou defina horários alternativos para um
            dia (folgas, feriados, manutenção).
          </p>
          <ExceptionsEditor resources={resources} />
        </TabsContent>
      </Tabs>
    </div>
  )
}
```

- [ ] **Step 2: Adicionar o link na sidebar**

Em `frontend/components/admin/Sidebar.tsx`, adicionar o import de `Clock` e a entrada de navegação.

Substituir a linha de import dos ícones:

```tsx
import {
  LayoutDashboard, CalendarDays, ClipboardList, Users,
  Scissors, Briefcase, DollarSign, Bell, Settings, Rocket, Wallet2
} from 'lucide-react'
```

por:

```tsx
import {
  LayoutDashboard, CalendarDays, ClipboardList, Users,
  Scissors, Briefcase, DollarSign, Bell, Settings, Rocket, Wallet2, Clock
} from 'lucide-react'
```

E no array `NAV`, adicionar a entrada de Disponibilidade logo após Recursos:

```tsx
{ href: '/admin/recursos',       label: 'Recursos',        icon: Briefcase },
{ href: '/admin/disponibilidade', label: 'Disponibilidade', icon: Clock },
```

- [ ] **Step 3: Verificar tipos**

```bash
cd frontend && npx tsc --noEmit
```

Expected: `0 errors`

- [ ] **Step 4: Commit**

```bash
git add frontend/app/(admin)/admin/disponibilidade/page.tsx
git add frontend/components/admin/Sidebar.tsx
git commit -m "feat: add /admin/disponibilidade page with business hours, resource rules and exceptions tabs"
```

---

## Checklist de cobertura da spec

- [x] Horários de funcionamento globais: GET (Task 2, 6, 8) + PUT existente, UI (Task 8, 11)
- [x] Grade por recurso: GET (Task 3, 6, 9) + PUT existente, UI (Task 9, 11)
- [x] Exceções por recurso: GET (Task 4, 6, 10) + PUT existente + DELETE (Task 5, 6, 10), UI (Task 10, 11)
- [x] Link na sidebar (Task 11)
- [x] Testes unitários para todos os handlers novos (Tasks 2–5)
