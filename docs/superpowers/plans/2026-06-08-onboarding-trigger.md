# Onboarding Trigger Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redirecionar automaticamente para `/admin/onboarding` quando um TenantOwner/TenantAdmin faz login sem ter completado o onboarding.

**Architecture:** Adiciona `OnboardingCompletedAt` (nullable) no Tenant. Na conclusão do wizard, o frontend chama `POST /api/v1/tenants/me/onboarding-complete` que seta o campo. No login, o frontend busca o tenant logo após a autenticação e redireciona para `/admin/onboarding` se o campo for nulo.

**Tech Stack:** .NET 8 (MediatR, EF Core, xUnit/Moq), Next.js 16 (React Hook Form, Vitest/Testing Library)

---

## Arquivos criados/modificados

| Ação | Arquivo |
|------|---------|
| Modify | `src/Horafy.Domain/Entities/Tenants/Tenant.cs` |
| Modify | `src/Horafy.Application/Features/Tenants/Queries/GetCurrentTenant/GetCurrentTenantQuery.cs` |
| Create | `src/Horafy.Application/Features/Tenants/Commands/CompleteOnboarding/CompleteOnboardingCommand.cs` |
| Modify | `src/Horafy.API/Controllers/V1/TenantsController.cs` |
| Generate | `src/Horafy.Infrastructure/Persistence/Migrations/..._AddOnboardingCompletedAt.cs` |
| Modify | `tests/Horafy.Domain.Tests/Entities/TenantTests.cs` |
| Create | `tests/Horafy.Application.Tests/Tenants/CompleteOnboardingCommandHandlerTests.cs` |
| Modify | `frontend/lib/types/tenant.ts` |
| Modify | `frontend/lib/api/tenants.ts` |
| Modify | `frontend/app/(auth)/login/page.tsx` |
| Modify | `frontend/app/(admin)/admin/onboarding/page.tsx` |
| Modify | `frontend/__tests__/login.test.tsx` |

---

## Task 1 — Domínio: propriedade + método no Tenant

**Files:**
- Modify: `src/Horafy.Domain/Entities/Tenants/Tenant.cs`

- [ ] **Step 1.1: Escreva o teste falho**

Em `tests/Horafy.Domain.Tests/Entities/TenantTests.cs`, adicione ao final da classe:

```csharp
[Fact]
public void CompleteOnboarding_SetsOnboardingCompletedAt()
{
    var tenant = Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop);
    tenant.ClearDomainEvents();

    tenant.CompleteOnboarding();

    tenant.OnboardingCompletedAt.Should().NotBeNull();
    tenant.IsOnboardingCompleted.Should().BeTrue();
    tenant.UpdatedAt.Should().NotBeNull();
}

[Fact]
public void IsOnboardingCompleted_ReturnsFalseForNewTenant()
{
    var tenant = Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop);

    tenant.IsOnboardingCompleted.Should().BeFalse();
}
```

- [ ] **Step 1.2: Execute o teste para confirmar que falha**

```bash
dotnet test tests/Horafy.Domain.Tests --filter "CompleteOnboarding|IsOnboardingCompleted" --no-build
```

Resultado esperado: FAIL — `CompleteOnboarding` não existe.

- [ ] **Step 1.3: Implemente no Tenant**

Em `src/Horafy.Domain/Entities/Tenants/Tenant.cs`, adicione após `public LoyaltySettings LoyaltySettings`:

```csharp
public DateTimeOffset? OnboardingCompletedAt { get; private set; }
public bool IsOnboardingCompleted => OnboardingCompletedAt.HasValue;
```

E adicione o método após `UpdateLoyaltySettings`:

```csharp
public void CompleteOnboarding()
{
    OnboardingCompletedAt = DateTimeOffset.UtcNow;
    UpdatedAt             = DateTimeOffset.UtcNow;
}
```

- [ ] **Step 1.4: Execute os testes para confirmar que passam**

