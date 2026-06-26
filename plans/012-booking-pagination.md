# Plan 012: Adicionar paginação em GetByCustomerAsync e endpoints de bookings do cliente

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md`.
>
> **Drift check (run first)**: `git diff --stat 8f8cad2..HEAD -- src/Horafy.Infrastructure/Repositories/BookingRepository.cs src/Horafy.Domain/Interfaces/Repositories/IBookingRepository.cs src/Horafy.Application/Features/Bookings/Queries/`
> Se qualquer arquivo in-scope mudou, compare os excerpts antes de prosseguir.

## Status

- **Priority**: P2
- **Effort**: M
- **Risk**: MED
- **Depends on**: none
- **Category**: perf
- **Planned at**: commit `8f8cad2`, 2026-06-26

## Why this matters

`BookingRepository.GetByCustomerAsync` carrega todos os bookings de um cliente sem limite. Um cliente com histórico de 1.000+ agendamentos causa uma query que traz todas as linhas + todos os Services (via `Include(b => b.Services)`), potencialmente centenas de MB de dados por requisição. Em ambiente multi-tenant com muitos clientes ativos simultaneamente, isso pode causar OOM e timeouts. O portal público exibe os bookings em uma lista — não existe razão para carregar 5 anos de histórico de uma só vez.

## Current state

**Arquivo 1**: `src/Horafy.Infrastructure/Repositories/BookingRepository.cs`
- Papel: repositório de bookings para o schema do tenant

```csharp
// BookingRepository.cs:35-43 — sem paginação
public async Task<IReadOnlyList<Booking>> GetByCustomerAsync(
    Guid customerId,
    CancellationToken cancellationToken = default) =>
    await DbSet
        .AsNoTracking()
        .Include(b => b.Services)
        .Where(b => b.CustomerId == customerId)
        .OrderByDescending(b => b.ScheduledAt)
        .ToListAsync(cancellationToken);
```

**Arquivo 2**: `src/Horafy.Domain/Interfaces/Repositories/IBookingRepository.cs`
- Papel: interface do repositório — ler para ver a assinatura exata de `GetByCustomerAsync`

**Arquivo 3**: `src/Horafy.Application/Features/Bookings/Queries/` — ler os arquivos para identificar onde `GetByCustomerAsync` é chamado

**Padrão de paginação do projeto**: Verificar se há um tipo `PaginatedResult<T>` em `src/Horafy.Shared/` ou similar. Se existir, usar. Se não existir, criar um simples.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Build | `dotnet build src/` | exit 0 |
| Tests | `dotnet test tests/` | All pass |

## Scope

**In scope**:
- `src/Horafy.Domain/Interfaces/Repositories/IBookingRepository.cs` — atualizar assinatura de `GetByCustomerAsync`
- `src/Horafy.Infrastructure/Repositories/BookingRepository.cs` — implementar paginação
- Query handler(s) que chamam `GetByCustomerAsync` — atualizar chamadas
- `src/Horafy.Shared/` — adicionar `PaginatedResult<T>` se não existir

**Out of scope**:
- `GetByResourceAsync` — essa query já é filtrada por intervalo de datas; não paginar agora
- `GetBookingsQuery` (admin) — escopo separado
- Mudanças no schema do banco de dados — não necessário para paginação

## Git workflow

- Branch: `advisor/012-booking-pagination`
- Commit: `Adicionar paginação em GetByCustomerAsync`
- Não fazer push nem abrir PR a menos que instruído

## Steps

### Step 1: Verificar callers de GetByCustomerAsync e tipo PaginatedResult

Antes de qualquer mudança:
1. Buscar todos os usos: `grep -rn "GetByCustomerAsync" src/`
2. Ler cada caller para entender o contexto de uso
3. Verificar se `PaginatedResult<T>` ou similar existe em `src/Horafy.Shared/`

Se o tipo não existir, criar `src/Horafy.Shared/PaginatedResult.cs`:

```csharp
namespace Horafy.Shared;

public sealed record PaginatedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
```

**Verify**: `dotnet build src/Horafy.Shared` → exit 0

### Step 2: Atualizar a interface IBookingRepository

Em `src/Horafy.Domain/Interfaces/Repositories/IBookingRepository.cs`, alterar a assinatura de `GetByCustomerAsync`:

**Código atual**:
```csharp
Task<IReadOnlyList<Booking>> GetByCustomerAsync(
    Guid customerId,
    CancellationToken cancellationToken = default);
```

**Código novo**:
```csharp
Task<PaginatedResult<Booking>> GetByCustomerAsync(
    Guid customerId,
    int page = 1,
    int pageSize = 20,
    CancellationToken cancellationToken = default);
