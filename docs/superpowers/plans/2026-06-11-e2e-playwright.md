# E2E Playwright Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar testes E2E com Playwright cobrindo os 4 fluxos críticos do Horafy (onboarding, booking, admin-workflow, fidelidade), rodando localmente via docker-compose com pagamento mockado e tenant isolado por suite.

**Architecture:** `docker-compose.e2e.yml` sobe todo o stack (infra + API com `PAYMENT_GATEWAY=fake` + Next.js); `playwright.config.ts` gerencia o ciclo de vida via `globalSetup`/`globalTeardown`; cada spec file cria um tenant isolado via `POST /api/v1/platform/tenants` no `beforeAll`; autenticação é injetada via localStorage (Zustand persist) antes de cada navegação. Um endpoint `POST /api/v1/customers/auth/test-login` (ativo só em `E2ETest`) permite criar tokens de cliente sem OAuth.

**Tech Stack:** Playwright 1.x, @playwright/test, Node 20, .NET 9 / MediatR, Next.js 15, Docker Compose v2

---

## Mapa de arquivos

| Arquivo | Ação |
|---|---|
| `frontend/package.json` | Modificar: adicionar script `test:e2e` |
| `frontend/playwright.config.ts` | Criar |
| `frontend/e2e/global-setup.ts` | Criar |
| `frontend/e2e/global-teardown.ts` | Criar |
| `frontend/e2e/helpers/api.ts` | Criar |
| `frontend/e2e/onboarding.spec.ts` | Criar |
| `frontend/e2e/booking.spec.ts` | Criar |
| `frontend/e2e/admin-workflow.spec.ts` | Criar |
| `frontend/e2e/loyalty.spec.ts` | Criar |
| `docker-compose.e2e.yml` | Criar |
| `src/Horafy.Infrastructure/Gateways/FakePaymentGateway.cs` | Criar |
| `src/Horafy.Infrastructure/DependencyInjection.cs` | Modificar: registro condicional do gateway |
| `src/Horafy.Application/Features/Auth/Commands/CustomerTestLogin/CustomerTestLoginCommand.cs` | Criar |
| `src/Horafy.API/Controllers/V1/CustomerAuthController.cs` | Modificar: adicionar endpoint test-login |

---

## Task 1: Instalar Playwright e configurar scripts

**Files:**
- Modify: `frontend/package.json`

- [ ] **Step 1: Instalar @playwright/test**

Na raiz do projeto:
```bash
cd frontend
npm install -D @playwright/test
npx playwright install chromium
```

Saída esperada: `Chromium X.X.X downloaded`

- [ ] **Step 2: Adicionar script no package.json**

Em `frontend/package.json`, adicionar na seção `"scripts"`:
```json
"test:e2e": "playwright test"
```

- [ ] **Step 3: Verificar instalação**

```bash
cd frontend
npx playwright --version
```

Saída esperada: `Version X.X.X`

- [ ] **Step 4: Commit**

```bash
git add frontend/package.json frontend/package-lock.json
git commit -m "chore: install playwright for E2E tests"
```

---

## Task 2: docker-compose.e2e.yml

**Files:**
- Create: `docker-compose.e2e.yml`

- [ ] **Step 1: Criar o arquivo**

Criar `docker-compose.e2e.yml` na raiz do projeto:

```yaml
name: horafy-e2e

services:
  postgres:
    image: postgres:16-alpine
    container_name: horafy-e2e-postgres
    environment:
      POSTGRES_DB: horafy
      POSTGRES_USER: horafy
      POSTGRES_PASSWORD: horafy_e2e_pass
      POSTGRES_INITDB_ARGS: "--encoding=UTF-8"
    ports:
      - "5434:5432"
    volumes:
      - ./infra/postgres/init.sql:/docker-entrypoint-initdb.d/01-init.sql:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U horafy -d horafy"]
      interval: 5s
      timeout: 5s
      retries: 10
    networks:
      - horafy-e2e-net

  redis:
    image: redis:7-alpine
    container_name: horafy-e2e-redis
    command: redis-server --requirepass horafy_e2e_redis --maxmemory 128mb --maxmemory-policy allkeys-lru
    ports:
      - "6381:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "-a", "horafy_e2e_redis", "ping"]
      interval: 5s
      timeout: 5s
      retries: 10
    networks:
      - horafy-e2e-net

  rabbitmq:
    image: rabbitmq:3.13-management-alpine
    container_name: horafy-e2e-rabbitmq
    environment:
      RABBITMQ_DEFAULT_USER: horafy
      RABBITMQ_DEFAULT_PASS: horafy_e2e_rabbit
      RABBITMQ_DEFAULT_VHOST: horafy
    ports:
      - "5674:5672"
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "ping"]
      interval: 10s
      timeout: 10s
      retries: 10
    networks:
      - horafy-e2e-net

  api:
    build:
      context: .
      dockerfile: src/Horafy.API/Dockerfile
    container_name: horafy-e2e-api
    environment:
      ASPNETCORE_ENVIRONMENT: E2ETest
      PAYMENT_GATEWAY: fake
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=horafy;Username=horafy;Password=horafy_e2e_pass"
      Jwt__Secret: "e2e-test-secret-key-at-least-32-chars!!"
      Jwt__Issuer: "horafy-e2e"
      Jwt__Audience: "horafy-e2e"
      Jwt__ExpirationMinutes: "60"
      Jwt__RefreshTokenExpirationDays: "7"
      Redis__ConnectionString: "redis:6379,password=horafy_e2e_redis"
      RabbitMq__Host: "rabbitmq"
      RabbitMq__VirtualHost: "horafy"
      RabbitMq__Username: "horafy"
      RabbitMq__Password: "horafy_e2e_rabbit"
      MercadoPago__AccessToken: "fake-token"
    ports:
      - "8084:8080"
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:8080/health || exit 1"]
      interval: 10s
      timeout: 10s
      retries: 12
    networks:
      - horafy-e2e-net

  frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
      args:
        NEXT_PUBLIC_API_URL: http://localhost:8084
        NEXT_PUBLIC_GOOGLE_CLIENT_ID: e2e-fake-client-id
    container_name: horafy-e2e-frontend
    ports:
      - "3001:3000"
    depends_on:
      api:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:3000 || exit 1"]
      interval: 10s
      timeout: 10s
      retries: 12
    networks:
      - horafy-e2e-net

networks:
  horafy-e2e-net:
    driver: bridge
```