```bash
dotnet test tests/Horafy.Domain.Tests --filter "CompleteOnboarding|IsOnboardingCompleted" --no-build
```

Resultado esperado: PASS — 2 testes.

- [ ] **Step 1.5: Build geral para confirmar sem erros**

```bash
dotnet build Horafy.sln -q 2>&1 | grep -iv "warning\|aviso\|info\|NETSDK\|NU190" | tail -5
```

Resultado esperado: `Compilação com êxito.`

- [ ] **Step 1.6: Commit**

```bash
git add src/Horafy.Domain/Entities/Tenants/Tenant.cs tests/Horafy.Domain.Tests/Entities/TenantTests.cs
git commit -m "feat: add OnboardingCompletedAt to Tenant aggregate"
```

---

## Task 2 — Application: CompleteOnboardingCommand

**Files:**
- Create: `src/Horafy.Application/Features/Tenants/Commands/CompleteOnboarding/CompleteOnboardingCommand.cs`

- [ ] **Step 2.1: Escreva o teste falho**

Crie `tests/Horafy.Application.Tests/Tenants/CompleteOnboardingCommandHandlerTests.cs`:

```csharp
using FluentAssertions;
using Horafy.Application.Features.Tenants.Commands.CompleteOnboarding;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Tenants;

public sealed class CompleteOnboardingCommandHandlerTests
{
    private readonly Mock<ITenantRepository>     _repo      = new();
    private readonly Mock<ICurrentTenantService> _tenantSvc = new();
    private readonly Mock<IUnitOfWork>           _uow       = new();

    private CompleteOnboardingCommandHandler MakeHandler() =>
        new(_repo.Object, _tenantSvc.Object, _uow.Object);

    [Fact]
    public async Task Handle_ValidTenant_SetsOnboardingCompletedAndSaves()
    {
        var tenantId = Guid.NewGuid();
        var tenant   = Tenant.Create("T", "t", TenantVertical.Barbershop);

        _tenantSvc.Setup(s => s.TenantId).Returns(tenantId);
        _repo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        var result = await MakeHandler().Handle(new CompleteOnboardingCommand(), default);

        result.IsSuccess.Should().BeTrue();
        tenant.IsOnboardingCompleted.Should().BeTrue();
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_NoTenantContext_ReturnsUnauthorized()
    {
        _tenantSvc.Setup(s => s.TenantId).Returns((Guid?)null);

        var result = await MakeHandler().Handle(new CompleteOnboardingCommand(), default);

        result.IsFailure.Should().BeTrue();
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }
}
```

- [ ] **Step 2.2: Execute o teste para confirmar que falha**

```bash
dotnet test tests/Horafy.Application.Tests --filter "CompleteOnboardingCommandHandler" --no-build
```

Resultado esperado: FAIL — namespace não existe.

- [ ] **Step 2.3: Crie o command e handler**

Crie `src/Horafy.Application/Features/Tenants/Commands/CompleteOnboarding/CompleteOnboardingCommand.cs`:

```csharp
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Commands.CompleteOnboarding;

public sealed record CompleteOnboardingCommand : IRequest<Result>;

internal sealed class CompleteOnboardingCommandHandler(
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenant,
    IUnitOfWork           unitOfWork) : IRequestHandler<CompleteOnboardingCommand, Result>
{
    public async Task<Result> Handle(CompleteOnboardingCommand request, CancellationToken ct)
    {
        if (!currentTenant.TenantId.HasValue)
            return Result.Failure(Error.Unauthorized);

        var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, ct);
        if (tenant is null) return Result.Failure(TenantErrors.NotFound);

        tenant.CompleteOnboarding();
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

- [ ] **Step 2.4: Execute os testes para confirmar que passam**

```bash
dotnet test tests/Horafy.Application.Tests --filter "CompleteOnboardingCommandHandler"
```

Resultado esperado: PASS — 2 testes.

- [ ] **Step 2.5: Commit**

```bash
git add src/Horafy.Application/Features/Tenants/Commands/CompleteOnboarding/ tests/Horafy.Application.Tests/Tenants/CompleteOnboardingCommandHandlerTests.cs
git commit -m "feat: add CompleteOnboardingCommand handler"
```

---

## Task 3 — Infrastructure + API: migration e endpoint

**Files:**
- Modify: `src/Horafy.API/Controllers/V1/TenantsController.cs`
- Generate: migration EF Core

- [ ] **Step 3.1: Gere a migration**

```bash
dotnet ef migrations add AddOnboardingCompletedAt --project src/Horafy.Infrastructure --startup-project src/Horafy.API
```

Resultado esperado: arquivo `..._AddOnboardingCompletedAt.cs` criado em `src/Horafy.Infrastructure/Persistence/Migrations/`.

- [ ] **Step 3.2: Verifique o conteúdo da migration gerada**

Abra o arquivo gerado e confirme que contém:

```csharp
migrationBuilder.AddColumn<DateTimeOffset>(
    name: "onboarding_completed_at",
    schema: "public",
    table: "tenants",
    nullable: true,
    defaultValue: null);