```

O `pageSize` padrão de 20 é suficiente para o portal que exibe uma lista de histórico. Parâmetros têm defaults para manter callers existentes funcionando (apenas precisam ser atualizados, não reescritos).

Adicionar `using Horafy.Shared;` no topo se necessário.

**Verify**: `dotnet build src/Horafy.Domain` → exit 0 (vai falhar na Infrastructure — normal)

### Step 3: Implementar paginação em BookingRepository

Em `src/Horafy.Infrastructure/Repositories/BookingRepository.cs`, atualizar a implementação:

```csharp
public async Task<PaginatedResult<Booking>> GetByCustomerAsync(
    Guid customerId,
    int page = 1,
    int pageSize = 20,
    CancellationToken cancellationToken = default)
{
    var query = DbSet
        .AsNoTracking()
        .Include(b => b.Services)
        .Where(b => b.CustomerId == customerId);

    var totalCount = await query.CountAsync(cancellationToken);

    var items = await query
        .OrderByDescending(b => b.ScheduledAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(cancellationToken);

    return new PaginatedResult<Booking>(items, totalCount, page, pageSize);
}
```

Adicionar `using Horafy.Shared;` e `using Microsoft.EntityFrameworkCore;` no topo se necessário.

**Verify**: `dotnet build src/Horafy.Infrastructure` → exit 0

### Step 4: Atualizar todos os callers

Para cada caller encontrado no Step 1, atualizar para:
- Aceitar o novo tipo `PaginatedResult<Booking>`
- Passar `page` e `pageSize` (da query/request se disponível, ou usar defaults)
- Retornar `PaginatedResult` no response DTO se o endpoint é paginado, ou extrair `Items` se o caller usa apenas a lista

Exemplo típico de query handler:

```csharp
// GetCustomerBookingsQuery.cs
public sealed record GetCustomerBookingsQuery(int Page = 1, int PageSize = 20) : IRequest<Result<PaginatedResult<BookingDto>>>;

// Handler
var result = await bookingRepository.GetByCustomerAsync(
    currentUser.UserId.Value,
    request.Page,
    request.PageSize,
    cancellationToken);

return Result.Success(new PaginatedResult<BookingDto>(
    result.Items.Select(b => new BookingDto(...)).ToList(),
    result.TotalCount,
    result.Page,
    result.PageSize));
```

**Verify**: `dotnet build src/` → exit 0

### Step 5: Atualizar testes

Para cada teste que usa `GetByCustomerAsync`, atualizar o mock para retornar `PaginatedResult<Booking>` ao invés de `IReadOnlyList<Booking>`:

```csharp
// Exemplo de mock atualizado:
mockRepo.Setup(r => r.GetByCustomerAsync(
    It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(new PaginatedResult<Booking>(
        new List<Booking> { booking1, booking2 },
        totalCount: 2, page: 1, pageSize: 20));
```

**Verify**: `dotnet test tests/` → todos os testes passam

## Test plan

- Atualizar testes existentes que usam `GetByCustomerAsync`
- Adicionar teste: chamar com `page=2, pageSize=1` em 2 bookings → retorna Items.Count=1, TotalCount=2, HasNextPage=false
- Adicionar teste: `page=1, pageSize=1` em 2 bookings → Items.Count=1, TotalCount=2, HasNextPage=true

## Done criteria

- [ ] `dotnet build src/` exits 0
- [ ] `dotnet test tests/` exits 0
- [ ] `grep -n "ToListAsync" src/Horafy.Infrastructure/Repositories/BookingRepository.cs` na linha de `GetByCustomerAsync` — deve usar `Skip/Take` antes de `ToListAsync`
- [ ] `grep -n "GetByCustomerAsync" src/` — todos os callers foram atualizados para `PaginatedResult`
- [ ] `plans/README.md` atualizado

## STOP conditions

- `GetByCustomerAsync` é chamado em mais de 5 places diferentes — listar todos antes de modificar a interface, pois o impacto é maior que o esperado
- O frontend espera a lista inteira (sem paginação) e teria que ser atualizado — STOP e reporte; coordenar com Plan 007 ou criar um plan de frontend separado
- `PaginatedResult<T>` já existe mas com assinatura diferente — adaptar para o tipo existente ao invés de criar novo

## Maintenance notes

- O frontend portal (`frontend/app/(portal)/[slug]/minha-conta/`) precisará ser atualizado para suportar "Carregar mais" ou paginação numérica — este plano cobre apenas o backend; criar issue separada para o frontend
- Se o `pageSize` padrão de 20 gerar percepção de "histórico incompleto" no portal, aumentar para 50 — mas nunca remover o limite
- `CountAsync` antes de `ToListAsync` adiciona uma query extra ao banco — se performance for crítica, usar `ToListAsync` + `Count()` após, ou paginar sem total count (retornar apenas `HasNextPage`)