> **Notas de porta:** usa portas diferentes do docker-compose.yml (5434, 6381, 5674, 8084, 3001) para não conflitar com o ambiente de desenvolvimento rodando simultaneamente.

- [ ] **Step 2: Commit**

```bash
git add docker-compose.e2e.yml
git commit -m "chore: add docker-compose.e2e.yml for E2E test environment"
```

---

## Task 3: FakePaymentGateway

**Files:**
- Create: `src/Horafy.Infrastructure/Gateways/FakePaymentGateway.cs`
- Modify: `src/Horafy.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Criar FakePaymentGateway.cs**

```csharp
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;

namespace Horafy.Infrastructure.Gateways;

/// <summary>Gateway de pagamento para testes E2E. Ativo quando PAYMENT_GATEWAY=fake.</summary>
internal sealed class FakePaymentGateway : IPaymentGateway
{
    public Task<PaymentPreferenceResult> CreatePreferenceAsync(
        CreatePaymentPreferenceRequest request, CancellationToken ct = default) =>
        Task.FromResult(new PaymentPreferenceResult(
            PreferenceId: $"fake-{request.BookingId}",
            PaymentUrl: string.Empty,   // string vazia → frontend não redireciona para MP
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1)));

    public Task<PaymentStatusResult> GetPaymentStatusAsync(
        string mpPaymentId, CancellationToken ct = default) =>
        Task.FromResult(new PaymentStatusResult(
            MpPaymentId: mpPaymentId,
            PreferenceId: mpPaymentId,
            Status: PaymentStatus.Approved,
            PaidAt: DateTimeOffset.UtcNow));

    public Task<RefundResult> RefundAsync(
        string mpPaymentId, decimal amount, CancellationToken ct = default) =>
        Task.FromResult(new RefundResult(true, null));

    public bool ValidateWebhookSignature(
        string mpPaymentId, string requestId, string xSignature) => true;
}
```

- [ ] **Step 2: Modificar DependencyInjection.cs — registro condicional**

Em `src/Horafy.Infrastructure/DependencyInjection.cs`, substituir o bloco de registro do `MercadoPagoPaymentGateway`:

```csharp
// DE:
services.Configure<MercadoPagoOptions>(configuration.GetSection(MercadoPagoOptions.SectionName));
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

// PARA:
services.Configure<MercadoPagoOptions>(configuration.GetSection(MercadoPagoOptions.SectionName));
if (Environment.GetEnvironmentVariable("PAYMENT_GATEWAY") == "fake")
{
    services.AddScoped<IPaymentGateway, FakePaymentGateway>();
}
else
{
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
}
```

- [ ] **Step 3: Compilar**

```bash
dotnet build src/Horafy.Infrastructure
```

Saída esperada: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 4: Rodar testes unitários para garantir que nada quebrou**

```bash
dotnet test tests/Horafy.Application.Tests --no-build -c Debug
```

Saída esperada: `Passed! – Failed: 0`

- [ ] **Step 5: Commit**

```bash
git add src/Horafy.Infrastructure/Gateways/FakePaymentGateway.cs src/Horafy.Infrastructure/DependencyInjection.cs
git commit -m "feat: add FakePaymentGateway for E2E testing"
```

---

## Task 4: Endpoint test-login para clientes

**Files:**
- Create: `src/Horafy.Application/Features/Auth/Commands/CustomerTestLogin/CustomerTestLoginCommand.cs`
- Modify: `src/Horafy.API/Controllers/V1/CustomerAuthController.cs`

- [ ] **Step 1: Criar CustomerTestLoginCommand.cs**

Criar `src/Horafy.Application/Features/Auth/Commands/CustomerTestLogin/CustomerTestLoginCommand.cs`:

```csharp
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Auth.Commands.CustomerTestLogin;

