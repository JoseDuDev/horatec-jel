# Plan 002: Adicionar timeout aos HTTP clients de gateways externos

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md`.
>
> **Drift check (run first)**: `git diff --stat 8f8cad2..HEAD -- src/Horafy.Infrastructure/DependencyInjection.cs`
> Se o arquivo mudou, compare os excerpts de "Current state" antes de prosseguir.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: reliability
- **Planned at**: commit `8f8cad2`, 2026-06-26

## Why this matters

Os HTTP clients para Mercado Pago e Evolution API (WhatsApp) não têm timeout configurado, herdando o default do `HttpClient` que é 100 segundos. Uma única resposta lenta do gateway externo mantém uma thread do ASP.NET thread pool ocupada por até 100 segundos. Sob carga concorrente — múltiplos agendamentos simultâneos — o pool se esgota e todas as requisições subsequentes falham imediatamente com `TaskCanceledException`. O `apple-jwks` client e o `integration-webhook` client (linha 102 no mesmo arquivo) já têm `TimeSpan.FromSeconds(10)` configurado — o padrão correto já existe no projeto.

## Current state

**Arquivo**: `src/Horafy.Infrastructure/DependencyInjection.cs`
- Papel: registro de todos os serviços de infraestrutura, incluindo HttpClients

```csharp
// DependencyInjection.cs:131-134 — apple-jwks tem timeout (exemplo a seguir)
services.AddHttpClient("apple-jwks", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// DependencyInjection.cs:143-153 — MercadoPago SEM timeout
services.AddHttpClient<IPaymentGateway, MercadoPagoPaymentGateway>(client =>
{
    client.BaseAddress = new Uri("https://api.mercadopago.com");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddHttpMessageHandler(sp =>
{
    var token = configuration["MercadoPago:AccessToken"] ?? string.Empty;
    return new BearerTokenHandler(token);
});

// DependencyInjection.cs:158-165 — EvolutionApi SEM timeout
services.AddHttpClient<IWhatsAppService, EvolutionApiWhatsAppService>(client =>
{
    var baseUrl = configuration[$"{EvolutionApiOptions.SectionName}:BaseUrl"];
    if (!string.IsNullOrEmpty(baseUrl))
        client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("apikey",
        configuration[$"{EvolutionApiOptions.SectionName}:ApiKey"] ?? string.Empty);
});
```

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Build | `dotnet build src/Horafy.Infrastructure` | exit 0 |
| Tests .NET | `dotnet test tests/` | All tests pass |

## Scope

**In scope**:
- `src/Horafy.Infrastructure/DependencyInjection.cs` — apenas as duas configurações de `AddHttpClient`

**Out of scope**:
- `MercadoPagoPaymentGateway.cs` — não adicionar timeout interno via `CancellationTokenSource` (isso é diferente de timeout de cliente HTTP)
- `EvolutionApiWhatsAppService.cs` — não modificar
- Qualquer adição de Polly retry (escopo separado; este plano só adiciona timeout)

## Git workflow

- Branch: `advisor/002-gateway-timeouts`
- Commit: `Adicionar timeout de 15s aos HTTP clients de gateways externos`
- Não fazer push nem abrir PR a menos que instruído

## Steps

### Step 1: Adicionar timeout ao HTTP client do MercadoPago

Em `src/Horafy.Infrastructure/DependencyInjection.cs`, localizar o bloco `AddHttpClient<IPaymentGateway, MercadoPagoPaymentGateway>` (linha 143) e adicionar `client.Timeout`:

**Código atual**:
```csharp
services.AddHttpClient<IPaymentGateway, MercadoPagoPaymentGateway>(client =>
{
    client.BaseAddress = new Uri("https://api.mercadopago.com");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
```

**Código novo**:
```csharp
services.AddHttpClient<IPaymentGateway, MercadoPagoPaymentGateway>(client =>
{
    client.BaseAddress = new Uri("https://api.mercadopago.com");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(15);
})
```

15 segundos é adequado para criação de preferência (normalmente <2s) com margem para picos.

**Verify**: `dotnet build src/Horafy.Infrastructure` → exit 0

### Step 2: Adicionar timeout ao HTTP client da Evolution API

No mesmo arquivo, localizar o bloco `AddHttpClient<IWhatsAppService, EvolutionApiWhatsAppService>` (linha 158) e adicionar `client.Timeout`:

**Código atual**:
```csharp
services.AddHttpClient<IWhatsAppService, EvolutionApiWhatsAppService>(client =>
{
    var baseUrl = configuration[$"{EvolutionApiOptions.SectionName}:BaseUrl"];
    if (!string.IsNullOrEmpty(baseUrl))
        client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("apikey",
        configuration[$"{EvolutionApiOptions.SectionName}:ApiKey"] ?? string.Empty);
});
```

**Código novo**:
```csharp
services.AddHttpClient<IWhatsAppService, EvolutionApiWhatsAppService>(client =>
{
    var baseUrl = configuration[$"{EvolutionApiOptions.SectionName}:BaseUrl"];
    if (!string.IsNullOrEmpty(baseUrl))
        client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("apikey",
        configuration[$"{EvolutionApiOptions.SectionName}:ApiKey"] ?? string.Empty);
    client.Timeout = TimeSpan.FromSeconds(10);
});
```

WhatsApp é fire-and-forget com menor criticidade de latência; 10 segundos é suficiente.

**Verify**: `dotnet build src/Horafy.Infrastructure` → exit 0

### Step 3: Verificação final

**Verify**: `dotnet test tests/` → todos os testes passam

## Test plan

Este plano não requer novos testes unitários (os timeouts são testados indiretamente por comportamento de HttpClient). Porém, se o projeto tiver testes de integração de gateway, verificar que os mocks existentes não quebram com a adição de timeout.

## Done criteria

- [ ] `dotnet build src/` exits 0
- [ ] `dotnet test tests/` exits 0
- [ ] `grep -A5 "MercadoPagoPaymentGateway" src/Horafy.Infrastructure/DependencyInjection.cs` mostra `client.Timeout = TimeSpan.FromSeconds(15)`
- [ ] `grep -A5 "EvolutionApiWhatsAppService" src/Horafy.Infrastructure/DependencyInjection.cs` mostra `client.Timeout = TimeSpan.FromSeconds(10)`
- [ ] `plans/README.md` atualizado

## STOP conditions

- Os blocos de `AddHttpClient` não correspondem aos excerpts (drift — compare e reporte)
- Um teste existente falha após a mudança (improvável, mas reporte)
- Encontrar que `MercadoPagoPaymentGateway` ou `EvolutionApiWhatsAppService` já definem timeout via `CancellationTokenSource` internamente — nesse caso, não duplicar e reporte

## Maintenance notes

- Se Polly retry policies forem adicionadas no futuro (Plan 004 proposto), o timeout do `HttpClient` deve ser maior que o tempo total de retry (ex.: 3 retries × 15s + backoff ≈ 60s total — nesse caso, ajustar para `TimeSpan.FromSeconds(60)`)
- Revisor: conferir que nenhum teste mockava `HttpClient` com `InfiniteTimeSpan` explicitamente (quebraria com timeout real)
