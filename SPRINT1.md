# Sprint 1 — Infraestrutura Base

## Como rodar

```bash
# 1. Copie as variáveis de ambiente
cp .env.example .env

# 2. Suba a infraestrutura
docker-compose up -d postgres redis rabbitmq seq

# 3. Restaure os pacotes
dotnet restore

# 4. Aplique as migrations (cria schema public e tabela tenants)
dotnet ef database update --project src/Horafy.Infrastructure --startup-project src/Horafy.API

# 5. Rode a API
dotnet run --project src/Horafy.API

# 6. Rode os testes
dotnet test
```

## URLs úteis (desenvolvimento)

| Serviço       | URL                          |
|---------------|------------------------------|
| API           | http://localhost:8080        |
| Swagger       | http://localhost:8080/swagger|
| Scalar        | http://localhost:8080/scalar |
| Seq (logs)    | http://localhost:5341        |
| RabbitMQ UI   | http://localhost:15672       |
| PostgreSQL    | localhost:5432               |

## Criar a migration inicial

```bash
dotnet ef migrations add InitialCreate \
  --project src/Horafy.Infrastructure \
  --startup-project src/Horafy.API \
  --output-dir Persistence/Migrations
```

## Estrutura de schemas no PostgreSQL

```
public            → tabelas globais (tenants, outbox_messages, __ef_migrations_history)
tenant_{slug}     → tabelas isoladas por cliente (Sprint 3+)
```

## O que foi implementado

- **Horafy.Shared**: Result pattern, Error, PagedResult
- **Horafy.Domain**: BaseEntity (auditoria + soft-delete + Domain Events), Tenant aggregate, interfaces
- **Horafy.Application**: MediatR + ValidationBehavior + LoggingBehavior + DI registration
- **Horafy.Infrastructure**: HorafyDbContext, multi-tenancy (TenantMiddleware + TenantService), AuditInterceptor, OutboxPattern, repositórios, UnitOfWork
- **Horafy.API**: Program.cs, Serilog, versionamento, Swagger/Scalar, ExceptionMiddleware, ApiControllerBase
- **Docker Compose**: PostgreSQL 16, Redis 7, RabbitMQ 3.13, Seq, API
- **Testes**: TenantTests (9 casos), ResultTests (6 casos), TenantMiddlewareTests (6 casos)

## Próxima Sprint

**Sprint 2 — Autenticação**: Google/Apple OAuth, JWT com claims de tenant, endpoints de login.