/// <summary>Cria ou recupera um cliente de teste sem OAuth. Só deve ser exposto em E2ETest.</summary>
public sealed record CustomerTestLoginCommand(string Email, string TenantSlug) : IRequest<Result<TokenPair>>;

internal sealed class CustomerTestLoginCommandHandler(
    IUserRepository userRepository,
    ITenantRepository tenantRepository,
    ITokenService tokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<CustomerTestLoginCommand, Result<TokenPair>>
{
    public async Task<Result<TokenPair>> Handle(
        CustomerTestLoginCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetBySlugAsync(request.TenantSlug, cancellationToken);
        if (tenant is null)
            return Result.Failure<TokenPair>(AuthErrors.TenantNotFound);

        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null)
        {
            user = User.CreateWithEmail(
                request.Email,
                passwordHash: "e2e-placeholder",
                name: request.Email.Split('@')[0],
                tenantId: tenant.Id,
                role: UserRole.Customer);

            userRepository.Add(user);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(tokenService.GenerateTokens(user));
    }
}
```

- [ ] **Step 2: Adicionar endpoint em CustomerAuthController.cs**

Em `src/Horafy.API/Controllers/V1/CustomerAuthController.cs`, adicionar o using e o novo endpoint:

```csharp
using Horafy.Application.Features.Auth.Commands.CustomerTestLogin;
```

Adicionar o método ao controlador (após o método `Apple`):

```csharp
/// <summary>
/// Login de teste para clientes — cria ou recupera usuário sem OAuth.
/// Retorna 404 fora do ambiente E2ETest.
/// </summary>
[HttpPost("test-login")]
[AllowAnonymous]
[ProducesResponseType(typeof(TokenPair), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> TestLogin(
    [FromBody] CustomerTestLoginRequest request, CancellationToken ct)
{
    if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "E2ETest")
        return NotFound();

    return ToActionResult(await Sender.Send(
        new CustomerTestLoginCommand(request.Email, request.TenantSlug), ct));
}
```

Adicionar o DTO no final do arquivo:

```csharp
public sealed record CustomerTestLoginRequest(string Email, string TenantSlug);
```

- [ ] **Step 3: Compilar**

```bash
dotnet build src/Horafy.API
```

Saída esperada: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 4: Commit**

```bash
git add src/Horafy.Application/Features/Auth/Commands/CustomerTestLogin/CustomerTestLoginCommand.cs \
        src/Horafy.API/Controllers/V1/CustomerAuthController.cs
git commit -m "feat: add customer test-login endpoint for E2E (E2ETest env only)"
```

---

## Task 5: playwright.config.ts + globalSetup + globalTeardown

**Files:**
- Create: `frontend/playwright.config.ts`
- Create: `frontend/e2e/global-setup.ts`
- Create: `frontend/e2e/global-teardown.ts`

- [ ] **Step 1: Criar playwright.config.ts**

Criar `frontend/playwright.config.ts`:

```ts
import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  expect: { timeout: 10_000 },
  retries: 1,
  workers: 1,          // suites rodam em série (cada uma cria seu próprio tenant)
  globalSetup: './e2e/global-setup.ts',
  globalTeardown: './e2e/global-teardown.ts',
  use: {
    baseURL: 'http://localhost:3001',
    navigationTimeout: 15_000,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
})
```

> **Nota:** `baseURL: http://localhost:3001` aponta para o frontend do `docker-compose.e2e.yml` (porta 3001). A API fica em `http://localhost:8084`.

- [ ] **Step 2: Criar global-setup.ts**

Criar `frontend/e2e/global-setup.ts`:

```ts
import { execSync } from 'child_process'
import path from 'path'

const ROOT = path.resolve(__dirname, '../..')

export default async function globalSetup() {
  console.log('[E2E] Subindo docker-compose.e2e.yml...')
  execSync('docker compose -f docker-compose.e2e.yml up -d --build --wait', {
    stdio: 'inherit',
    timeout: 300_000,   // 5 min — next build pode ser lento
    cwd: ROOT,
  })
  console.log('[E2E] Stack pronto.')
}
```

- [ ] **Step 3: Criar global-teardown.ts**

Criar `frontend/e2e/global-teardown.ts`:

```ts
import { execSync } from 'child_process'
import path from 'path'

const ROOT = path.resolve(__dirname, '../..')

export default async function globalTeardown() {
  console.log('[E2E] Derrubando docker-compose.e2e.yml...')
  execSync('docker compose -f docker-compose.e2e.yml down -v', {
    stdio: 'inherit',
    cwd: ROOT,
  })
  console.log('[E2E] Stack removido.')
}
```

- [ ] **Step 4: Commit**

```bash
git add frontend/playwright.config.ts frontend/e2e/global-setup.ts frontend/e2e/global-teardown.ts
git commit -m "feat: add playwright config and global setup/teardown"
```

---

## Task 6: e2e/helpers/api.ts

**Files:**
- Create: `frontend/e2e/helpers/api.ts`

- [ ] **Step 1: Criar o arquivo com todos os helpers**

Criar `frontend/e2e/helpers/api.ts`:

```ts
// Aponta para a API do docker-compose.e2e.yml (porta 8084)
const API = 'http://localhost:8084/api/v1'

// ── Tipos ────────────────────────────────────────────────────────────────────

export interface TenantSetup {
  tenantId: string
  slug: string
  ownerToken: string
  ownerEmail: string
  ownerName: string
}

export interface CustomerSetup {
  customerId: string
  customerToken: string
  customerEmail: string
  customerName: string
}

// ── Helpers internos ─────────────────────────────────────────────────────────

async function post(url: string, body: unknown, token?: string, slug?: string) {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (token) headers['Authorization'] = `Bearer ${token}`
  if (slug) headers['X-Tenant-Slug'] = slug
  const res = await fetch(url, { method: 'POST', headers, body: JSON.stringify(body) })
  if (!res.ok) {
    const text = await res.text().catch(() => '')
    throw new Error(`POST ${url} → ${res.status}: ${text}`)
  }
  // 204 No Content não tem body
  if (res.status === 204) return null
  return res.json()
}

async function put(url: string, body: unknown, token: string, slug: string) {
  const res = await fetch(url, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`,
      'X-Tenant-Slug': slug,
    },
    body: JSON.stringify(body),
  })
  if (!res.ok) {
    const text = await res.text().catch(() => '')
    throw new Error(`PUT ${url} → ${res.status}: ${text}`)
  }
}

