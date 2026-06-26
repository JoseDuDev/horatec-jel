# Plan 011: Adicionar rate limiting nos endpoints de autenticação

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md`.
>
> **Drift check (run first)**: `git diff --stat 8f8cad2..HEAD -- src/Horafy.API/Controllers/V1/AuthController.cs src/Horafy.API/Program.cs src/Horafy.API/Horafy.API.csproj`
> Se qualquer arquivo in-scope mudou, compare os excerpts antes de prosseguir.

## Status

- **Priority**: P2
- **Effort**: M
- **Risk**: MED
- **Depends on**: none
- **Category**: security
- **Planned at**: commit `8f8cad2`, 2026-06-26

## Why this matters

Os endpoints `/api/v1/auth/email` (login), `/api/v1/auth/register` e `/api/v1/auth/refresh` não têm nenhum throttling. Um atacante pode enviar milhares de tentativas de senha por segundo para um email específico (brute force), ou tentar listas de credenciais (credential stuffing). O .NET 7+ inclui `Microsoft.AspNetCore.RateLimiting` nativo — nenhuma dependência externa é necessária.

## Current state

**Arquivo 1**: `src/Horafy.API/Controllers/V1/AuthController.cs`
- Papel: controller de autenticação, todos os endpoints `[AllowAnonymous]`
- Endpoints sem rate limiting:
  - `POST /api/v1/auth/email` (login com email/senha)
  - `POST /api/v1/auth/register`
  - `POST /api/v1/auth/refresh`
  - `POST /api/v1/auth/google`
  - `POST /api/v1/auth/apple`

**Arquivo 2**: `src/Horafy.API/Program.cs`
- Papel: configuração da aplicação
- Não há `AddRateLimiter` ou `UseRateLimiter` no arquivo

**Convenção do projeto**: Middlewares são adicionados em `Program.cs` (linhas ~139-160). Policies nomeadas são usadas (ex.: `"HorafyCors"` para CORS).

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Build | `dotnet build src/Horafy.API` | exit 0 |
| Tests | `dotnet test tests/` | All pass |

## Scope

**In scope**:
- `src/Horafy.API/Program.cs` — adicionar `AddRateLimiter` e `UseRateLimiter`
- `src/Horafy.API/Controllers/V1/AuthController.cs` — adicionar atributo `[EnableRateLimiting]`

**Out of scope**:
- Outros controllers — não adicionar rate limiting global (pode afetar fluxos legítimos)
- Redis-backed rate limiting (distributed) — usar o `FixedWindowRateLimiter` em memória para este plano; distributed pode ser adicionado depois
- Middleware de rate limiting por IP em nível de Caddy/Nginx — é complementar, não substituto

## Git workflow

- Branch: `advisor/011-rate-limiting-auth`
- Commit: `Adicionar rate limiting nos endpoints de autenticação`
- Não fazer push nem abrir PR a menos que instruído

## Steps

### Step 1: Adicionar configuração de rate limiting em Program.cs

Em `src/Horafy.API/Program.cs`, após o bloco de CORS (linha ~104), adicionar:

```csharp
// ── Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Limite por IP para endpoints de login: 10 tentativas por 5 minutos
    options.AddFixedWindowLimiter("auth-login", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMinutes(5);
        limiterOptions.PermitLimit = 10;
        limiterOptions.QueueLimit = 0;
        limiterOptions.AutoReplenishment = true;
    });

    // Limite por IP para registro: 5 registros por 10 minutos
    options.AddFixedWindowLimiter("auth-register", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMinutes(10);
        limiterOptions.PermitLimit = 5;
        limiterOptions.QueueLimit = 0;
        limiterOptions.AutoReplenishment = true;
    });

    // Limite por IP para refresh: 20 refreshes por hora
    options.AddFixedWindowLimiter("auth-refresh", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromHours(1);
        limiterOptions.PermitLimit = 20;
        limiterOptions.QueueLimit = 0;
        limiterOptions.AutoReplenishment = true;
    });
});
```

Adicionar `using Microsoft.AspNetCore.RateLimiting;` e `using System.Threading.RateLimiting;` no topo do arquivo se necessário.

**Verify**: `dotnet build src/Horafy.API` → exit 0

### Step 2: Adicionar `UseRateLimiter` no pipeline de middlewares

Em `Program.cs`, adicionar `app.UseRateLimiter()` após `app.UseAuthentication()` (linha ~154):

```csharp
app.UseAuthentication();
app.UseRateLimiter();          // ← adicionar aqui
app.UseMiddleware<TenantMiddleware>();
```

**Verify**: `dotnet build src/Horafy.API` → exit 0

### Step 3: Adicionar atributo `[EnableRateLimiting]` nos endpoints

Em `src/Horafy.API/Controllers/V1/AuthController.cs`, adicionar o atributo em cada endpoint:

```csharp
using Microsoft.AspNetCore.RateLimiting;

