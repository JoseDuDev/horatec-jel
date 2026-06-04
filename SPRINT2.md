# Sprint 2 — Autenticação

## Como rodar (após Sprint 1)

```bash
# 1. Restaure os novos pacotes
dotnet restore

# 2. Crie a migration da tabela users
dotnet ef migrations add AddUsersTable \
  --project src/Horafy.Infrastructure \
  --startup-project src/Horafy.API \
  --output-dir Persistence/Migrations

# 3. Aplique a migration
dotnet ef database update \
  --project src/Horafy.Infrastructure \
  --startup-project src/Horafy.API

# 4. Suba a infra (se ainda não estiver rodando)
docker-compose up -d postgres redis rabbitmq seq

# 5. Rode a API
dotnet run --project src/Horafy.API

# 6. Rode os testes
dotnet test
```

## Variáveis de ambiente obrigatórias

| Variável              | Descrição                                  | Dev (appsettings.Development.json) |
|-----------------------|--------------------------------------------|------------------------------------|
| `Jwt__Secret`         | Chave HMAC-SHA256 (mínimo 32 chars)        | Já configurado para dev            |
| `Jwt__Issuer`         | Issuer do JWT                              | `horafy`                           |
| `Jwt__Audience`       | Audience do JWT                            | `horafy-clients`                   |
| `Google__ClientId`    | Client ID do Google Cloud Console          | Opcional (valida sem audience)     |
| `Apple__ClientId`     | Bundle ID / Service ID do Apple Developer  | Opcional (valida sem audience)     |

> **Produção:** substitua `Jwt__Secret` por uma chave de 64+ caracteres gerada com:
> ```bash
> openssl rand -base64 64
> ```

## Endpoints de autenticação

| Método | URL                        | Descrição                              | Auth |
|--------|----------------------------|----------------------------------------|------|
| POST   | `/api/v1/auth/google`      | Login com Google ID token              | ❌   |
| POST   | `/api/v1/auth/apple`       | Login com Apple identity token         | ❌   |
| POST   | `/api/v1/auth/email`       | Login com e-mail e senha               | ❌   |
| POST   | `/api/v1/auth/register`    | Cadastro com e-mail e senha            | ❌   |
| POST   | `/api/v1/auth/refresh`     | Renova access token via refresh token  | ❌   |
| GET    | `/api/v1/auth/me`          | Dados do usuário autenticado           | ✅   |

### Exemplo — Login com e-mail
```http
POST /api/v1/auth/email
Content-Type: application/json

{
  "email": "jose@gmail.com",
  "password": "Senha123",
  "tenantSlug": "minha-barbearia"
}
```

### Exemplo — Resposta (TokenPair)
```json
{
  "accessToken": "eyJhbGci...",
  "refreshToken": "eyJhbGci...",
  "accessTokenExpiresAt": "2026-06-03T21:00:00Z",
  "refreshTokenExpiresAt": "2026-06-10T20:00:00Z"
}
```

### Exemplo — Requisição autenticada
```http
GET /api/v1/auth/me
Authorization: Bearer eyJhbGci...
X-Tenant-Slug: minha-barbearia
```

## Estratégia de tokens

| Token         | Duração padrão | Tipo        | Armazenamento |
|---------------|----------------|-------------|---------------|
| Access token  | 60 minutos     | JWT (RS256) | Memória/header |
| Refresh token | 7 dias         | JWT (RS256) | HTTP-only cookie recomendado |

- **Stateless:** refresh tokens não são armazenados no banco — rogue tokens expiram naturalmente.
- **Revoção:** para invalidar antes do prazo, altere o `Jwt__Secret` (invalida todos os tokens).

## Roles e permissões

| Role            | Descrição                                      |
|-----------------|------------------------------------------------|
| `PlatformAdmin` | Acesso total à plataforma                      |
| `TenantOwner`   | Dono do estabelecimento — acesso total ao tenant |
| `TenantAdmin`   | Administrador — sem gestão de billing          |
| `TenantStaff`   | Funcionário — apenas agendamentos e serviços   |
| `Customer`      | Cliente final — criar/cancelar/ver agendamentos |

As permissões são atribuídas automaticamente na criação do usuário conforme o role,
e podem ser ajustadas individualmente via `GrantPermission` / `RevokePermission`.

## O que foi implementado

### Domain
- `User` aggregate — suporte a Google, Apple e e-mail/senha
- `UserRole` enum (5 roles)
- `UserPermission` enum (15 permissões granulares)
- `UserCreatedEvent` domain event
- `IUserRepository` interface

### Application
- `ITokenService`, `IPasswordHasher`, `IGoogleOAuthService`, `IAppleOAuthService`, `ICurrentUserService`
- Commands: `LoginWithGoogleCommand`, `LoginWithAppleCommand`, `LoginWithEmailCommand`, `RegisterWithEmailCommand`, `RefreshTokenCommand`
- Query: `GetCurrentUserQuery`
- `AuthErrors` — erros tipados de autenticação

### Infrastructure
- `JwtTokenService` — access token (60 min) + refresh token stateless (7 dias)
- `GoogleOAuthService` — valida Google ID tokens via `Google.Apis.Auth`
- `AppleOAuthService` — valida Apple identity tokens via JWKS
- `BCryptPasswordHasher` — hash com work factor 12
- `CurrentUserService` — resolve usuário atual das claims JWT
- `UserRepository` + `UserEntityConfiguration` (EF Core)
- Novos pacotes: `System.IdentityModel.Tokens.Jwt`, `Microsoft.IdentityModel.Tokens`, `Google.Apis.Auth`, `BCrypt.Net-Next`

### API
- `AuthController` — 6 endpoints REST
- JWT Bearer middleware configurado no `Program.cs`

### Testes
- `UserTests` — 11 casos (domain)
- `LoginWithEmailCommandHandlerTests` — 5 casos (application)
- `JwtTokenServiceTests` — 6 casos (infrastructure)

## Próxima Sprint

**Sprint 3 — Multi-tenancy avançado**: isolamento por schema PostgreSQL, onboarding de tenant, CRUD de serviços/profissionais.
