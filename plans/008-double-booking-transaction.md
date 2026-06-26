# Plan 008: Proteger CreateBookingCommand com transação Serializable

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md`.
>
> **Drift check (run first)**: `git diff --stat 8f8cad2..HEAD -- src/Horafy.Application/Features/Bookings/Commands/CreateBookingCommand.cs src/Horafy.Application/Features/Bookings/Commands/AdminCreateBookingCommand.cs`
> Se qualquer arquivo in-scope mudou, compare os excerpts antes de prosseguir.

## Status

- **Priority**: P1
- **Effort**: M
- **Risk**: MED
- **Depends on**: none
- **Category**: bug
- **Planned at**: commit `8f8cad2`, 2026-06-26

## Why this matters

`CreateBookingCommand` verifica conflito de horário (`HasConflictAsync`) e depois cria o booking em dois passos separados sem transação. Com dois clientes tentando o mesmo horário simultaneamente, ambos passam pela verificação antes de qualquer um persistir — resultando em double booking. `CreateRecurringBookingCommand` (arquivo homólogo) já usa corretamente `ExecuteInTransactionAsync` com `IsolationLevel.Serializable`, conforme comentário inline que explica a necessidade. O mesmo padrão precisa ser aplicado a `CreateBookingCommand` e `AdminCreateBookingCommand`.

## Current state

**Arquivo 1**: `src/Horafy.Application/Features/Bookings/Commands/CreateBookingCommand.cs`
- Papel: handler de criação de booking pelo cliente via portal

```csharp
// CreateBookingCommand.cs:62-82 — check-then-act sem transação
var hasConflict = await bookingRepository.HasConflictAsync(
    request.ResourceId, request.ScheduledAt, endsAt,
    cancellationToken: cancellationToken);

if (hasConflict) return Result.Failure<Guid>(BookingErrors.Conflict);

var booking = Booking.Create(
    services, request.ResourceId, resource.Name,
    customerId:    currentUser.UserId.Value,
    customerName:  currentUser.Email ?? "Cliente",
    customerEmail: currentUser.Email ?? string.Empty,
    scheduledAt:   request.ScheduledAt,
    customerPhone: customerPhone,
    notes:         request.Notes);

bookingRepository.Add(booking);
await unitOfWork.SaveChangesAsync(cancellationToken);

return Result.Success(booking.Id);
```

**Arquivo 2**: `src/Horafy.Application/Features/Bookings/Commands/CreateRecurringBookingCommand.cs`
- Papel: handler que JÁ usa o padrão correto — usar como referência

```csharp
// CreateRecurringBookingCommand.cs:58 — PADRÃO CORRETO a replicar
return await unitOfWork.ExecuteInTransactionAsync(async ct =>
{
    foreach (var date in occurrences)
    {
        var hasConflict = await bookingRepository.HasConflictAsync(...);
        if (hasConflict) return Result.Failure<Guid>(BookingErrors.Conflict);
        // ...
        bookingRepository.Add(booking);
    }
    await unitOfWork.SaveChangesAsync(ct);
    return Result.Success(recurrenceGroupId);
}, IsolationLevel.Serializable, cancellationToken);
```

**Interface**: `ITenantUnitOfWork.ExecuteInTransactionAsync<T>` recebe `Func<CancellationToken, Task<T>>` + `IsolationLevel` + `CancellationToken`.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Build | `dotnet build src/` | exit 0 |
| Tests | `dotnet test tests/` | All pass |

## Scope

**In scope**:
- `src/Horafy.Application/Features/Bookings/Commands/CreateBookingCommand.cs`
- `src/Horafy.Application/Features/Bookings/Commands/AdminCreateBookingCommand.cs`

**Out of scope**:
- `CreateRecurringBookingCommand.cs` — já está correto
- `CreateIntegrationBookingCommand.cs` — similar race condition mas escopo separado
- `ConfirmPaymentCommand.cs` — race condition diferente (Plan 009)
- Mudanças no banco de dados (índices, constraints) — não necessário; a transação Serializable detecta conflitos

## Git workflow

- Branch: `advisor/008-double-booking-transaction`
- Commit: `Proteger CreateBookingCommand com transação Serializable`
- Não fazer push nem abrir PR a menos que instruído

## Steps

### Step 1: Refatorar CreateBookingCommandHandler para usar ExecuteInTransactionAsync

Em `src/Horafy.Application/Features/Bookings/Commands/CreateBookingCommand.cs`, envolver o bloco de check-then-create em `ExecuteInTransactionAsync` com `IsolationLevel.Serializable`.

A lógica antes da transação (busca de usuário, recurso, serviços, cálculo de duração) pode permanecer fora da transação — são reads que não precisam ser serializados. Apenas o check de conflito e a inserção devem estar dentro.

**Código novo para o handler** (substituindo linhas 62-82):

```csharp
// using System.Data; — adicionar no topo do arquivo se não existir

