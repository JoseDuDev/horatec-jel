# Plan 003: Adicionar Redis e RabbitMQ ao health check

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md`.
>
> **Drift check (run first)**: `git diff --stat 8f8cad2..HEAD -- src/Horafy.API/Program.cs src/Horafy.API/Horafy.API.csproj`
> Se qualquer arquivo in-scope mudou, compare os excerpts antes de prosseguir.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: reliability
- **Planned at**: commit `8f8cad2`, 2026-06-26

## Why this matters

O endpoint `/health` verifica apenas PostgreSQL. Se Redis ou RabbitMQ ficarem indisponíveis, o load balancer ou Kubernetes considerará o pod saudável e continuará roteando tráfego. O resultado: bookings são criados mas confirmações via e-mail/WhatsApp ficam presas na fila indefinidamente, e sessões de cache falham silenciosamente. Redis e RabbitMQ são dependências críticas — notificações, jobs Quartz (lembretes de booking) e cache de tenant passam por eles.

## Current state

**Arquivo**: `src/Horafy.API/Program.cs`
- Papel: configuração da aplicação, middleware, health checks

```csharp
// Program.cs:129-134 — somente PostgreSQL verificado
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "postgresql",
        tags: ["db", "ready"]);
```

**Dependências de health check disponíveis** — verificar o `.csproj`:
- `AspNetCore.HealthChecks.NpgSql` já está referenciado
- `AspNetCore.HealthChecks.Redis` e `AspNetCore.HealthChecks.RabbitMQ` precisam ser adicionados

**Configuração do Redis** (lida de `appsettings.json`): `builder.Configuration.GetConnectionString("Redis")` ou seção `Redis:ConnectionString`. Verificar o valor exato no arquivo antes de prosseguir.

**Configuração do RabbitMQ**: `builder.Configuration.GetSection("RabbitMq")` com campos `Host`, `VirtualHost`, `Username`, `Password` (já consumido em `DependencyInjection.cs:172`).

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Add package Redis | `dotnet add src/Horafy.API/Horafy.API.csproj package AspNetCore.HealthChecks.Redis` | exit 0 |
| Add package RabbitMQ | `dotnet add src/Horafy.API/Horafy.API.csproj package AspNetCore.HealthChecks.RabbitMQ` | exit 0 |
| Build | `dotnet build src/Horafy.API` | exit 0 |
| Tests | `dotnet test tests/` | All pass |
| Verify health endpoint | `curl http://localhost:5000/health` (requer app rodando) | JSON com todos serviços healthy |

## Scope

**In scope**:
- `src/Horafy.API/Horafy.API.csproj` — adicionar packages NuGet
- `src/Horafy.API/Program.cs` — adicionar `.AddRedis()` e `.AddRabbitMQ()`

**Out of scope**:
- Endpoint `/health/ready` vs `/health/live` — não criar health checks diferenciados neste plano (isso é infraestrutura de Kubernetes; manter um único `/health` por ora)
- Adicionar health check de SMTP — baixa prioridade
- Modificar `docker-compose.yml` — não necessário

## Git workflow

- Branch: `advisor/003-health-checks-redis-rabbitmq`
- Commit: `Adicionar Redis e RabbitMQ ao health check`
- Não fazer push nem abrir PR a menos que instruído

## Steps

### Step 1: Verificar connection strings no appsettings.json

Antes de escrever código, ler `src/Horafy.API/appsettings.json` e identificar:
- A connection string do Redis (pode ser `ConnectionStrings:Redis` ou uma seção separada)
- As configurações do RabbitMQ (provavelmente em `RabbitMq:Host`, `RabbitMq:Username`, `RabbitMq:Password`)

Se Redis estiver em formato diferente de connection string (ex.: `Redis:Host` e `Redis:Port`), montar a string `"${Host}:${Port},password=${Password}"` no código.

**Verify**: Você consegue ler os valores corretos? Se não encontrar, STOP.

### Step 2: Adicionar packages NuGet

```bash
dotnet add src/Horafy.API/Horafy.API.csproj package AspNetCore.HealthChecks.Redis --version 8.0.*
dotnet add src/Horafy.API/Horafy.API.csproj package AspNetCore.HealthChecks.RabbitMQ --version 8.0.*
```

Use a versão compatível com `AspNetCore.HealthChecks.NpgSql` já instalado (provavelmente `8.0.x`). Se houver conflito de versão, escolha a mesma major/minor já usada pelo NpgSql package.

**Verify**: `dotnet restore src/Horafy.API` → exit 0

### Step 3: Adicionar health checks em Program.cs

Em `src/Horafy.API/Program.cs`, localizar o bloco `AddHealthChecks()` (linhas 130-134) e adicionar:

```csharp
// Adicionar using na parte superior do arquivo se necessário:
// using HealthChecks.RabbitMQ;  (conforme package instalado)

builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "postgresql",
        tags: ["db", "ready"])
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis")!,  // ajustar key conforme Step 1
        name: "redis",
        tags: ["cache", "ready"])
    .AddRabbitMQ(
        rabbitConnectionString: $"amqp://{builder.Configuration["RabbitMq:Username"]}:{builder.Configuration["RabbitMq:Password"]}@{builder.Configuration["RabbitMq:Host"]}{builder.Configuration["RabbitMq:VirtualHost"]}",
        name: "rabbitmq",
        tags: ["messaging", "ready"]);
```

**Nota**: Se o package `AspNetCore.HealthChecks.RabbitMQ` usar uma API diferente (ex.: `IConnection` factory), adaptar conforme a documentação do package instalado — a interface pode variar por versão.

**Verify**: `dotnet build src/Horafy.API` → exit 0

### Step 4: Verificação final

**Verify**: `dotnet test tests/` → todos os testes passam

## Test plan

Não há testes unitários para health checks (eles testam conectividade real). O teste é manual:
1. `docker-compose up -d` (ou `docker-compose.e2e.yml`)
2. `dotnet run --project src/Horafy.API`
3. `curl http://localhost:5000/health` → JSON com `status: "Healthy"`, `postgresql`, `redis`, `rabbitmq` todos `Healthy`
4. `docker stop horafy_redis_1` → `curl http://localhost:5000/health` → `status: "Unhealthy"`, `redis: "Unhealthy"`

## Done criteria

- [ ] `dotnet build src/` exits 0
- [ ] `dotnet test tests/` exits 0
- [ ] `grep -n "AddRedis\|AddRabbitMQ" src/Horafy.API/Program.cs` retorna 2 matches
- [ ] `src/Horafy.API/Horafy.API.csproj` contém referência a `AspNetCore.HealthChecks.Redis` e `AspNetCore.HealthChecks.RabbitMQ`
- [ ] `plans/README.md` atualizado

## STOP conditions

- Connection string do Redis não encontrada em `appsettings.json` (investigar onde Redis é configurado)
- Package `AspNetCore.HealthChecks.RabbitMQ` com API incompatível com a versão de RabbitMQ usada — reportar e aguardar orientação
- Build falha com conflito de versão de packages — não downgrade packages existentes; reporte

## Maintenance notes

- Se Kubernetes for introduzido, diferenciar `/health/live` (apenas app) e `/health/ready` (inclui dependências): liveness probe não deve falhar por Redis/RabbitMQ down (pod seria reiniciado desnecessariamente); readiness probe sim
- Ao escalar para múltiplos pods, o health check de RabbitMQ verifica apenas conectividade ao broker, não o estado das filas — se filas acumularem mensagens mortas, isso não será detectado aqui; considerar métrica separada
