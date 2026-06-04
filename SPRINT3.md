# Sprint 3 — Módulo Tenant: CRUD, Configuração Visual e Resolução de Domínio

## Como rodar (após Sprint 2)

```bash
dotnet restore
docker-compose up -d postgres redis rabbitmq seq
dotnet run --project src/Horafy.API
dotnet test
```

## Onboarding — criando um tenant

```http
POST /api/v1/platform/tenants
Content-Type: application/json

{
  "name": "Barbearia do João",
  "slug": "barbearia-joao",
  "vertical": "Barbershop",
  "ownerName": "João Silva",
  "ownerEmail": "joao@barbearia.com",
  "ownerPassword": "Senha@123"
}
```

**Resposta 201** — retorna `tenantId`, `slug` e o par de tokens JWT para uso imediato.

---

## Endpoints do Tenant

### Público (sem autenticação)
| Método | URL | Descrição |
|--------|-----|-----------|
| POST | `/api/v1/platform/tenants` | Onboarding — cria tenant + schema + owner |
| GET  | `/api/v1/platform/tenants/{slug}` | Dados públicos do tenant (landing page) |

### Painel do proprietário (requerem JWT + `X-Tenant-Slug` ou subdomínio)
| Método | URL | Auth | Descrição |
|--------|-----|------|-----------|
| GET    | `/api/v1/tenants/me` | Owner/Admin | Dados completos do tenant |
| PUT    | `/api/v1/tenants/me` | Owner/Admin | Atualizar dados cadastrais |
| PUT    | `/api/v1/tenants/me/theme` | Owner/Admin | Atualizar identidade visual |
| PUT    | `/api/v1/tenants/me/domain` | Owner | Vincular domínio próprio |
| DELETE | `/api/v1/tenants/me/domain` | Owner | Remover domínio próprio |

### Administração de plataforma (PlatformAdmin)
| Método | URL | Descrição |
|--------|-----|-----------|
| POST | `/api/v1/platform/tenants/{id}/suspend` | Suspender tenant |
| POST | `/api/v1/platform/tenants/{id}/activate` | Reativar tenant |

---

## Configuração visual — `PUT /api/v1/tenants/me/theme`

```json
{
  "primaryColor":      "#2563EB",
  "secondaryColor":    "#7C3AED",
  "backgroundColor":   "#F8FAFC",
  "textColor":         "#1E293B",
  "fontFamily":        "Inter",
  "logoUrl":           "https://cdn.minhaclinica.com.br/logo.png",
  "bannerText":        "Agende online, sem fila.",
  "showReviews":       true,
  "showTeam":          true,
  "showServicePrices": true,
  "whatsAppNumber":    "5511999999999",
  "instagramUrl":      "https://instagram.com/minhaclinica",
  "sectionsOrder":     "banner,services,team,reviews,contact"
}
```

## Resolução de domínio próprio

O tenant pode vincular `minhaclinica.com.br` via `PUT /api/v1/tenants/me/domain`.

**Fluxo completo:**
1. Owner faz `PUT` com o domínio → API verifica que não está em uso
2. Owner configura CNAME no DNS: `minhaclinica.com.br → api.horafy.com.br`
3. `TenantMiddleware` resolve automaticamente o tenant pelo `Host` header
4. Todas as queries do tenant são direcionadas ao schema `tenant_barbearia-joao`

---

## O que foi implementado

### Domain
- `Tenant.UpdateInfo(...)` — atualiza dados cadastrais
- `Tenant.RemoveCustomDomain()` — remove domínio próprio
- `ITenantRepository.IsDomainTakenAsync(...)` — valida unicidade de domínio

### Application
- `UpdateTenantCommand` — atualiza nome, contato, endereço, fuso, locale
- `UpdateTenantThemeCommand` — atualiza identidade visual completa
- `SetCustomDomainCommand` — vincula domínio próprio (com validação de unicidade)
- `RemoveCustomDomainCommand` — remove domínio próprio
- `SuspendTenantCommand` — suspende tenant (PlatformAdmin)
- `ActivateTenantCommand` — reativa tenant (PlatformAdmin)
- `GetCurrentTenantQuery` — retorna dados completos do tenant autenticado
- `GetTenantBySlugQuery` — retorna dados públicos pelo slug

### Infrastructure
- `TenantRepository.IsDomainTakenAsync` implementado

### API
- `TenantsController` completo com 10 endpoints
- TenantMiddleware já cobria resolução por domínio próprio desde a Sprint 1

### Testes
- `TenantCrudTests` — 9 casos (domain)
- `UpdateTenantCommandHandlerTests` — 4 casos (application)

## Próxima Sprint

**Sprint 4 — Agenda & Disponibilidade**: grade de horários por profissional, verificação de disponibilidade em tempo real, notificações WhatsApp/email.