return await unitOfWork.ExecuteInTransactionAsync(async ct =>
{
    var hasConflict = await bookingRepository.HasConflictAsync(
        request.ResourceId, request.ScheduledAt, endsAt,
        cancellationToken: ct);

    if (hasConflict) return Result.Failure<Guid>(BookingErrors.Conflict);

    var booking = Booking.Create(
        services,
        request.ResourceId,
        resource.Name,
        customerId:    currentUser.UserId.Value,
        customerName:  currentUser.Email ?? "Cliente",
        customerEmail: currentUser.Email ?? string.Empty,
        scheduledAt:   request.ScheduledAt,
        customerPhone: customerPhone,
        notes:         request.Notes);

    bookingRepository.Add(booking);
    await unitOfWork.SaveChangesAsync(ct);

    return Result.Success(booking.Id);
}, IsolationLevel.Serializable, cancellationToken);
```

Adicionar `using System.Data;` no topo do arquivo (se não existir — verificar no arquivo original).

**Verify**: `dotnet build src/Horafy.Application` → exit 0

### Step 2: Aplicar o mesmo padrão em AdminCreateBookingCommand

Ler `src/Horafy.Application/Features/Bookings/Commands/AdminCreateBookingCommand.cs` e localizar o trecho equivalente (check de conflito + Add + SaveChanges). Envolver no mesmo padrão `ExecuteInTransactionAsync(Serializable)`.

A assinatura exata depende do código atual do arquivo — leia antes de modificar.

**Verify**: `dotnet build src/Horafy.Application` → exit 0

### Step 3: Verificação final com todos os testes

**Verify**: `dotnet test tests/` → todos os testes passam (incluindo os de Bookings)

## Test plan

Verificar testes existentes em `tests/Horafy.Application.Tests/Bookings/`. Se existir `CreateBookingCommandHandlerTests.cs`:
- Confirmar que os testes ainda passam (a mudança não altera o comportamento externo — apenas adiciona isolamento de transação)
- Se os mocks de `ITenantUnitOfWork` não suportam `ExecuteInTransactionAsync`, atualizar os mocks para implementar a função executando a operação diretamente: `operation(cancellationToken)`

Adicionar teste de concorrência se `tests/Horafy.Application.Tests/Rentals/RentalStockConcurrencyTests.cs` existir como modelo — adaptar para bookings regulares.

## Done criteria

- [ ] `dotnet build src/` exits 0
- [ ] `dotnet test tests/` exits 0
- [ ] `grep -n "ExecuteInTransactionAsync" src/Horafy.Application/Features/Bookings/Commands/CreateBookingCommand.cs` retorna match com `Serializable`
- [ ] `grep -n "ExecuteInTransactionAsync" src/Horafy.Application/Features/Bookings/Commands/AdminCreateBookingCommand.cs` retorna match com `Serializable`
- [ ] `grep -n "await unitOfWork.SaveChangesAsync" src/Horafy.Application/Features/Bookings/Commands/CreateBookingCommand.cs` — não deve aparecer fora da lambda (verificar que não está duplicado)
- [ ] `plans/README.md` atualizado

## STOP conditions

- `ITenantUnitOfWork.ExecuteInTransactionAsync` não existe (drift desde o audit) — STOP e reporte
- A interface retorna `Task<T>` mas o handler retorna `Result<Guid>` sem `<T>` — adaptar o tipo genérico
- Testes do handler quebram com `NullReferenceException` no mock de `ExecuteInTransactionAsync` — o mock precisa invocar a função callback: `mock.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<...>(), ...)).Returns((Func<CancellationToken, Task<Result<Guid>>> op, ...) => op(CancellationToken.None))`
- O handler usa variáveis capturadas fora da lambda que são mutadas dentro — reportar se encontrar; pode ser um bug de closure

## Maintenance notes

- A operação dentro de `ExecuteInTransactionAsync` pode ser re-executada pelo Npgsql em caso de serialization failure (conforme comentário em `CreateRecurringBookingCommand`). Garantir que a lógica dentro da lambda seja idempotente — `Booking.Create()` gera `Guid.NewGuid()` internamente; se isso causar IDs diferentes em retries, verificar se há constraint de unique em booking
- Se `Booking.Create()` gera o ID internamente, um retry gerará um ID diferente — isso é correto (o booking anterior falhou; um novo é criado). O client receberá o ID do booking bem-sucedido
- Revisor: o `customerPhone` é capturado fora da transação — se o usuário mudar o telefone entre a captura e o retry, o booking pode ter telefone desatualizado. Aceitável para este plano
