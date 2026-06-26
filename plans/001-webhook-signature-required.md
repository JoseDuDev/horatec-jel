# Plan 001: Tornar validação de assinatura de webhook obrigatória

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md`.
>
> **Drift check (run first)**: `git diff --stat 8f8cad2..HEAD -- src/Horafy.API/Controllers/V1/WebhooksController.cs src/Horafy.Infrastructure/Gateways/MercadoPagoPaymentGateway.cs`
> Se qualquer arquivo in-scope mudou, compare os excerpts de "Current state" com o código atual antes de prosseguir.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: security
- **Planned at**: commit `8f8cad2`, 2026-06-26

## Why this matters

O webhook do Mercado Pago tem dois buracos que se combinam: (1) em `WebhooksController.cs:32-33`, a validação de assinatura só é executada `if (!string.IsNullOrEmpty(xSignature))` — se o atacante enviar o header ausente ou vazio, o `if` inteiro é ignorado e o pagamento é processado sem qualquer verificação; (2) em `MercadoPagoPaymentGateway.cs:108`, se `WebhookSecret` estiver vazio (como está em `appsettings.Development.json`), o método retorna `true` incondicionalmente. Isso significa que um atacante pode enviar um POST falso ao endpoint de webhook sem header de assinatura e confirmar um pagamento que nunca ocorreu.

## Current state

**Arquivo 1**: `src/Horafy.API/Controllers/V1/WebhooksController.cs`
- Papel: recebe eventos do Mercado Pago e despacha `ConfirmPaymentCommand`

```csharp
// WebhooksController.cs:30-38
if (payload?.Type == "payment" && payload.Data?.Id is { } mpPaymentId)
{
    if (!string.IsNullOrEmpty(xSignature)          // ← PROBLEMA: se vazio, bloco inteiro pulado
        && !gateway.ValidateWebhookSignature(mpPaymentId, xRequestId, xSignature))
        return Unauthorized();

    var result = await Sender.Send(new ConfirmPaymentCommand(mpPaymentId), cancellationToken);
    return result.IsSuccess ? Ok() : BadRequest(result.Error.Description);
}
```

**Arquivo 2**: `src/Horafy.Infrastructure/Gateways/MercadoPagoPaymentGateway.cs`
- Papel: implementação do gateway de pagamento

```csharp
// MercadoPagoPaymentGateway.cs:106-108
public bool ValidateWebhookSignature(string mpPaymentId, string requestId, string xSignature)
{
    if (string.IsNullOrEmpty(_opts.WebhookSecret)) return true; // dev mode  ← PROBLEMA: em produção, se não configurado, aceita tudo
```

**Padrão de erros do projeto**: Erros de autenticação retornam `Unauthorized()` via `ToActionResult`. Controllers base em `src/Horafy.API/Controllers/Base/ApiControllerBase.cs`.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Build | `dotnet build src/Horafy.API` | exit 0 |
| Tests .NET | `dotnet test tests/` | All tests pass |
| Lint/Check | `dotnet build --no-incremental 2>&1` | 0 warnings, exit 0 |

## Scope

**In scope** (os únicos arquivos a modificar):
- `src/Horafy.API/Controllers/V1/WebhooksController.cs`
- `src/Horafy.Infrastructure/Gateways/MercadoPagoPaymentGateway.cs`
- `tests/Horafy.Application.Tests/Payments/` — adicionar `WebhookSignatureTests.cs`

**Out of scope** (não tocar):
- `ConfirmPaymentCommand.cs` — lógica de pagamento não muda
- `appsettings.*.json` — a presença/ausência de `WebhookSecret` não é alterada aqui; o fix faz o código rejeitar quando ausente
- Nenhuma mudança na interface `IPaymentGateway`

## Git workflow

- Branch: `advisor/001-webhook-signature-required`
- Commit style (conforme git log): mensagem curta em português, ex: `Tornar validação de assinatura de webhook obrigatória`
- Não fazer push nem abrir PR a menos que instruído

## Steps

### Step 1: Corrigir `WebhooksController` para exigir sempre a assinatura

Em `src/Horafy.API/Controllers/V1/WebhooksController.cs`, **substituir** a lógica condicional que pula a validação quando `xSignature` está vazio:

**Código atual (remover)**:
```csharp
if (!string.IsNullOrEmpty(xSignature)
    && !gateway.ValidateWebhookSignature(mpPaymentId, xRequestId, xSignature))
    return Unauthorized();
```

**Código novo (inserir no lugar)**:
```csharp
if (!gateway.ValidateWebhookSignature(mpPaymentId, xRequestId, xSignature))
    return Unauthorized();
```

A validação agora é sempre executada. `ValidateWebhookSignature` recebe `xSignature` vazio e retornará `false` (após o Step 2), resultando em `401`.

**Verify**: `dotnet build src/Horafy.API` → exit 0

### Step 2: Corrigir `MercadoPagoPaymentGateway` para rejeitar quando `WebhookSecret` não configurado

Em `src/Horafy.Infrastructure/Gateways/MercadoPagoPaymentGateway.cs`, **substituir** o retorno `true` em dev mode:

**Código atual (remover)**:
```csharp
if (string.IsNullOrEmpty(_opts.WebhookSecret)) return true; // dev mode
```

**Código novo (inserir no lugar)**:
```csharp
if (string.IsNullOrEmpty(_opts.WebhookSecret)) return false;
```

Quando `WebhookSecret` não estiver configurado, qualquer webhook é rejeitado com `401`. Para desenvolvimento local sem Mercado Pago real, use `PAYMENT_GATEWAY=fake` (já suportado em `DependencyInjection.cs:137`) — o `FakePaymentGateway` não expõe endpoint de webhook.

**Verify**: `dotnet build src/Horafy.Infrastructure` → exit 0

### Step 3: Adicionar testes de regressão

Criar `tests/Horafy.Application.Tests/Payments/WebhookSignatureTests.cs`. Use o padrão dos testes existentes em `tests/Horafy.Application.Tests/Payments/` (Mock de repositório, sem DbContext real).

Casos a testar (usando `MercadoPagoPaymentGateway` diretamente ou mock de `IPaymentGateway`):
1. `xSignature` vazio → `ValidateWebhookSignature` retorna `false`
2. `WebhookSecret` vazio → `ValidateWebhookSignature` retorna `false`
3. `xSignature` válido + `WebhookSecret` correto → retorna `true`
4. `xSignature` adulterado → retorna `false`

Para o `WebhooksController`, criar um teste de integração leve (sem `WebApplicationFactory`; pode ser um teste de unidade do controller passando mock de `IPaymentGateway`):
- Mock retornando `false` → controller retorna `Unauthorized`
- Mock retornando `true` → controller despacha `ConfirmPaymentCommand`

**Verify**: `dotnet test tests/ --filter "WebhookSignature"` → todos os novos testes passam

### Step 4: Verificação final completa

**Verify**: `dotnet test tests/` → todos os testes passam (incluindo os existentes)

## Test plan

- Novo arquivo: `tests/Horafy.Application.Tests/Payments/WebhookSignatureTests.cs`
- Casos: (1) empty signature → false, (2) empty secret → false, (3) valid → true, (4) tampered → false
- Pattern: seguir `tests/Horafy.Application.Tests/Payments/ConfirmPaymentCommandHandlerTests.cs`
- Verification: `dotnet test tests/ --filter "Webhook"` → 4+ novos testes passam

## Done criteria

- [ ] `dotnet build src/` exits 0
- [ ] `dotnet test tests/` exits 0; pelo menos 4 novos testes de assinatura existem e passam
- [ ] `grep -n "IsNullOrEmpty(xSignature)" src/Horafy.API/Controllers/V1/WebhooksController.cs` retorna 0 matches
- [ ] `grep -n "return true; // dev mode" src/Horafy.Infrastructure/Gateways/MercadoPagoPaymentGateway.cs` retorna 0 matches
- [ ] `plans/README.md` atualizado

## STOP conditions

- O código em `WebhooksController.cs:30-38` não corresponde ao excerpt (drift — compare e reporte)
- Um step de verify falha duas vezes após tentativa razoável de fix
- O fix requer tocar `ConfirmPaymentCommand.cs` ou `IPaymentGateway.cs` (escopo expandido — reporte)
- `FakePaymentGateway` também tem `ValidateWebhookSignature` retornando `true` sem verificação — se encontrar, inclua o fix no Step 2 mas reporte a descoberta

## Maintenance notes

- Em desenvolvimento local, configure `PAYMENT_GATEWAY=fake` em `.env` para desabilitar o gateway real — isso contorna a necessidade de `WebhookSecret` local
- Quando Mercado Pago for configurado em staging/produção, `MercadoPago__WebhookSecret` DEVE ser definido como env var; o endpoint rejeitará todos os webhooks caso contrário
- Revisor: conferir que `FakePaymentGateway.ValidateWebhookSignature` também retorna `false` quando secret vazio (caso implemente a interface)
