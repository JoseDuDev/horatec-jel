# Plan 009: Corrigir race condition em ConfirmPaymentCommand (webhook duplo)

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md`.
>
> **Drift check (run first)**: `git diff --stat 8f8cad2..HEAD -- src/Horafy.Application/Features/Payments/Commands/ConfirmPaymentCommand.cs src/Horafy.Domain/Entities/Payments/Payment.cs`
> Se qualquer arquivo in-scope mudou, compare os excerpts antes de prosseguir.

## Status

- **Priority**: P1
- **Effort**: M
- **Risk**: MED
- **Depends on**: plans/001-webhook-signature-required.md
- **Category**: bug
- **Planned at**: commit `8f8cad2`, 2026-06-26

## Why this matters

O Mercado Pago pode enviar o mesmo evento de pagamento mais de uma vez (retry de entrega). `ConfirmPaymentCommand` tem verificação de idempotência na linha 19-20 (`GetByMpPaymentIdAsync`), mas ela verifica se `MpPaymentId` já está preenchido na entidade. O problema: `MpPaymentId` só é preenchido após `payment.Approve()` + `SaveChanges`. Com dois webhooks chegando simultaneamente, ambos passam pelo check inicial (ambos encontram `existing == null`), buscam o status no MP, ambos chamam `payment.Approve()` — gerando o evento de domínio `PaymentApproved` duas vezes. O resultado é que notificações de confirmação são enviadas duas vezes ao cliente e a wallet pode receber créditos de fidelidade em duplicata.

## Current state

**Arquivo 1**: `src/Horafy.Application/Features/Payments/Commands/ConfirmPaymentCommand.cs`
- Papel: handler que processa confirmação de pagamento do MP

```csharp
// ConfirmPaymentCommand.cs:17-34
public async Task<Result> Handle(ConfirmPaymentCommand request, CancellationToken cancellationToken)
{
    // Idempotency: if MpPaymentId already processed, return success
    var existing = await paymentRepository.GetByMpPaymentIdAsync(request.MpPaymentId, cancellationToken);
    if (existing is not null) return Result.Success();                          // linha 20

    var mpStatus = await gateway.GetPaymentStatusAsync(request.MpPaymentId, cancellationToken);

    var payment = await paymentRepository.GetByPreferenceIdAsync(mpStatus.PreferenceId, cancellationToken);
    if (payment is null) return Result.Failure(PaymentErrors.NotFound);

    if (mpStatus.Status == PaymentStatus.Approved)
        payment.Approve(request.MpPaymentId);                                   // linha 28
    else if (mpStatus.Status is PaymentStatus.Rejected or PaymentStatus.Cancelled)
        payment.Reject(request.MpPaymentId);

    paymentRepository.Update(payment);
    await unitOfWork.SaveChangesAsync(cancellationToken);                       // linha 32
    return Result.Success();
}
```

**Arquivo 2**: `src/Horafy.Domain/Entities/Payments/Payment.cs`
- Papel: entidade de domínio — ler o método `Approve()` para entender os guards

Ler antes de implementar. Verificar se `Payment.Approve()` tem guard de idempotência (ex.: `if (Status == Approved) return`).

**Interface**: `ITenantUnitOfWork.ExecuteInTransactionAsync` — já usada em `CreateRecurringBookingCommand` (Pattern idêntico).

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Build | `dotnet build src/` | exit 0 |
| Tests | `dotnet test tests/` | All pass |

## Scope

**In scope**:
- `src/Horafy.Application/Features/Payments/Commands/ConfirmPaymentCommand.cs`
- `src/Horafy.Domain/Entities/Payments/Payment.cs` — adicionar guard em `Approve()` se não existir

**Out of scope**:
- `WebhooksController.cs` — não modificar (Plan 001 cuida da validação de entrada)
- `PaymentRepository` — não modificar
- Outros commands de pagamento (CancelBookingCommand, RefundPaymentCommand) — escopo separado

## Git workflow

- Branch: `advisor/009-webhook-payment-race`
- Commit: `Corrigir race condition em ConfirmPaymentCommand com transação Serializable`
- Não fazer push nem abrir PR a menos que instruído

## Steps

### Step 1: Ler Payment.cs e verificar os guards existentes em Approve()

Ler `src/Horafy.Domain/Entities/Payments/Payment.cs`. Identificar:
- Existe guard `if (Status == PaymentStatus.Approved) return;` em `Approve()`?
- O campo `MpPaymentId` é definido em `Approve()`?
- Há campo de status que pode ser usado como lock otimista?

Se `Approve()` **não** tiver guard de idempotência, adicionar:
```csharp
public void Approve(string mpPaymentId)
{
    if (Status == PaymentStatus.Approved) return;  // Adicionar se não existir
    // resto do método
}
```

**Verify**: `dotnet build src/Horafy.Domain` → exit 0

### Step 2: Envolver ConfirmPaymentCommand em transação Serializable

Em `src/Horafy.Application/Features/Payments/Commands/ConfirmPaymentCommand.cs`, mover o bloco de check + fetch + approve para dentro de `ExecuteInTransactionAsync(Serializable)`.

**Nota importante**: A chamada `gateway.GetPaymentStatusAsync` é uma requisição HTTP externa — **não deve estar dentro da transação** (transações longas com I/O externo são problemáticas). Mover o gateway call para ANTES da transação:

```csharp
public async Task<Result> Handle(ConfirmPaymentCommand request, CancellationToken cancellationToken)
{
    // Verificação rápida de idempotência fora da transação (otimização — pode ter falso negativo em race)
    var existing = await paymentRepository.GetByMpPaymentIdAsync(request.MpPaymentId, cancellationToken);
    if (existing is not null) return Result.Success();

    // Busca status no gateway ANTES da transação (operação I/O externa)
    var mpStatus = await gateway.GetPaymentStatusAsync(request.MpPaymentId, cancellationToken);

    // Transação Serializable para o check-then-update atômico
    return await unitOfWork.ExecuteInTransactionAsync(async ct =>
    {
        // Re-verificar dentro da transação (garante que outro thread não processou entre o check acima e agora)
        var existingInTx = await paymentRepository.GetByMpPaymentIdAsync(request.MpPaymentId, ct);
        if (existingInTx is not null) return Result.Success();

        var payment = await paymentRepository.GetByPreferenceIdAsync(mpStatus.PreferenceId, ct);
        if (payment is null) return Result.Failure(PaymentErrors.NotFound);

        if (mpStatus.Status == PaymentStatus.Approved)
            payment.Approve(request.MpPaymentId);
        else if (mpStatus.Status is PaymentStatus.Rejected or PaymentStatus.Cancelled)
            payment.Reject(request.MpPaymentId);

        paymentRepository.Update(payment);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }, IsolationLevel.Serializable, cancellationToken);
}
```

Adicionar `using System.Data;` no topo se não existir.

**Verify**: `dotnet build src/Horafy.Application` → exit 0

### Step 3: Verificação completa

**Verify**: `dotnet test tests/` → todos os testes passam

## Test plan

Verificar `tests/Horafy.Application.Tests/Payments/ConfirmPaymentCommandHandlerTests.cs`:
- Atualizar mocks de `ITenantUnitOfWork` para invocar o callback (pattern idêntico ao Plan 008)
- Verificar que testes existentes passam
- Adicionar teste: chamar handler duas vezes com mesmo `MpPaymentId` → segunda chamada retorna `Result.Success()` sem chamar `payment.Approve()` novamente

## Done criteria

- [ ] `dotnet build src/` exits 0
- [ ] `dotnet test tests/` exits 0
- [ ] `grep -n "ExecuteInTransactionAsync" src/Horafy.Application/Features/Payments/Commands/ConfirmPaymentCommand.cs` retorna match com `Serializable`
- [ ] `Payment.Approve()` tem guard de idempotência (verificar com grep ou read)
- [ ] `plans/README.md` atualizado

## STOP conditions

- `Payment.Approve()` tem efeitos colaterais além de setar `MpPaymentId` e `Status` — listar todos os efeitos antes de adicionar guard; o guard pode precisar ser mais específico
- `paymentRepository.GetByPreferenceIdAsync` usa tracking (não `AsNoTracking`) — confirmar que o Update posterior funciona corretamente dentro da transação; se não, converter para Update por ID
- O handler tem mais de uma chamada ao gateway (ex.: `RefundAsync`) — STOP e redesenhar (gateway calls dentro de transação são proibidas)

## Maintenance notes

- O double-check de idempotência (fora + dentro da transação) é o padrão correto: a verificação externa é uma otimização para evitar abrir uma transação na maioria dos casos; a interna é a garantia de segurança
- Se o MP continuar enviando duplicatas e causar contenção no banco, adicionar um índice único em `mp_payment_id` como segunda linha de defesa (geraria constraint violation em vez de duplicata silenciosa)
- Revisor: garantir que `unitOfWork` aqui é o `ITenantUnitOfWork` (schema correto) e não o `IUnitOfWork` do schema público