// ── API pública ───────────────────────────────────────────────────────────────

/** Cria tenant + TenantOwner. Retorna credenciais do owner. */
export async function setupTenant(slug: string): Promise<TenantSetup> {
  const ownerEmail = `owner-${slug}@e2e.test`
  const ownerName = `Owner ${slug}`
  const data = await post(`${API}/platform/tenants`, {
    name: `Tenant ${slug}`,
    slug,
    vertical: 'Barbershop',
    ownerName,
    ownerEmail,
    ownerPassword: 'E2eTest1',
  })
  return {
    tenantId: data.tenantId,
    slug: data.slug,
    ownerToken: data.tokens.accessToken,
    ownerEmail,
    ownerName,
  }
}

/**
 * Cria serviço no tenant. Retorna o id criado.
 * O endpoint retorna o Guid diretamente (não um objeto): `"550e8400-..."`
 */
export async function createService(
  token: string,
  slug: string,
  opts: { name: string; durationMinutes: number; price: number }
): Promise<string> {
  const id = await post(`${API}/services`, {
    name: opts.name,
    durationMinutes: opts.durationMinutes,
    price: opts.price,
    description: null,
    category: null,
  }, token, slug)
  return id as string
}

/**
 * Cria recurso (profissional) no tenant. Retorna o id criado.
 * O endpoint retorna o Guid diretamente (não um objeto): `"550e8400-..."`
 */
export async function createResource(
  token: string,
  slug: string,
  name: string
): Promise<string> {
  const id = await post(`${API}/resources`, {
    name,
    type: 'Professional',
    email: null,
    phone: null,
    specialty: null,
    bio: null,
    avatarUrl: null,
    userId: null,
  }, token, slug)
  return id as string
}

/** Vincula serviço a recurso. */
export async function linkServiceToResource(
  token: string,
  slug: string,
  resourceId: string,
  serviceId: string
): Promise<void> {
  await post(`${API}/resources/${resourceId}/services/${serviceId}`, {}, token, slug)
}

/**
 * Define horários de funcionamento seg–sex 08:00–18:00.
 * Sábado e domingo ficam fechados (isOpen: false).
 */
export async function setBusinessHoursWeekdays(token: string, slug: string): Promise<void> {
  const days = [
    { day: 1, open: true },   // Monday
    { day: 2, open: true },   // Tuesday
    { day: 3, open: true },   // Wednesday
    { day: 4, open: true },   // Thursday
    { day: 5, open: true },   // Friday
    { day: 6, open: false },  // Saturday
    { day: 0, open: false },  // Sunday
  ]
  for (const { day, open } of days) {
    await put(`${API}/availability/business-hours`, {
      dayOfWeek: day,
      openTime: '08:00:00',
      closeTime: '18:00:00',
      isOpen: open,
    }, token, slug)
  }
}

/**
 * Cria/recupera cliente de teste via endpoint exclusivo de E2ETest.
 * Retorna credenciais do cliente.
 */
export async function customerTestLogin(
  email: string,
  slug: string
): Promise<CustomerSetup> {
  const data = await post(`${API}/customers/auth/test-login`, { email, tenantSlug: slug })
  // Decodifica o JWT para pegar o id (payload é base64url, campo "sub")
  const payload = JSON.parse(
    Buffer.from(data.accessToken.split('.')[1], 'base64url').toString('utf-8')
  )
  const name = email.split('@')[0]
  return {
    customerId: payload.sub,
    customerToken: data.accessToken,
    customerEmail: email,
    customerName: name,
  }
}