// Login com Google
[HttpPost("google")]
[AllowAnonymous]
[EnableRateLimiting("auth-login")]
public async Task<IActionResult> LoginWithGoogle(...) { ... }

// Login com Apple
[HttpPost("apple")]
[AllowAnonymous]
[EnableRateLimiting("auth-login")]
public async Task<IActionResult> LoginWithApple(...) { ... }

// Login com email
[HttpPost("email")]
[AllowAnonymous]
[EnableRateLimiting("auth-login")]
public async Task<IActionResult> LoginWithEmail(...) { ... }

// Registro
[HttpPost("register")]
[AllowAnonymous]
[EnableRateLimiting("auth-register")]
public async Task<IActionResult> Register(...) { ... }

// Refresh
[HttpPost("refresh")]
[AllowAnonymous]
[EnableRateLimiting("auth-refresh")]
public async Task<IActionResult> Refresh(...) { ... }
```

O endpoint `GET /auth/me` com `[Authorize]` não precisa de rate limiting (já protegido por autenticação).

**Verify**: `dotnet build src/Horafy.API` → exit 0

### Step 4: Verificação final

**Verify**: `dotnet test tests/` → todos os testes passam

## Test plan

Teste manual:
1. Enviar 11 requisições POST para `/api/v1/auth/email` com credenciais inválidas em menos de 5 minutos
2. A 11ª deve retornar `429 Too Many Requests`
3. Aguardar 5 minutos → nova tentativa funciona

Verificar que testes existentes de `AuthController` não foram quebrados (eles podem precisar de mock do rate limiter ou usar `DisableRateLimiting` attribute).

## Done criteria

- [ ] `dotnet build src/` exits 0
- [ ] `dotnet test tests/` exits 0
- [ ] `grep -n "EnableRateLimiting" src/Horafy.API/Controllers/V1/AuthController.cs` retorna 5 matches (login-google, login-apple, login-email, register, refresh)
- [ ] `grep -n "AddRateLimiter\|UseRateLimiter" src/Horafy.API/Program.cs` retorna 2 matches
- [ ] `plans/README.md` atualizado

## STOP conditions

- `Microsoft.AspNetCore.RateLimiting` não disponível na versão do .NET usada (< .NET 7) — verificar `<TargetFramework>` no `.csproj`; se `net8.0`, está disponível nativamente
- Testes de `AuthController` usam mocks que não suportam rate limiting e falham — adicionar `[DisableRateLimiting]` apenas nos testes ou configurar o rate limiter com limites altos no ambiente de teste
- O projeto já tem um middleware de rate limiting personalizado — STOP e reporte; adaptar ao invés de duplicar

## Maintenance notes

- `FixedWindowRateLimiter` em memória não é distribuído — em múltiplas instâncias da API, cada pod tem seu próprio contador. Para produção com múltiplos pods, migrar para Redis-backed limiter (via `RedisRateLimiter` do pacote `Microsoft.AspNetCore.RateLimiting.Redis` ou implementação customizada com `IDistributedCache`)
- O limite de 10 tentativas em 5 minutos é conservador — se gerar falsos positivos em testes de carga ou fluxos legítimos de admin, aumentar para 20/5min. Monitorar via logs de `429` no Seq
- Retornar `Retry-After` header na resposta `429` para informar o cliente quando tentar novamente: configurar `options.OnRejected` em `AddRateLimiter` para adicionar o header
