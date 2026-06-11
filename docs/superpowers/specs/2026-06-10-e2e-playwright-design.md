# Horafy — E2E Playwright Design

> **Data:** 2026-06-10
> **Status:** Aprovado

---

## 1. Objetivo

Cobrir os 4 fluxos críticos do Horafy com testes E2E usando Playwright, rodando localmente via docker-compose. Zero dependências externas — pagamento mockado, banco isolado por suite.

---

## 2. Decisões tomadas

| Decisão | Escolha |
|---|---|
| Ambiente | docker-compose.e2e.yml sobe tudo (infra + API + Next.js) |
| Pagamento | FakePaymentGateway — aprovação imediata, sem MercadoPago |
| Isolamento | Tenant novo por spec file (slug `test-<timestamp>`) |
| CI | Somente local por enquanto |
| Browser | Chromium apenas |

---

## 3. Arquitetura

### 3.1 Camadas

```
npx playwright test
    │
    ├── global-setup.ts   →  docker compose -f docker-compose.e2e.yml up -d --wait
    │
    ├── e2e/*.spec.ts
    │       beforeAll: setupTenant() via REST  →  tenant isolado por spec
    │       tests: Playwright browser
    │
    └── global-teardown.ts  →  docker compose -f docker-compose.e2e.yml down -v
```

### 3.2 Serviços no docker-compose.e2e.yml

| Serviço | Imagem | Porta | Diferença do docker-compose.yml |
|---|---|---|---|
| postgres | postgres:16-alpine | 5433 | igual |
| redis | redis:7-alpine | 6380 | igual |
| rabbitmq | rabbitmq:3.13-management | 5673 | igual |
| seq | datalust/seq | 5341 | igual |
| api | build local | 8083 | + `PAYMENT_GATEWAY=fake`, `ASPNETCORE_ENVIRONMENT=E2ETest` |
| frontend | build local | 3000 | novo — `next build` + `next start` |

O `--wait` do `docker compose up` aguarda todos os healthchecks passarem antes de retornar.  
Timeout do `globalSetup`: 120s (suficiente para o `next build`).

---

## 4. FakePaymentGateway

**Arquivo:** `src/Horafy.Infrastructure/Gateways/FakePaymentGateway.cs`

Implementa `IPaymentGateway` com respostas fixas:

| Método | Retorno |
|---|---|
| `CreatePreferenceAsync` | `PaymentPreferenceResult` com URL fake e preferenceId gerado via `Guid.NewGuid()` |
| `GetPaymentStatusAsync` | `PaymentStatusResult` com `PaymentStatus.Approved` |
| `RefundAsync` | `RefundResult(true, null)` |
| `ValidateWebhookSignature` | `true` |

**Registro em `DependencyInjection.cs`:**

```csharp
if (Environment.GetEnvironmentVariable("PAYMENT_GATEWAY") == "fake")
    services.AddScoped<IPaymentGateway, FakePaymentGateway>();
else
    services.AddHttpClient<IPaymentGateway, MercadoPagoPaymentGateway>(...);
```

---

## 5. Playwright

### 5.1 playwright.config.ts

```
testDir:            ./e2e
baseURL:            http://localhost:3000
globalSetup:        ./e2e/global-setup.ts
globalTeardown:     ./e2e/global-teardown.ts
browser:            chromium
timeout:            30_000
navigationTimeout:  15_000
retries:            1
```

### 5.2 global-setup.ts

Os arquivos `global-setup.ts` e `global-teardown.ts` ficam em `frontend/e2e/`, portanto o docker-compose.e2e.yml está em `../../` relativo a eles — mas como o `execSync` herda o `cwd` do processo Playwright (raiz do projeto ao rodar `cd frontend && npx playwright test`), o caminho correto é `../docker-compose.e2e.yml`.

```ts
import { execSync } from 'child_process'
import path from 'path'

const ROOT = path.resolve(__dirname, '../..')

export default async function globalSetup() {
  execSync(
    'docker compose -f docker-compose.e2e.yml up -d --wait',
    { stdio: 'inherit', timeout: 120_000, cwd: ROOT }
  )
}
```

### 5.3 global-teardown.ts

```ts
import { execSync } from 'child_process'
import path from 'path'

const ROOT = path.resolve(__dirname, '../..')

export default async function globalTeardown() {
  execSync(
    'docker compose -f docker-compose.e2e.yml down -v',
    { stdio: 'inherit', cwd: ROOT }
  )
}
```

---

## 6. Helpers

**`e2e/helpers/api.ts`** — chamadas REST para setup programático (sem UI):