/** Ativa fidelidade no tenant com taxa percentual. */
export async function setLoyalty(
  token: string,
  slug: string,
  creditRatePercent: number
): Promise<void> {
  await put(`${API}/tenants/loyalty-settings`, {
    isEnabled: true,
    creditRatePercent,
    minBookingAmount: 0,
  }, token, slug)
}

/** Confirma agendamento (ação de admin/staff). */
export async function confirmBooking(
  token: string,
  slug: string,
  bookingId: string
): Promise<void> {
  const res = await fetch(`${API}/bookings/${bookingId}/confirm`, {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}`, 'X-Tenant-Slug': slug },
  })
  if (!res.ok) throw new Error(`confirmBooking → ${res.status}`)
}

/** Marca agendamento como Concluído (ação de admin/staff). */
export async function completeBooking(
  token: string,
  slug: string,
  bookingId: string
): Promise<void> {
  const res = await fetch(`${API}/bookings/${bookingId}/complete`, {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}`, 'X-Tenant-Slug': slug },
  })
  if (!res.ok) throw new Error(`completeBooking → ${res.status}`)
}

// ── StorageState helpers ──────────────────────────────────────────────────────

/**
 * Retorna o storageState do Playwright para autenticar o admin.
 * Injeta o Zustand store `horafy-auth` no localStorage.
 */
export function adminStorageState(setup: TenantSetup): object {
  const state = {
    user: { id: 'e2e-owner', name: setup.ownerName, email: setup.ownerEmail, role: 'TenantOwner' },
    accessToken: setup.ownerToken,
    refreshToken: '',
    tenantSlug: setup.slug,
  }
  return {
    cookies: [],
    origins: [{
      origin: 'http://localhost:3001',
      localStorage: [
        { name: 'horafy-auth', value: JSON.stringify({ state, version: 0 }) },
      ],
    }],
  }
}

/**
 * Retorna o storageState do Playwright para autenticar o cliente portal.
 * Injeta o Zustand store `horafy-portal-auth` no localStorage.
 */