```

Se o `schema: "public"` não estiver presente, adicione manualmente.

- [ ] **Step 3.3: Adicione o endpoint no TenantsController**

Em `src/Horafy.API/Controllers/V1/TenantsController.cs`, adicione o using no topo:

```csharp
using Horafy.Application.Features.Tenants.Commands.CompleteOnboarding;
```

E adicione o endpoint na seção dos endpoints de tenant (após `UpdateLoyaltySettings`):

```csharp
/// <summary>Marca o onboarding do tenant como concluído.</summary>
[HttpPost("/api/v{version:apiVersion}/tenants/me/onboarding-complete")]
[Authorize(Roles = "TenantOwner,TenantAdmin")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
public async Task<IActionResult> CompleteOnboarding(CancellationToken cancellationToken)
{
    var result = await Sender.Send(new CompleteOnboardingCommand(), cancellationToken);
    return result.IsSuccess ? NoContent() : ToActionResult(result);
}
```

- [ ] **Step 3.4: Exponha `IsOnboardingCompleted` no TenantResult**

Em `src/Horafy.Application/Features/Tenants/Queries/GetCurrentTenant/GetCurrentTenantQuery.cs`:

Adicione `bool IsOnboardingCompleted` ao record `TenantResult`:

```csharp
public sealed record TenantResult(
    Guid   Id,
    string Name,
    string Slug,
    string? CustomDomain,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? ZipCode,
    string  TimeZoneId,
    string  Locale,
    TenantStatus   Status,
    TenantPlan     Plan,
    TenantVertical Vertical,
    TenantThemeResult        Theme,
    DateTimeOffset?          TrialEndsAt,
    DateTimeOffset?          PlanRenewsAt,
    CancellationPolicyResult CancellationPolicy,
    LoyaltySettingsResult    LoyaltySettings,
    bool IsOnboardingCompleted);   // ← novo campo
```

Atualize o mapeamento `ToResult` no handler (linha onde constrói o record), adicionando `t.IsOnboardingCompleted` como último argumento:

```csharp
internal static TenantResult ToResult(Domain.Entities.Tenants.Tenant t) => new(
    t.Id, t.Name, t.Slug, t.CustomDomain,
    t.Email, t.Phone, t.Address, t.City, t.State, t.ZipCode,
    t.TimeZoneId, t.Locale, t.Status, t.Plan, t.Vertical,
    new TenantThemeResult(
        t.Theme.PrimaryColor, t.Theme.SecondaryColor,
        t.Theme.BackgroundColor, t.Theme.TextColor, t.Theme.FontFamily,
        t.Theme.LogoUrl, t.Theme.FaviconUrl,
        t.Theme.BannerUrl, t.Theme.BannerText,
        t.Theme.ShowReviews, t.Theme.ShowTeam, t.Theme.ShowServicePrices,
        t.Theme.InstagramUrl, t.Theme.WhatsAppNumber, t.Theme.FacebookUrl,
        t.Theme.SectionsOrder),
    t.TrialEndsAt, t.PlanRenewsAt,
    new CancellationPolicyResult(
        t.CancellationPolicy.MinCancellationHours,
        t.CancellationPolicy.CancellationFeePercent,
        t.CancellationPolicy.AllowCustomerCancellation),
    new LoyaltySettingsResult(
        t.LoyaltySettings.IsEnabled,
        t.LoyaltySettings.CreditRatePercent,
        t.LoyaltySettings.MinBookingAmount),
    t.IsOnboardingCompleted);
```

- [ ] **Step 3.5: Build geral para confirmar sem erros**

```bash
dotnet build Horafy.sln -q 2>&1 | grep -iv "warning\|aviso\|info\|NETSDK\|NU190" | tail -5
```

Resultado esperado: `Compilação com êxito.`

- [ ] **Step 3.6: Execute todos os testes backend**

```bash
dotnet test --no-build 2>&1 | tail -5
```

Resultado esperado: todos os testes passam, 0 falhas.

- [ ] **Step 3.7: Commit**

```bash
git add src/ tests/
git commit -m "feat: expose IsOnboardingCompleted in TenantResult and add POST /tenants/me/onboarding-complete"
```

---

## Task 4 — Frontend: tipos e cliente de API

**Files:**
- Modify: `frontend/lib/types/tenant.ts`
- Modify: `frontend/lib/api/tenants.ts`

- [ ] **Step 4.1: Adicione `isOnboardingCompleted` ao tipo Tenant**

Em `frontend/lib/types/tenant.ts`, atualize a interface `Tenant`:

```typescript
export interface Tenant {
  id: string
  name: string
  slug: string
  logoUrl?: string
  primaryColor?: string
  customDomain?: string
  timezone: string
  plan: string
  cancellationPolicy: CancellationPolicy
  loyaltySettings: LoyaltySettings
  isOnboardingCompleted: boolean   // ← novo campo
}
```

- [ ] **Step 4.2: Adicione `completeOnboarding()` ao cliente**

Em `frontend/lib/api/tenants.ts`, adicione ao objeto `tenantsApi`:

```typescript
export const tenantsApi = {
  me: () => apiFetch<Tenant>('/api/v1/tenants/me'),
  update: (data: UpdateTenantRequest) =>
    apiFetch<void>('/api/v1/tenants/me', { method: 'PUT', body: JSON.stringify(data) }),
  updateTheme: (primaryColor: string, logoUrl?: string) =>
    apiFetch<void>('/api/v1/tenants/me/theme', {
      method: 'PUT',
      body: JSON.stringify({ primaryColor, logoUrl }),
    }),
  updateLoyaltySettings: (data: UpdateLoyaltySettingsRequest) =>
    apiFetch<void>('/api/v1/tenants/loyalty-settings', {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  updateCancellationPolicy: (data: UpdateCancellationPolicyRequest) =>
    apiFetch<void>('/api/v1/tenants/cancellation-policy', {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  completeOnboarding: () =>
    apiFetch<void>('/api/v1/tenants/me/onboarding-complete', { method: 'POST' }),
}
```

- [ ] **Step 4.3: Build TypeScript para confirmar sem erros**

```bash
cd frontend && npx tsc --noEmit 2>&1 | head -20
```

Resultado esperado: sem erros de tipo.

- [ ] **Step 4.4: Commit**

```bash
git add frontend/lib/types/tenant.ts frontend/lib/api/tenants.ts
git commit -m "feat: add isOnboardingCompleted to Tenant type and completeOnboarding to API client"
```

---

## Task 5 — Frontend: página de login

**Files:**
- Modify: `frontend/app/(auth)/login/page.tsx`
- Modify: `frontend/__tests__/login.test.tsx`

- [ ] **Step 5.1: Escreva os testes falhos**

Em `frontend/__tests__/login.test.tsx`, adicione o mock de `tenantsApi` junto aos outros mocks no topo do arquivo:

```typescript
vi.mock('@/lib/api/tenants', () => ({
  tenantsApi: {
    me: vi.fn(),
  },
}))
```

E adicione os dois novos testes ao `describe('LoginPage')`:

```typescript
it('redirects to /admin/onboarding when onboarding not completed', async () => {
  const { authApi } = await import('@/lib/api/auth')
  const { tenantsApi } = await import('@/lib/api/tenants')
  const replace = vi.fn()
  vi.mocked(useRouter).mockReturnValue({ replace, push: vi.fn() } as never)

  vi.mocked(authApi.login).mockResolvedValue({
    accessToken: 'token', refreshToken: 'r', expiresAt: '',
  })
  vi.mocked(authApi.me).mockResolvedValue({
    id: '1', name: 'Owner', email: 'a@b.com', role: 'TenantOwner',
  } as never)
  vi.mocked(tenantsApi.me).mockResolvedValue({
    isOnboardingCompleted: false,
  } as never)

  render(<LoginPage />)
  await userEvent.type(screen.getByLabelText(/slug/i), 'barbearia')
  await userEvent.type(screen.getByLabelText(/email/i), 'a@b.com')
  await userEvent.type(screen.getByLabelText(/senha/i), '123456')
  fireEvent.click(screen.getByRole('button', { name: /entrar/i }))

  await waitFor(() => {
    expect(replace).toHaveBeenCalledWith('/admin/onboarding')
  })
})

it('redirects to /admin/dashboard when onboarding is completed', async () => {
  const { authApi } = await import('@/lib/api/auth')
  const { tenantsApi } = await import('@/lib/api/tenants')
  const replace = vi.fn()
  vi.mocked(useRouter).mockReturnValue({ replace, push: vi.fn() } as never)

  vi.mocked(authApi.login).mockResolvedValue({
    accessToken: 'token', refreshToken: 'r', expiresAt: '',
  })
  vi.mocked(authApi.me).mockResolvedValue({
    id: '1', name: 'Owner', email: 'a@b.com', role: 'TenantOwner',
  } as never)
  vi.mocked(tenantsApi.me).mockResolvedValue({
    isOnboardingCompleted: true,
  } as never)

  render(<LoginPage />)
  await userEvent.type(screen.getByLabelText(/slug/i), 'barbearia')
  await userEvent.type(screen.getByLabelText(/email/i), 'a@b.com')
  await userEvent.type(screen.getByLabelText(/senha/i), '123456')
  fireEvent.click(screen.getByRole('button', { name: /entrar/i }))

  await waitFor(() => {
    expect(replace).toHaveBeenCalledWith('/admin/dashboard')
  })
})
```

> **Nota:** o teste usa `vi.mocked(useRouter)` — verifique se o mock de `next/navigation` no arquivo já inclui `useRouter` como vi.fn() importável. Se não, ajuste o mock para: `vi.mock('next/navigation', () => ({ useRouter: vi.fn(() => ({ replace: vi.fn() })), useSearchParams: () => ({ get: () => null }) }))`.

- [ ] **Step 5.2: Execute os testes para confirmar que falham**

```bash
cd frontend && npx vitest run __tests__/login.test.tsx 2>&1 | tail -15
```

Resultado esperado: os 2 novos testes FAIL.

- [ ] **Step 5.3: Atualize a página de login**

Em `frontend/app/(auth)/login/page.tsx`, adicione o import de `tenantsApi`:

```typescript
import { tenantsApi } from '@/lib/api/tenants'
```

E substitua a função `onSubmit` completa:

```typescript
const onSubmit = async (data: FormData) => {
  setError(null)
  setLoading(true)
  try {
    document.cookie = `tenant_slug=${data.tenantSlug}; path=/`
    const tokens = await authApi.login(data.email, data.password)
    document.cookie = `access_token=${tokens.accessToken}; path=/; max-age=${60 * 60 * 24}`
    const [user, tenant] = await Promise.all([authApi.me(), tenantsApi.me()])
    setAuth(user, tokens, data.tenantSlug)

    const needsOnboarding =
      !tenant.isOnboardingCompleted &&
      (user.role === 'TenantOwner' || user.role === 'TenantAdmin')

    if (needsOnboarding) {
      router.replace('/admin/onboarding')
    } else {
      const redirect = searchParams.get('redirect') ?? '/admin/dashboard'
      router.replace(redirect)
    }
  } catch (err: unknown) {
    setError(err instanceof Error ? err.message : 'Erro ao fazer login')
  } finally {
    setLoading(false)
  }
}
```

- [ ] **Step 5.4: Execute os testes para confirmar que passam**

```bash
cd frontend && npx vitest run __tests__/login.test.tsx 2>&1 | tail -10
```

Resultado esperado: PASS — todos os testes do arquivo passam (incluindo os 2 existentes + 2 novos).

- [ ] **Step 5.5: Commit**

```bash
git add frontend/app/\(auth\)/login/page.tsx frontend/__tests__/login.test.tsx
git commit -m "feat: redirect to onboarding on first login when not completed"
```

---

## Task 6 — Frontend: página de onboarding marca conclusão

**Files:**
- Modify: `frontend/app/(admin)/admin/onboarding/page.tsx`

- [ ] **Step 6.1: Adicione o import de `tenantsApi`**

Em `frontend/app/(admin)/admin/onboarding/page.tsx`, adicione ao bloco de imports:

```typescript
import { tenantsApi } from '@/lib/api/tenants'
```

- [ ] **Step 6.2: Atualize `handleHoursFinish` para chamar `completeOnboarding`**

Substitua a função `handleHoursFinish` existente:

```typescript
const handleHoursFinish = async (data: OnboardingHoursData) => {
  setLoading(true)
  setError(null)
  try {
    await Promise.all(
      data.schedule.map(d =>
        onboardingApi.setBusinessHours(d.dayOfWeek, d.isOpen, d.openTime, d.closeTime)
      )
    )
    await tenantsApi.completeOnboarding()
    router.push('/admin/dashboard')
  } catch (e) {
    setError(e instanceof Error ? e.message : 'Erro ao salvar')
    setLoading(false)
  }
}
```

- [ ] **Step 6.3: Execute todos os testes frontend para confirmar sem regressões**

```bash
cd frontend && npx vitest run 2>&1 | tail -8
```

Resultado esperado: todos os testes passam, 0 falhas.

- [ ] **Step 6.4: Build de produção para confirmar sem erros TypeScript**

```bash
cd frontend && npx tsc --noEmit 2>&1 | head -20
```

Resultado esperado: sem erros de tipo.

- [ ] **Step 6.5: Commit final**

```bash
git add frontend/app/\(admin\)/admin/onboarding/page.tsx
git commit -m "feat: mark onboarding as complete when wizard finishes"
```

---

## Checklist de validação final

- [ ] `dotnet test` — 0 falhas (backend)
- [ ] `npx vitest run` — 0 falhas (frontend)
- [ ] Migration gerada e aplicável: `dotnet ef database update --project src/Horafy.Infrastructure --startup-project src/Horafy.API`
- [ ] Novo tenant (sem onboarding) → login → redireciona para `/admin/onboarding`
- [ ] Após completar o wizard → redireciona para `/admin/dashboard`
- [ ] Login com tenant já onboarded → redireciona para `/admin/dashboard`
- [ ] TenantAdmin (não apenas TenantOwner) também é redirecionado

---

## Resumo dos commits esperados

```
feat: add OnboardingCompletedAt to Tenant aggregate
feat: add CompleteOnboardingCommand handler
feat: expose IsOnboardingCompleted in TenantResult and add POST /tenants/me/onboarding-complete
feat: add isOnboardingCompleted to Tenant type and completeOnboarding to API client
feat: redirect to onboarding on first login when not completed
feat: mark onboarding as complete when wizard finishes
```