| Função | Endpoint | Uso |
|---|---|---|
| `setupTenant(slug)` | POST `/api/v1/auth/register` | Cria tenant + owner, retorna `{ token, tenantSlug }` |
| `createService(token, slug, data)` | POST `/api/v1/services` | Cria serviço no tenant |
| `createResource(token, slug, data)` | POST `/api/v1/resources` | Cria recurso no tenant |
| `linkServiceToResource(token, slug, resourceId, serviceId)` | POST `/api/v1/resources/{id}/services/{serviceId}` | Vincula serviço ao recurso |
| `setBusinessHours(token, slug, hours)` | PUT `/api/v1/availability/business-hours` | Define horários de funcionamento |
| `createAdminBooking(token, slug, data)` | POST `/api/v1/bookings/admin` | Cria agendamento direto (setup para testes de workflow) |
| `loginCustomer(email, slug)` | POST `/api/v1/customers/auth/test-login` | Retorna token de cliente (endpoint ativo só em E2ETest) |
| `saveStorageState(token, role, path)` | — | Salva cookie em arquivo para `storageState` do Playwright |

Todas as funções usam `fetch` nativo (Node 18+) apontando para `http://localhost:8083`.

**Endpoint de teste para login de cliente:** O `CustomerAuthController` só expõe Google/Apple OAuth — não há login por email para clientes. Para os specs que precisam de um cliente autenticado (`admin-workflow`, `loyalty`), será necessário um endpoint adicional:

```
POST /api/v1/customers/auth/test-login
Body: { email: string, tenantSlug: string }
Retorna: JWT de cliente
Restrição: só ativo quando ASPNETCORE_ENVIRONMENT == "E2ETest"
```

Cria o cliente no banco se não existir, retorna token válido. Bloqueado por guard em produção.

---

## 7. Os 4 specs

### 7.1 `e2e/onboarding.spec.ts`

**Setup:** `setupTenant()` via API — só o registro, sem configuração.

**Fluxo testado na UI:**
1. Login como owner no admin
2. Wizard de 5 passos: identidade visual → serviço → recurso → horários → conclusão
3. Dashboard admin carrega após o wizard

**Assertions principais:**
- Cada passo do wizard avança sem erro
- URL final é `/admin/dashboard`
- KPIs da semana são visíveis

---

### 7.2 `e2e/booking.spec.ts`

**Setup via API:** tenant + serviço + recurso + link serviço↔recurso + horários de funcionamento (seg–sex 08:00–18:00, slot 60min).

**Fluxo testado na UI:**
1. Portal do cliente (`/[slug]`)
2. Wizard: escolhe serviço → recurso → slot → preenche nome/email/telefone → checkout Pix
3. FakePaymentGateway retorna aprovação imediata
4. Página de confirmação exibe status `Confirmado`

**Assertions principais:**
- Slots disponíveis aparecem no calendário
- Após checkout, status é `Confirmado`
- Número de confirmação é exibido

---

### 7.3 `e2e/admin-workflow.spec.ts`

**Setup via API:** tenant + serviço + recurso + horários + agendamento criado via `createAdminBooking` (status inicial: `Pendente`).

**Fluxo testado na UI (dois atores):**
1. Admin acessa `/admin/agendamentos`, confirma o agendamento → status vira `Confirmado`
2. Cliente acessa `/minha-conta`, cancela o agendamento → status vira `Cancelado`
3. Admin verifica no financeiro que o reembolso foi registrado

**Assertions principais:**
- Tabela de agendamentos reflete mudança de status em tempo real
- Página do cliente mostra `Cancelado`
- Financeiro mostra o reembolso

---

### 7.4 `e2e/loyalty.spec.ts`

**Setup via API:** tenant com fidelidade configurada (ex: 10% do valor em créditos) + agendamento no status `Confirmado`.

**Fluxo testado na UI:**
1. Admin acessa `/admin/agendamentos`, marca como `Concluído`
2. Cliente acessa `/minha-conta/carteira`
3. Saldo da carteira mostra o crédito de fidelidade

**Assertions principais:**
- Status do agendamento muda para `Concluído`
- Carteira do cliente exibe crédito > 0
- Valor do crédito corresponde à regra de fidelidade configurada

---

## 8. Estrutura de arquivos

```
frontend/
├── playwright.config.ts
├── e2e/
│   ├── global-setup.ts
│   ├── global-teardown.ts
│   ├── helpers/
│   │   └── api.ts
│   ├── onboarding.spec.ts
│   ├── booking.spec.ts
│   ├── admin-workflow.spec.ts
│   └── loyalty.spec.ts
src/
└── Horafy.Infrastructure/
    └── Gateways/
        └── FakePaymentGateway.cs
docker-compose.e2e.yml
```

---

## 9. Comando para rodar

```bash
# Na raiz do projeto
cd frontend && npx playwright test

# Spec específico
npx playwright test e2e/booking.spec.ts

# Com UI interativa
npx playwright test --ui
```

---

## 10. Fora de escopo

- CI/CD (GitHub Actions) — adicionado futuramente
- Testes cross-browser (Firefox, Safari)
- Testes de performance / acessibilidade
- Cobertura do fluxo Apple Sign-In