export function customerStorageState(setup: CustomerSetup): object {
  const state = {
    customer: { id: setup.customerId, name: setup.customerName, email: setup.customerEmail },
    accessToken: setup.customerToken,
  }
  return {
    cookies: [],
    origins: [{
      origin: 'http://localhost:3001',
      localStorage: [
        { name: 'horafy-portal-auth', value: JSON.stringify({ state, version: 0 }) },
      ],
    }],
  }
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/e2e/helpers/api.ts
git commit -m "feat: add E2E API helpers for tenant setup and auth injection"
```

---

## Task 7: e2e/onboarding.spec.ts

**Files:**
- Create: `frontend/e2e/onboarding.spec.ts`

Testa o wizard de onboarding (5 passos) após o tenant ser criado. Setup via API — só o registro do tenant. O login do owner é feito via UI (único spec que testa a tela de login).

- [ ] **Step 1: Criar o spec**

Criar `frontend/e2e/onboarding.spec.ts`:

```ts
import { test, expect } from '@playwright/test'
import { setupTenant } from './helpers/api'

test.describe('Onboarding wizard', () => {
  const slug = `e2e-onboarding-${Date.now()}`
  let ownerEmail: string
  let ownerToken: string

  test.beforeAll(async () => {
    const setup = await setupTenant(slug)
    ownerEmail = setup.ownerEmail
    // senha definida em setupTenant
  })

  test('owner faz login e conclui o wizard de 5 passos', async ({ page }) => {
    // 1. Login via UI
    await page.goto('/login')
    await page.getByLabel(/e-mail/i).fill(ownerEmail)
    await page.getByLabel(/senha/i).fill('E2eTest1')
    await page.getByRole('button', { name: /entrar/i }).click()

    // 2. Após login deve ir para /admin/onboarding ou /admin/dashboard
    await page.waitForURL(/\/admin/)

    // Se caiu direto no dashboard (onboarding já completo não é possível em tenant novo)
    // redirecionar explicitamente
    if (!page.url().includes('onboarding')) {
      await page.goto('/admin/onboarding')
    }
    await expect(page).toHaveURL(/onboarding/)

    // 3. Passo 1 — Identidade visual
    await expect(page.getByText(/identidade visual|nome do negócio/i)).toBeVisible()
    await page.getByRole('button', { name: /próximo|avançar/i }).click()

    // 4. Passo 2 — Serviço
    await expect(page.getByText(/serviço|adicionar serviço/i)).toBeVisible()
    await page.getByRole('button', { name: /próximo|avançar|pular/i }).click()

    // 5. Passo 3 — Recurso
    await expect(page.getByText(/recurso|profissional|adicionar/i)).toBeVisible()
    await page.getByRole('button', { name: /próximo|avançar|pular/i }).click()

    // 6. Passo 4 — Horários
    await expect(page.getByText(/horário|funcionamento/i)).toBeVisible()
    await page.getByRole('button', { name: /próximo|avançar|pular/i }).click()

    // 7. Passo 5 — Conclusão
    await expect(page.getByText(/concluído|parabéns|pronto/i)).toBeVisible()
    await page.getByRole('button', { name: /ir para o painel|dashboard|concluir/i }).click()

    // 8. Dashboard carregado
    await page.waitForURL(/\/admin\/dashboard/)
    await expect(page).toHaveURL(/\/admin\/dashboard/)
  })
})
```

- [ ] **Step 2: Commit**

```bash
git add frontend/e2e/onboarding.spec.ts
git commit -m "feat: add E2E onboarding wizard spec"
```

---

## Task 8: e2e/booking.spec.ts

**Files:**
- Create: `frontend/e2e/booking.spec.ts`

Testa o fluxo completo do portal: cliente autentica, passa pelo wizard de agendamento, cria o booking, e admin confirma via API. A página de status deve refletir `Confirmado`.

- [ ] **Step 1: Criar o spec**

Criar `frontend/e2e/booking.spec.ts`:

```ts
import { test, expect } from '@playwright/test'
import {
  setupTenant, createService, createResource, linkServiceToResource,
  setBusinessHoursWeekdays, customerTestLogin, confirmBooking,
  adminStorageState, customerStorageState,
} from './helpers/api'

test.describe('Portal booking flow', () => {
  const slug = `e2e-booking-${Date.now()}`
  let serviceId: string
  let resourceId: string
  let ownerToken: string
  let customerSetup: Awaited<ReturnType<typeof customerTestLogin>>

  test.beforeAll(async () => {
    const tenant = await setupTenant(slug)
    ownerToken = tenant.ownerToken

    serviceId = await createService(ownerToken, slug, {
      name: 'Corte de cabelo',
      durationMinutes: 60,
      price: 80,
    })
    resourceId = await createResource(ownerToken, slug, 'João Barbeiro')
    await linkServiceToResource(ownerToken, slug, resourceId, serviceId)
    await setBusinessHoursWeekdays(ownerToken, slug)

    customerSetup = await customerTestLogin(`cliente-${slug}@e2e.test`, slug)
  })

  test('cliente navega pelo wizard e agendamento fica Confirmado', async ({ browser }) => {
    // Contexto com auth do cliente no localStorage
    const ctx = await browser.newContext({
      storageState: customerStorageState(customerSetup) as any,
    })
    const page = await ctx.newPage()

    // 1. Abre portal e inicia wizard
    await page.goto(`/${slug}/agendar`)
    await expect(page.getByText('Corte de cabelo')).toBeVisible()
    await page.getByText('Corte de cabelo').click()
    await page.getByRole('button', { name: /próximo/i }).click()

    // 2. Escolhe recurso
    await expect(page.getByText('João Barbeiro')).toBeVisible()
    await page.getByText('João Barbeiro').click()
    await page.getByRole('button', { name: /próximo/i }).click()

    // 3. Seleciona o primeiro slot disponível
    const slotButton = page.getByRole('button', { name: /^\d{2}:\d{2}$/ }).first()
    await expect(slotButton).toBeVisible({ timeout: 15_000 })
    await slotButton.click()
    await page.getByRole('button', { name: /próximo/i }).click()

    // 4. Confirma no step final
    const confirmBtn = page.getByRole('button', { name: /confirmar/i })
    await expect(confirmBtn).toBeVisible()
    await confirmBtn.click()

    // 5. Aguarda navegação para página de status
    await page.waitForURL(/\/agendar\/.+\/status/, { timeout: 15_000 })

    // Extrai bookingId da URL
    const url = page.url()
    const bookingId = url.match(/agendar\/([^/]+)\/status/)?.[1]
    expect(bookingId).toBeTruthy()

    // 6. Admin confirma via API
    await confirmBooking(ownerToken, slug, bookingId!)

    // 7. Recarrega status page
    await page.reload()
    await expect(page.getByText('Confirmado')).toBeVisible({ timeout: 10_000 })

    await ctx.close()
  })
})
```

- [ ] **Step 2: Commit**

```bash
git add frontend/e2e/booking.spec.ts
git commit -m "feat: add E2E booking wizard spec"
```

---

## Task 9: e2e/admin-workflow.spec.ts

**Files:**
- Create: `frontend/e2e/admin-workflow.spec.ts`

Dois atores: admin confirma agendamento via UI, cliente cancela via portal, admin vê reembolso no financeiro.

- [ ] **Step 1: Criar o spec**

Criar `frontend/e2e/admin-workflow.spec.ts`:

```ts
import { test, expect } from '@playwright/test'
import {
  setupTenant, createService, createResource, linkServiceToResource,
  setBusinessHoursWeekdays, customerTestLogin, confirmBooking,
  adminStorageState, customerStorageState,
} from './helpers/api'

test.describe('Admin workflow: confirm → customer cancel → refund', () => {
  const slug = `e2e-workflow-${Date.now()}`
  let ownerToken: string
  let tenantSetup: Awaited<ReturnType<typeof setupTenant>>
  let customerSetup: Awaited<ReturnType<typeof customerTestLogin>>
  let bookingId: string

  test.beforeAll(async ({ browser }) => {
    tenantSetup = await setupTenant(slug)
    ownerToken = tenantSetup.ownerToken

    const serviceId = await createService(ownerToken, slug, {
      name: 'Massagem',
      durationMinutes: 60,
      price: 120,
    })
    const resourceId = await createResource(ownerToken, slug, 'Ana Massagista')
    await linkServiceToResource(ownerToken, slug, resourceId, serviceId)
    await setBusinessHoursWeekdays(ownerToken, slug)

    customerSetup = await customerTestLogin(`cliente-workflow-${slug}@e2e.test`, slug)

    // Cliente cria booking via wizard para ter um booking associado ao usuário
    const ctx = await browser.newContext({
      storageState: customerStorageState(customerSetup) as any,
    })
    const page = await ctx.newPage()
    await page.goto(`/${slug}/agendar`)
    await page.getByText('Massagem').click()
    await page.getByRole('button', { name: /próximo/i }).click()
    await page.getByText('Ana Massagista').click()
    await page.getByRole('button', { name: /próximo/i }).click()
    const slot = page.getByRole('button', { name: /^\d{2}:\d{2}$/ }).first()
    await slot.waitFor({ timeout: 15_000 })
    await slot.click()
    await page.getByRole('button', { name: /próximo/i }).click()
    await page.getByRole('button', { name: /confirmar/i }).click()
    await page.waitForURL(/\/agendar\/.+\/status/, { timeout: 15_000 })
    const url = page.url()
    bookingId = url.match(/agendar\/([^/]+)\/status/)?.[1] ?? ''
    expect(bookingId).toBeTruthy()
    await ctx.close()
  })

  test('admin confirma agendamento via UI', async ({ browser }) => {
    const ctx = await browser.newContext({
      storageState: adminStorageState(tenantSetup) as any,
    })
    const page = await ctx.newPage()
    await page.goto('/admin/agendamentos')

    // Acha o botão de confirmar na linha do agendamento
    const row = page.locator('tr', { hasText: 'Massagem' }).first()
    await expect(row).toBeVisible({ timeout: 10_000 })
    await row.getByRole('button', { name: /confirmar/i }).click()

    // Status na tabela muda para Confirmado
    await expect(row.getByText(/confirmado/i)).toBeVisible({ timeout: 10_000 })
    await ctx.close()
  })

  test('cliente cancela agendamento via portal', async ({ browser }) => {
    const ctx = await browser.newContext({
      storageState: customerStorageState(customerSetup) as any,
    })
    const page = await ctx.newPage()
    await page.goto(`/${slug}/minha-conta`)

    // Acha o agendamento e clica em cancelar
    const bookingCard = page.locator('[data-booking-id], .booking-item').first()
    await expect(bookingCard).toBeVisible({ timeout: 10_000 })
    await page.getByRole('button', { name: /cancelar/i }).first().click()

    // Confirmação de cancelamento (pode ser um dialog)
    const confirmDialog = page.getByRole('dialog')
    if (await confirmDialog.isVisible()) {
      await confirmDialog.getByRole('button', { name: /confirmar|sim/i }).click()
    }

    // Agendamento some da lista de "próximos"
    await expect(page.getByText('Massagem')).not.toBeVisible({ timeout: 10_000 })
    await ctx.close()
  })

  test('admin vê reembolso no financeiro', async ({ browser }) => {
    const ctx = await browser.newContext({
      storageState: adminStorageState(tenantSetup) as any,
    })
    const page = await ctx.newPage()
    await page.goto('/admin/financeiro')

    // Seção de reembolsos ou transações negativas deve estar visível
    await expect(page.getByText(/reembolso|cancelamento/i)).toBeVisible({ timeout: 10_000 })
    await ctx.close()
  })
})
```

- [ ] **Step 2: Commit**

```bash
git add frontend/e2e/admin-workflow.spec.ts
git commit -m "feat: add E2E admin workflow spec (confirm, cancel, refund)"
```

---

## Task 10: e2e/loyalty.spec.ts

**Files:**
- Create: `frontend/e2e/loyalty.spec.ts`

Admin marca agendamento como Concluído, programa de fidelidade credita a carteira do cliente.

- [ ] **Step 1: Criar o spec**

Criar `frontend/e2e/loyalty.spec.ts`:

```ts
import { test, expect } from '@playwright/test'
import {
  setupTenant, createService, createResource, linkServiceToResource,
  setBusinessHoursWeekdays, customerTestLogin, setLoyalty, confirmBooking,
  adminStorageState, customerStorageState,
} from './helpers/api'

test.describe('Loyalty: booking concluído credita carteira', () => {
  const slug = `e2e-loyalty-${Date.now()}`
  let ownerToken: string
  let tenantSetup: Awaited<ReturnType<typeof setupTenant>>
  let customerSetup: Awaited<ReturnType<typeof customerTestLogin>>
  let bookingId: string

  test.beforeAll(async ({ browser }) => {
    tenantSetup = await setupTenant(slug)
    ownerToken = tenantSetup.ownerToken

    // Fidelidade: 10% do valor em créditos
    await setLoyalty(ownerToken, slug, 10)

    const serviceId = await createService(ownerToken, slug, {
      name: 'Manicure',
      durationMinutes: 45,
      price: 60,
    })
    const resourceId = await createResource(ownerToken, slug, 'Carla Manicure')
    await linkServiceToResource(ownerToken, slug, resourceId, serviceId)
    await setBusinessHoursWeekdays(ownerToken, slug)

    customerSetup = await customerTestLogin(`cliente-loyalty-${slug}@e2e.test`, slug)

    // Cliente cria booking
    const ctx = await browser.newContext({
      storageState: customerStorageState(customerSetup) as any,
    })
    const page = await ctx.newPage()
    await page.goto(`/${slug}/agendar`)
    await page.getByText('Manicure').click()
    await page.getByRole('button', { name: /próximo/i }).click()
    await page.getByText('Carla Manicure').click()
    await page.getByRole('button', { name: /próximo/i }).click()
    const slot = page.getByRole('button', { name: /^\d{2}:\d{2}$/ }).first()
    await slot.waitFor({ timeout: 15_000 })
    await slot.click()
    await page.getByRole('button', { name: /próximo/i }).click()
    await page.getByRole('button', { name: /confirmar/i }).click()
    await page.waitForURL(/\/agendar\/.+\/status/, { timeout: 15_000 })
    const url = page.url()
    bookingId = url.match(/agendar\/([^/]+)\/status/)?.[1] ?? ''
    expect(bookingId).toBeTruthy()
    await ctx.close()

    // Admin confirma via API (pré-condição para poder completar)
    await confirmBooking(ownerToken, slug, bookingId)
  })

  test('admin marca como Concluído via UI', async ({ browser }) => {
    const ctx = await browser.newContext({
      storageState: adminStorageState(tenantSetup) as any,
    })
    const page = await ctx.newPage()
    await page.goto('/admin/agendamentos')

    const row = page.locator('tr', { hasText: 'Manicure' }).first()
    await expect(row).toBeVisible({ timeout: 10_000 })
    await row.getByRole('button', { name: /concluir|concluído/i }).click()

    await expect(row.getByText(/concluído/i)).toBeVisible({ timeout: 10_000 })
    await ctx.close()
  })

  test('carteira do cliente mostra crédito de fidelidade (R$ 6,00 = 10% de R$ 60)', async ({ browser }) => {
    const ctx = await browser.newContext({
      storageState: customerStorageState(customerSetup) as any,
    })
    const page = await ctx.newPage()
    await page.goto(`/${slug}/minha-conta`)

    // Abre aba Carteira
    await page.getByRole('tab', { name: /carteira/i }).click()

    // Saldo deve ser R$ 6,00 (10% de R$ 60)
    await expect(page.getByText(/r\$\s*6[,.]00|saldo.*6/i)).toBeVisible({ timeout: 10_000 })
    await ctx.close()
  })
})
```

- [ ] **Step 2: Commit**

```bash
git add frontend/e2e/loyalty.spec.ts
git commit -m "feat: add E2E loyalty spec (complete booking credits wallet)"
```

---

## Como rodar

```bash
# Rodar todos os specs (sobe e derruba o stack automaticamente)
cd frontend
npx playwright test

# Rodar um spec específico
npx playwright test e2e/booking.spec.ts

# Modo interativo com UI do Playwright
npx playwright test --ui

# Ver relatório após a execução
npx playwright show-report
```

> **Primeira execução:** o `globalSetup` faz `docker compose up --build`, que inclui `next build`. Espere 3–5 minutos. Execuções seguintes reusam o build em cache e são mais rápidas.

---

## Notas de implementação

**Por que `workers: 1`?** Cada spec cria seu próprio tenant, mas o banco é compartilhado. Rodar em paralelo causaria conflitos de porta no docker-compose e corrida no banco. Com `workers: 1` os specs rodam em série, cada um com seu tenant isolado.

**Por que `storageState` via localStorage?** O admin e o portal cliente usam Zustand com `persist` (chaves `horafy-auth` e `horafy-portal-auth`). O Playwright suporta injetar localStorage via `storageState`, que é aplicado antes de qualquer script da página carregar.

**Por que FakePaymentGateway retorna `paymentUrl = ""`?** O `BookingWizard.tsx` só redireciona para o MercadoPago se `payment.paymentUrl` for truthy. Com string vazia, o wizard cai direto no `router.push` para a página de status — sem sair da aplicação.

**Seletores dos specs:** Os specs usam seletores de role/text que refletem o que o usuário vê. Se o texto de um botão mudar no frontend, o teste quebra — isso é intencional (testes E2E devem detectar mudanças de UX).
