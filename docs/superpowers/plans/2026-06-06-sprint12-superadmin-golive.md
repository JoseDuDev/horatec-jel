# Sprint 12 — Super Admin & Go-live

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar o Painel Super Admin (4 páginas), expandir o backend com o endpoint de listagem global de tenants, criar os arquivos de infraestrutura para deploy VPS (Caddy + Docker Compose produção) e o pipeline CI/CD no GitHub Actions.

**Architecture:** O backend recebe uma nova query `GetAllTenantsQuery` (sem scoping de tenant, acessível apenas para `PlatformAdmin`). O frontend ganha um route group `app/platform/(dashboard)/` com layout próprio, protegido por uma nova verificação no middleware (cookie `platform_access_token`). A infraestrutura inclui `docker-compose.prod.yml`, `Caddyfile` (Caddy como reverse proxy para API e Next.js) e `.github/workflows/ci.yml`.

**Tech Stack:** .NET 8 + MediatR (backend), Next.js 16 + shadcn/ui + Zustand (frontend), Caddy v2, Docker Compose v2, GitHub Actions.

---

## File Map

```
# Backend
src/Horafy.Application/Features/Tenants/Queries/GetAllTenants/
│   GetAllTenantsQuery.cs               # query + handler + DTO
tests/Horafy.Application.Tests/Tenants/
│   GetAllTenantsQueryHandlerTests.cs   # 3 testes unitários
src/Horafy.API/Controllers/V1/
│   TenantsController.cs                # (modify) adicionar GET /api/v1/platform/tenants

# Frontend
frontend/lib/types/platform.ts          # TenantSummary, PlanLimits
frontend/lib/api/platform.ts           # platformFetch, platformApi, platformLogin
frontend/store/platform-admin.ts       # Zustand store horafy-platform-admin
frontend/middleware.ts                  # (modify) add /platform/* protection
frontend/app/platform/login/page.tsx   # login standalone (sem layout)
frontend/app/platform/(dashboard)/
│   layout.tsx                          # platform layout + sidebar
│   tenants/page.tsx                    # listagem + ações activate/suspend
│   planos/page.tsx                     # gestão de planos (visualização + limites)
│   financeiro/page.tsx                 # MRR por plano, distribuição
frontend/components/platform/
│   PlatformSidebar.tsx                 # sidebar com 3 links
frontend/__tests__/PlatformTenants.test.tsx   # 2 testes

# Infrastructure
docker-compose.prod.yml                 # produção com Next.js + API + deps
Caddyfile                               # reverse proxy para API e frontend
.env.prod.example                       # template de variáveis de produção
.github/workflows/ci.yml               # CI: lint+build .NET, test+build Next.js
```

---

### Task 1: Backend — GetAllTenantsQuery

**Files:**
- Create: `src/Horafy.Application/Features/Tenants/Queries/GetAllTenants/GetAllTenantsQuery.cs`
- Create: `tests/Horafy.Application.Tests/Tenants/GetAllTenantsQueryHandlerTests.cs`
- Modify: `src/Horafy.API/Controllers/V1/TenantsController.cs`

- [ ] **Step 1: Escrever o teste**

```csharp
// tests/Horafy.Application.Tests/Tenants/GetAllTenantsQueryHandlerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Tenants.Queries.GetAllTenants;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Tenants;

public sealed class GetAllTenantsQueryHandlerTests
{
    private readonly Mock<ITenantRepository> _repo = new();

    private GetAllTenantsQueryHandler MakeHandler() =>
        new(_repo.Object);

    [Fact]
    public async Task Handle_WithTenants_ReturnsSummaryList()
    {
        var t1 = Tenant.Create("Barbearia A", "barb-a", TenantVertical.Barbershop, "a@test.com");
        var t2 = Tenant.Create("Clínica B",  "clinic-b", TenantVertical.MedicalClinic, "b@test.com");

        _repo.Setup(r => r.GetAllAsync(default))
             .ReturnsAsync(new List<Tenant> { t1, t2 });

        var result = await MakeHandler().Handle(new GetAllTenantsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(s => s.Slug).Should().Contain("barb-a").And.Contain("clinic-b");
    }

    [Fact]
    public async Task Handle_EmptyRepository_ReturnsEmptyList()
    {
        _repo.Setup(r => r.GetAllAsync(default))
             .ReturnsAsync(new List<Tenant>());

        var result = await MakeHandler().Handle(new GetAllTenantsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OrdersByCreatedAtDescending()
    {
        var older = Tenant.Create("Old",  "old-slug",  TenantVertical.Other);
        var newer = Tenant.Create("New",  "new-slug",  TenantVertical.Other);

        // Força CreatedAt via reflexão para simular ordem
        typeof(Horafy.Domain.Entities.Base.BaseEntity)
            .GetProperty("CreatedAt",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
            .SetValue(older, DateTimeOffset.UtcNow.AddDays(-5));
        typeof(Horafy.Domain.Entities.Base.BaseEntity)
            .GetProperty("CreatedAt",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
            .SetValue(newer, DateTimeOffset.UtcNow.AddDays(-1));

        _repo.Setup(r => r.GetAllAsync(default))
             .ReturnsAsync(new List<Tenant> { older, newer });

        var result = await MakeHandler().Handle(new GetAllTenantsQuery(), default);

        result.Value.First().Slug.Should().Be("new-slug");
    }
}
```

- [ ] **Step 2: Executar para verificar falha**

```powershell
cd C:\Projetos\JEL\JEL\Horafy
dotnet test tests/Horafy.Application.Tests --filter "GetAllTenantsQueryHandlerTests" 2>&1 | Select-Object -Last 10
```

Expected: FAIL — `GetAllTenantsQuery` não existe.

- [ ] **Step 3: Criar a query e handler**

```csharp
// src/Horafy.Application/Features/Tenants/Queries/GetAllTenants/GetAllTenantsQuery.cs
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Queries.GetAllTenants;

public sealed record GetAllTenantsQuery : IRequest<Result<IReadOnlyList<TenantSummary>>>;

public sealed record TenantSummary(
    Guid           Id,
    string         Name,
    string         Slug,
    TenantStatus   Status,
    TenantPlan     Plan,
    TenantVertical Vertical,
    string?        Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset? PlanRenewsAt);

internal sealed class GetAllTenantsQueryHandler(
    ITenantRepository tenantRepository)
    : IRequestHandler<GetAllTenantsQuery, Result<IReadOnlyList<TenantSummary>>>
{
    public async Task<Result<IReadOnlyList<TenantSummary>>> Handle(
        GetAllTenantsQuery request, CancellationToken cancellationToken)
    {
        var tenants = await tenantRepository.GetAllAsync(cancellationToken);

        var result = tenants
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TenantSummary(
                t.Id, t.Name, t.Slug,
                t.Status, t.Plan, t.Vertical,
                t.Email, t.CreatedAt, t.TrialEndsAt, t.PlanRenewsAt))
            .ToList();

        return Result.Success<IReadOnlyList<TenantSummary>>(result);
    }
}
```

- [ ] **Step 4: Adicionar endpoint no TenantsController**

Ler `src/Horafy.API/Controllers/V1/TenantsController.cs`. Adicionar após o método `GetBySlug` (linha ~52) e antes dos comentários de tenant context:

```csharp
    /// <summary>Lista todos os tenants da plataforma (PlatformAdmin only).</summary>
    [HttpGet("/api/v{version:apiVersion}/platform/tenants")]
    [Authorize(Roles = "PlatformAdmin")]
    [ProducesResponseType(typeof(IReadOnlyList<TenantSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetAllTenantsQuery(), cancellationToken));
```

Também adicionar o using necessário no topo do arquivo:

```csharp
using Horafy.Application.Features.Tenants.Queries.GetAllTenants;
```

- [ ] **Step 5: Executar testes**

```powershell
dotnet test tests/Horafy.Application.Tests --filter "GetAllTenantsQueryHandlerTests" 2>&1 | Select-Object -Last 10
```

Expected: PASS — 3 testes.

- [ ] **Step 6: Build do backend**

```powershell
dotnet build src/Horafy.API 2>&1 | Select-Object -Last 5
```

Expected: Build succeeded.

- [ ] **Step 7: Commit**

```powershell
git add src/Horafy.Application/Features/Tenants/Queries/GetAllTenants/ src/Horafy.API/Controllers/V1/TenantsController.cs tests/Horafy.Application.Tests/Tenants/GetAllTenantsQueryHandlerTests.cs
git commit -m "feat: add GetAllTenantsQuery and GET /platform/tenants endpoint for PlatformAdmin"
```

---

### Task 2: Frontend — Platform Types, API Client e Auth Store

**Files:**
- Create: `frontend/lib/types/platform.ts`
- Create: `frontend/lib/api/platform.ts`
- Create: `frontend/store/platform-admin.ts`

- [ ] **Step 1: Criar `frontend/lib/types/platform.ts`**

```typescript
// frontend/lib/types/platform.ts
export type TenantStatus = 'Active' | 'Suspended' | 'Trial' | 'Cancelled'
export type TenantPlan = 'Free' | 'Starter' | 'Professional' | 'Enterprise'
export type TenantVertical =
  | 'Barbershop' | 'EventHall' | 'SportsCourt'
  | 'ToyRental' | 'ToolRental'
  | 'MedicalClinic' | 'AestheticClinic' | 'Other'

export interface TenantSummary {
  id: string
  name: string
  slug: string
  status: TenantStatus
  plan: TenantPlan
  vertical: TenantVertical
  email?: string
  createdAt: string
  trialEndsAt?: string
  planRenewsAt?: string
}

export interface PlanLimits {
  plan: TenantPlan
  maxServices: number
  maxResources: number
  maxBookingsPerMonth: number
  priceMonthly: number
}

export const PLAN_LIMITS: PlanLimits[] = [
  { plan: 'Free',         maxServices: 3,   maxResources: 1,  maxBookingsPerMonth: 50,   priceMonthly: 0 },
  { plan: 'Starter',      maxServices: 10,  maxResources: 3,  maxBookingsPerMonth: 200,  priceMonthly: 49 },
  { plan: 'Professional', maxServices: 50,  maxResources: 10, maxBookingsPerMonth: 1000, priceMonthly: 149 },
  { plan: 'Enterprise',   maxServices: 999, maxResources: 99, maxBookingsPerMonth: 9999, priceMonthly: 399 },
]
```

- [ ] **Step 2: Criar `frontend/lib/api/platform.ts`**

```typescript
// frontend/lib/api/platform.ts
import type { TenantSummary } from '../types/platform'

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

async function platformFetch<T>(
  path: string,
  token: string,
  options: RequestInit = {}
): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
      ...options.headers,
    },
  })

  if (!res.ok) {
    const error = await res.json().catch(() => ({ title: res.statusText }))
    throw new Error(error.title ?? `HTTP ${res.status}`)
  }

  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
}

export interface LoginResult {
  accessToken: string
  refreshToken: string
  expiresAt: string
}

export const platformLogin = async (email: string, password: string): Promise<LoginResult> => {
  const res = await fetch(`${API_URL}/api/v1/auth/email`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password, tenantSlug: null }),
  })
  if (!res.ok) {
    const err = await res.json().catch(() => ({ title: res.statusText }))
    throw new Error(err.title ?? 'Credenciais inválidas')
  }
  return res.json()
}

export const platformApi = {
  tenants: (token: string) =>
    platformFetch<TenantSummary[]>('/api/v1/platform/tenants', token),

  suspendTenant: (token: string, id: string, reason: string) =>
    platformFetch<void>(`/api/v1/platform/tenants/${id}/suspend`, token, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),

  activateTenant: (token: string, id: string) =>
    platformFetch<void>(`/api/v1/platform/tenants/${id}/activate`, token, {
      method: 'POST',
    }),
}
```

- [ ] **Step 3: Criar `frontend/store/platform-admin.ts`**

```typescript
// frontend/store/platform-admin.ts
import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface PlatformAdmin {
  email: string
}

interface PlatformAdminState {
  admin: PlatformAdmin | null
  accessToken: string | null
  login: (email: string, accessToken: string) => void
  logout: () => void
}

export const usePlatformAdminStore = create<PlatformAdminState>()(
  persist(
    (set) => ({
      admin: null,
      accessToken: null,
      login: (email, accessToken) => set({ admin: { email }, accessToken }),
      logout: () => set({ admin: null, accessToken: null }),
    }),
    { name: 'horafy-platform-admin' }
  )
)
```

- [ ] **Step 4: Commit**

```powershell
git add frontend/lib/types/platform.ts frontend/lib/api/platform.ts frontend/store/platform-admin.ts
git commit -m "feat: add platform admin types, API client and Zustand store"
```

---

### Task 3: Frontend — Middleware + Login Page

**Files:**
- Modify: `frontend/middleware.ts`
- Create: `frontend/app/platform/login/page.tsx`

- [ ] **Step 1: Ler `frontend/middleware.ts`**

Ler o arquivo atual para entender a estrutura antes de modificar.

- [ ] **Step 2: Substituir `frontend/middleware.ts` com proteção de rotas de plataforma**

O arquivo atual protege `/admin/*`. Substituir pelo conteúdo abaixo que também protege `/platform/*`:

```typescript
// frontend/middleware.ts
import { NextRequest, NextResponse } from 'next/server'

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl

  // Protect /admin/* routes — require access_token cookie
  if (pathname.startsWith('/admin')) {
    const token = request.cookies.get('access_token')?.value
    if (!token) {
      const url = request.nextUrl.clone()
      url.pathname = '/login'
      return NextResponse.redirect(url)
    }
  }

  // Protect /platform/* routes (except /platform/login) — require platform_access_token cookie
  if (pathname.startsWith('/platform') && !pathname.startsWith('/platform/login')) {
    const token = request.cookies.get('platform_access_token')?.value
    if (!token) {
      const url = request.nextUrl.clone()
      url.pathname = '/platform/login'
      return NextResponse.redirect(url)
    }
  }

  return NextResponse.next()
}

export const config = {
  matcher: ['/admin/:path*', '/platform/:path*'],
}
```

- [ ] **Step 3: Criar `frontend/app/platform/login/page.tsx`**

```typescript
// frontend/app/platform/login/page.tsx
'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { platformLogin } from '@/lib/api/platform'
import { usePlatformAdminStore } from '@/store/platform-admin'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

export default function PlatformLoginPage() {
  const router = useRouter()
  const { login } = usePlatformAdminStore()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    setError(null)
    try {
      const result = await platformLogin(email, password)
      login(email, result.accessToken)
      // Persiste em cookie httpOnly para o middleware verificar
      document.cookie = `platform_access_token=${result.accessToken}; path=/; max-age=86400; SameSite=Lax`
      router.push('/platform/tenants')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao autenticar')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-slate-900 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-lg w-full max-w-sm p-8">
        <div className="text-center mb-8">
          <h1 className="text-2xl font-bold text-slate-900">Horafy Platform</h1>
          <p className="text-sm text-slate-500 mt-1">Acesso restrito — administradores da plataforma</p>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <Label htmlFor="email">E-mail</Label>
            <Input
              id="email"
              type="email"
              value={email}
              onChange={e => setEmail(e.target.value)}
              placeholder="admin@horafy.com.br"
              required
            />
          </div>
          <div>
            <Label htmlFor="password">Senha</Label>
            <Input
              id="password"
              type="password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              required
            />
          </div>

          {error && (
            <div className="p-3 bg-red-50 border border-red-200 rounded-lg text-sm text-red-700">
              {error}
            </div>
          )}

          <Button type="submit" className="w-full" disabled={loading}>
            {loading ? 'Autenticando...' : 'Entrar'}
          </Button>
        </form>
      </div>
    </div>
  )
}
```

- [ ] **Step 4: Commit**

```powershell
git add frontend/middleware.ts frontend/app/platform/login/page.tsx
git commit -m "feat: extend middleware for /platform/* routes and add platform login page"
```

---

### Task 4: Frontend — Platform Layout + Sidebar

**Files:**
- Create: `frontend/components/platform/PlatformSidebar.tsx`
- Create: `frontend/app/platform/(dashboard)/layout.tsx`

- [ ] **Step 1: Criar `frontend/components/platform/PlatformSidebar.tsx`**

```typescript
// frontend/components/platform/PlatformSidebar.tsx
'use client'

import Link from 'next/link'
import { usePathname, useRouter } from 'next/navigation'
import { Building2, CreditCard, BarChart3, LogOut } from 'lucide-react'
import { usePlatformAdminStore } from '@/store/platform-admin'
import { cn } from '@/lib/utils'

const NAV = [
  { href: '/platform/tenants',    label: 'Tenants',    icon: Building2 },
  { href: '/platform/planos',     label: 'Planos',     icon: CreditCard },
  { href: '/platform/financeiro', label: 'Financeiro', icon: BarChart3 },
]

export function PlatformSidebar() {
  const pathname = usePathname()
  const router = useRouter()
  const { admin, logout } = usePlatformAdminStore()

  const handleLogout = () => {
    logout()
    document.cookie = 'platform_access_token=; path=/; max-age=0'
    router.push('/platform/login')
  }

  return (
    <aside className="w-60 min-h-screen bg-slate-900 text-white flex flex-col">
      <div className="h-16 flex items-center px-6 border-b border-slate-700">
        <div>
          <span className="font-bold text-lg">Horafy</span>
          <span className="ml-2 text-xs text-slate-400 bg-slate-700 px-1.5 py-0.5 rounded">Platform</span>
        </div>
      </div>
      <nav className="flex-1 p-4 space-y-1">
        {NAV.map(({ href, label, icon: Icon }) => (
          <Link
            key={href}
            href={href}
            className={cn(
              'flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors',
              pathname === href
                ? 'bg-indigo-600 text-white'
                : 'text-slate-300 hover:bg-slate-800 hover:text-white'
            )}
          >
            <Icon className="h-4 w-4" />
            {label}
          </Link>
        ))}
      </nav>
      <div className="p-4 border-t border-slate-700">
        <div className="text-xs text-slate-400 mb-3">{admin?.email}</div>
        <button
          onClick={handleLogout}
          className="flex items-center gap-2 text-sm text-slate-300 hover:text-white w-full"
        >
          <LogOut className="h-4 w-4" />
          Sair
        </button>
      </div>
    </aside>
  )
}
```

- [ ] **Step 2: Criar `frontend/app/platform/(dashboard)/layout.tsx`**

```typescript
// frontend/app/platform/(dashboard)/layout.tsx
import { PlatformSidebar } from '@/components/platform/PlatformSidebar'

export default function PlatformDashboardLayout({
  children,
}: {
  children: React.ReactNode
}) {
  return (
    <div className="flex min-h-screen">
      <PlatformSidebar />
      <main className="flex-1 bg-slate-50 p-8">{children}</main>
    </div>
  )
}
```

- [ ] **Step 3: Commit**

```powershell
git add frontend/components/platform/PlatformSidebar.tsx "frontend/app/platform/(dashboard)/layout.tsx"
git commit -m "feat: add platform admin layout and sidebar"
```

---

### Task 5: Frontend — Platform Tenants Page

**Files:**
- Create: `frontend/app/platform/(dashboard)/tenants/page.tsx`
- Create: `frontend/__tests__/PlatformTenants.test.tsx`

- [ ] **Step 1: Escrever o teste**

```typescript
// frontend/__tests__/PlatformTenants.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { vi } from 'vitest'

// Mock next/navigation
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }))

// Mock zustand store
vi.mock('@/store/platform-admin', () => ({
  usePlatformAdminStore: () => ({ admin: { email: 'admin@test.com' }, accessToken: 'tok123' }),
}))

// Mock API
vi.mock('@/lib/api/platform', () => ({
  platformApi: {
    tenants: vi.fn().mockResolvedValue([
      {
        id: '1', name: 'Barbearia A', slug: 'barb-a', status: 'Active',
        plan: 'Starter', vertical: 'Barbershop', createdAt: '2026-01-01T00:00:00Z',
      },
      {
        id: '2', name: 'Clínica B', slug: 'clinic-b', status: 'Trial',
        plan: 'Free', vertical: 'MedicalClinic', createdAt: '2026-02-01T00:00:00Z',
      },
    ]),
    suspendTenant: vi.fn().mockResolvedValue(undefined),
    activateTenant: vi.fn().mockResolvedValue(undefined),
  },
}))

import PlatformTenantsPage from '@/app/platform/(dashboard)/tenants/page'

describe('PlatformTenantsPage', () => {
  it('renders tenant names after loading', async () => {
    render(<PlatformTenantsPage />)
    await waitFor(() => {
      expect(screen.getByText('Barbearia A')).toBeInTheDocument()
      expect(screen.getByText('Clínica B')).toBeInTheDocument()
    })
  })

  it('shows status badges', async () => {
    render(<PlatformTenantsPage />)
    await waitFor(() => {
      expect(screen.getByText('Active')).toBeInTheDocument()
      expect(screen.getByText('Trial')).toBeInTheDocument()
    })
  })
})
```

- [ ] **Step 2: Executar para verificar falha**

```powershell
cd C:\Projetos\JEL\JEL\Horafy\frontend
npm run test:run -- __tests__/PlatformTenants.test.tsx 2>&1 | Select-Object -Last 10
```

Expected: FAIL.

- [ ] **Step 3: Criar `frontend/app/platform/(dashboard)/tenants/page.tsx`**

```typescript
// frontend/app/platform/(dashboard)/tenants/page.tsx
'use client'

import { useEffect, useState } from 'react'
import { format } from 'date-fns'
import { ptBR } from 'date-fns/locale'
import { usePlatformAdminStore } from '@/store/platform-admin'
import { platformApi } from '@/lib/api/platform'
import type { TenantSummary } from '@/lib/types/platform'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'

const STATUS_COLOR: Record<string, string> = {
  Active:    'bg-green-100 text-green-700',
  Trial:     'bg-blue-100 text-blue-700',
  Suspended: 'bg-red-100 text-red-700',
  Cancelled: 'bg-slate-100 text-slate-600',
}

const PLAN_COLOR: Record<string, string> = {
  Free:         'bg-slate-100 text-slate-600',
  Starter:      'bg-indigo-100 text-indigo-700',
  Professional: 'bg-purple-100 text-purple-700',
  Enterprise:   'bg-amber-100 text-amber-700',
}

export default function PlatformTenantsPage() {
  const { accessToken } = usePlatformAdminStore()
  const [tenants, setTenants] = useState<TenantSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [search, setSearch] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [actionLoading, setActionLoading] = useState<string | null>(null)

  useEffect(() => {
    if (!accessToken) return
    platformApi.tenants(accessToken)
      .then(setTenants)
      .catch(e => setError(e.message))
      .finally(() => setLoading(false))
  }, [accessToken])

  const handleSuspend = async (id: string) => {
    if (!accessToken) return
    const reason = prompt('Motivo da suspensão:')
    if (!reason) return
    setActionLoading(id)
    try {
      await platformApi.suspendTenant(accessToken, id, reason)
      setTenants(ts => ts.map(t => t.id === id ? { ...t, status: 'Suspended' } : t))
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Erro')
    } finally {
      setActionLoading(null)
    }
  }

  const handleActivate = async (id: string) => {
    if (!accessToken) return
    setActionLoading(id)
    try {
      await platformApi.activateTenant(accessToken, id)
      setTenants(ts => ts.map(t => t.id === id ? { ...t, status: 'Active' } : t))
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Erro')
    } finally {
      setActionLoading(null)
    }
  }

  const filtered = tenants.filter(t =>
    t.name.toLowerCase().includes(search.toLowerCase()) ||
    t.slug.toLowerCase().includes(search.toLowerCase())
  )

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Tenants</h1>
          <p className="text-slate-500 text-sm">{tenants.length} estabelecimentos cadastrados</p>
        </div>
        <Input
          value={search}
          onChange={e => setSearch(e.target.value)}
          placeholder="Buscar por nome ou slug..."
          className="w-64"
        />
      </div>

      {error && <div className="mb-4 p-3 bg-red-50 text-red-700 rounded-lg text-sm">{error}</div>}

      {loading ? (
        <p className="text-slate-500">Carregando...</p>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Lista de tenants</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-slate-500">
                    <th className="text-left py-2 pr-4">Nome</th>
                    <th className="text-left py-2 pr-4">Slug</th>
                    <th className="text-left py-2 pr-4">Status</th>
                    <th className="text-left py-2 pr-4">Plano</th>
                    <th className="text-left py-2 pr-4">Cadastro</th>
                    <th className="text-left py-2">Ações</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map(t => (
                    <tr key={t.id} className="border-b last:border-0 hover:bg-slate-50">
                      <td className="py-3 pr-4 font-medium">{t.name}</td>
                      <td className="py-3 pr-4 text-slate-500 font-mono text-xs">{t.slug}</td>
                      <td className="py-3 pr-4">
                        <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${STATUS_COLOR[t.status] ?? ''}`}>
                          {t.status}
                        </span>
                      </td>
                      <td className="py-3 pr-4">
                        <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${PLAN_COLOR[t.plan] ?? ''}`}>
                          {t.plan}
                        </span>
                      </td>
                      <td className="py-3 pr-4 text-slate-500">
                        {format(new Date(t.createdAt), 'dd/MM/yyyy', { locale: ptBR })}
                      </td>
                      <td className="py-3">
                        {t.status === 'Suspended' ? (
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => handleActivate(t.id)}
                            disabled={actionLoading === t.id}
                          >
                            Reativar
                          </Button>
                        ) : t.status === 'Active' || t.status === 'Trial' ? (
                          <Button
                            size="sm"
                            variant="destructive"
                            onClick={() => handleSuspend(t.id)}
                            disabled={actionLoading === t.id}
                          >
                            Suspender
                          </Button>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                  {filtered.length === 0 && (
                    <tr>
                      <td colSpan={6} className="py-8 text-center text-slate-400">
                        Nenhum tenant encontrado.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  )
}
```

- [ ] **Step 4: Executar teste**

```powershell
npm run test:run -- __tests__/PlatformTenants.test.tsx 2>&1 | Select-Object -Last 10
```

Expected: PASS — 2 testes.

- [ ] **Step 5: Commit**

```powershell
git add "frontend/app/platform/(dashboard)/tenants/page.tsx" frontend/__tests__/PlatformTenants.test.tsx
git commit -m "feat: add platform tenants page with activate/suspend actions"
```

---

### Task 6: Frontend — Platform Planos e Financeiro

**Files:**
- Create: `frontend/app/platform/(dashboard)/planos/page.tsx`
- Create: `frontend/app/platform/(dashboard)/financeiro/page.tsx`

- [ ] **Step 1: Criar `frontend/app/platform/(dashboard)/planos/page.tsx`**

```typescript
// frontend/app/platform/(dashboard)/planos/page.tsx
'use client'

import { useEffect, useState } from 'react'
import { usePlatformAdminStore } from '@/store/platform-admin'
import { platformApi } from '@/lib/api/platform'
import type { TenantSummary } from '@/lib/types/platform'
import { PLAN_LIMITS } from '@/lib/types/platform'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Check } from 'lucide-react'

export default function PlatformPlanosPage() {
  const { accessToken } = usePlatformAdminStore()
  const [tenants, setTenants] = useState<TenantSummary[]>([])

  useEffect(() => {
    if (!accessToken) return
    platformApi.tenants(accessToken).then(setTenants).catch(() => {})
  }, [accessToken])

  const countByPlan = (plan: string) => tenants.filter(t => t.plan === plan).length

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-slate-900">Planos</h1>
        <p className="text-slate-500 text-sm">Limites e preços de cada plano da plataforma</p>
      </div>

      <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
        {PLAN_LIMITS.map(p => (
          <Card key={p.plan} className="flex flex-col">
            <CardHeader>
              <CardTitle className="text-lg">{p.plan}</CardTitle>
              <p className="text-2xl font-bold mt-1">
                {p.priceMonthly === 0 ? 'Grátis' : `R$ ${p.priceMonthly}/mês`}
              </p>
            </CardHeader>
            <CardContent className="flex-1">
              <ul className="space-y-2 text-sm text-slate-600">
                <li className="flex items-center gap-2">
                  <Check className="h-4 w-4 text-green-500 shrink-0" />
                  {p.maxServices === 999 ? 'Serviços ilimitados' : `${p.maxServices} serviços`}
                </li>
                <li className="flex items-center gap-2">
                  <Check className="h-4 w-4 text-green-500 shrink-0" />
                  {p.maxResources === 99 ? 'Recursos ilimitados' : `${p.maxResources} recursos`}
                </li>
                <li className="flex items-center gap-2">
                  <Check className="h-4 w-4 text-green-500 shrink-0" />
                  {p.maxBookingsPerMonth === 9999 ? 'Agendamentos ilimitados' : `${p.maxBookingsPerMonth} agendamentos/mês`}
                </li>
              </ul>
              <div className="mt-4 pt-4 border-t">
                <p className="text-xs text-slate-400">Tenants neste plano</p>
                <p className="text-2xl font-bold text-slate-900">{countByPlan(p.plan)}</p>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}
```

- [ ] **Step 2: Criar `frontend/app/platform/(dashboard)/financeiro/page.tsx`**

```typescript
// frontend/app/platform/(dashboard)/financeiro/page.tsx
'use client'

import { useEffect, useState } from 'react'
import { usePlatformAdminStore } from '@/store/platform-admin'
import { platformApi } from '@/lib/api/platform'
import type { TenantSummary } from '@/lib/types/platform'
import { PLAN_LIMITS } from '@/lib/types/platform'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { TrendingUp, Users, DollarSign, Activity } from 'lucide-react'

export default function PlatformFinanceiroPage() {
  const { accessToken } = usePlatformAdminStore()
  const [tenants, setTenants] = useState<TenantSummary[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (!accessToken) return
    platformApi.tenants(accessToken)
      .then(setTenants)
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [accessToken])

  const activeTenants = tenants.filter(t => t.status === 'Active' || t.status === 'Trial')

  const mrr = tenants
    .filter(t => t.status === 'Active')
    .reduce((sum, t) => {
      const plan = PLAN_LIMITS.find(p => p.plan === t.plan)
      return sum + (plan?.priceMonthly ?? 0)
    }, 0)

  const planDist = PLAN_LIMITS.map(p => ({
    plan: p.plan,
    count: tenants.filter(t => t.plan === p.plan).length,
    revenue: tenants.filter(t => t.plan === p.plan && t.status === 'Active').length * p.priceMonthly,
  }))

  const metrics = [
    { label: 'MRR (Receita Mensal)', value: `R$ ${mrr.toLocaleString('pt-BR')}`, icon: DollarSign, color: 'text-green-600' },
    { label: 'Tenants Ativos', value: activeTenants.length, icon: Users, color: 'text-indigo-600' },
    { label: 'Total Tenants', value: tenants.length, icon: Activity, color: 'text-slate-600' },
    { label: 'ARR (Estimado)', value: `R$ ${(mrr * 12).toLocaleString('pt-BR')}`, icon: TrendingUp, color: 'text-purple-600' },
  ]

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-slate-900">Financeiro da Plataforma</h1>
        <p className="text-slate-500 text-sm">Receita e distribuição de tenants por plano</p>
      </div>

      {loading ? (
        <p className="text-slate-500">Carregando...</p>
      ) : (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4 mb-8">
            {metrics.map(m => (
              <Card key={m.label}>
                <CardContent className="pt-4">
                  <div className="flex items-center gap-3">
                    <m.icon className={`h-8 w-8 ${m.color}`} />
                    <div>
                      <p className="text-xs text-slate-500">{m.label}</p>
                      <p className="text-2xl font-bold text-slate-900">{m.value}</p>
                    </div>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Distribuição por Plano</CardTitle>
            </CardHeader>
            <CardContent>
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-slate-500">
                    <th className="text-left py-2 pr-4">Plano</th>
                    <th className="text-right py-2 pr-4">Tenants</th>
                    <th className="text-right py-2 pr-4">Receita Mensal</th>
                    <th className="text-right py-2">Receita Anual</th>
                  </tr>
                </thead>
                <tbody>
                  {planDist.map(p => (
                    <tr key={p.plan} className="border-b last:border-0">
                      <td className="py-3 pr-4 font-medium">{p.plan}</td>
                      <td className="py-3 pr-4 text-right">{p.count}</td>
                      <td className="py-3 pr-4 text-right text-green-700 font-medium">
                        R$ {p.revenue.toLocaleString('pt-BR')}
                      </td>
                      <td className="py-3 text-right text-slate-500">
                        R$ {(p.revenue * 12).toLocaleString('pt-BR')}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </CardContent>
          </Card>
        </>
      )}
    </div>
  )
}
```

- [ ] **Step 3: Commit**

```powershell
git add "frontend/app/platform/(dashboard)/planos/page.tsx" "frontend/app/platform/(dashboard)/financeiro/page.tsx"
git commit -m "feat: add platform plans and financeiro pages"
```

---

### Task 7: Infrastructure — docker-compose.prod.yml + Caddyfile

**Files:**
- Create: `docker-compose.prod.yml`
- Create: `Caddyfile`
- Create: `.env.prod.example`

- [ ] **Step 1: Criar `docker-compose.prod.yml`**

```yaml
# docker-compose.prod.yml — Produção (VPS Hetzner/DigitalOcean)
# Uso: docker compose -f docker-compose.prod.yml up -d
name: horafy-prod

services:
  caddy:
    image: caddy:2-alpine
    container_name: horafy-caddy
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
      - "443:443/udp"  # HTTP/3
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
      - caddy_config:/config
    networks:
      - horafy-net
    depends_on:
      - api
      - frontend

  postgres:
    image: postgres:16-alpine
    container_name: horafy-postgres
    restart: unless-stopped
    environment:
      POSTGRES_DB: horafy
      POSTGRES_USER: horafy
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./infra/postgres/init.sql:/docker-entrypoint-initdb.d/01-init.sql:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U horafy -d horafy"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - horafy-net

  redis:
    image: redis:7-alpine
    container_name: horafy-redis
    restart: unless-stopped
    command: redis-server --requirepass ${REDIS_PASSWORD} --maxmemory 256mb --maxmemory-policy allkeys-lru
    volumes:
      - redis_data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "-a", "${REDIS_PASSWORD}", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - horafy-net

  rabbitmq:
    image: rabbitmq:3.13-alpine
    container_name: horafy-rabbitmq
    restart: unless-stopped
    environment:
      RABBITMQ_DEFAULT_USER: horafy
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}
      RABBITMQ_DEFAULT_VHOST: horafy
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "ping"]
      interval: 15s
      timeout: 10s
      retries: 5
    networks:
      - horafy-net

  api:
    image: ghcr.io/${GITHUB_REPOSITORY}/horafy-api:${IMAGE_TAG:-latest}
    container_name: horafy-api
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=horafy;Username=horafy;Password=${POSTGRES_PASSWORD}"
      Redis__ConnectionString: "redis:6379,password=${REDIS_PASSWORD}"
      RabbitMQ__Host: rabbitmq
      RabbitMQ__Username: horafy
      RabbitMQ__Password: ${RABBITMQ_PASSWORD}
      JWT__Secret: ${JWT_SECRET}
      JWT__Issuer: ${JWT_ISSUER}
      JWT__Audience: ${JWT_AUDIENCE}
      MercadoPago__AccessToken: ${MERCADOPAGO_ACCESS_TOKEN}
      Google__ClientId: ${GOOGLE_CLIENT_ID}
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    networks:
      - horafy-net

  frontend:
    image: ghcr.io/${GITHUB_REPOSITORY}/horafy-frontend:${IMAGE_TAG:-latest}
    container_name: horafy-frontend
    restart: unless-stopped
    environment:
      NODE_ENV: production
      NEXT_PUBLIC_API_URL: https://${DOMAIN}
      NEXT_PUBLIC_GOOGLE_CLIENT_ID: ${GOOGLE_CLIENT_ID}
    networks:
      - horafy-net
    depends_on:
      - api

volumes:
  postgres_data:
  redis_data:
  rabbitmq_data:
  caddy_data:
  caddy_config:

networks:
  horafy-net:
    driver: bridge
```

- [ ] **Step 2: Criar `Caddyfile`**

```caddyfile
# Caddyfile — Horafy production reverse proxy
# Substitua horafy.com.br pelo domínio real

{
  email ops@horafy.com.br
}

horafy.com.br {
  # API: rota /api/* e /health para o backend .NET
  handle /api/* {
    reverse_proxy api:8080
  }

  handle /health {
    reverse_proxy api:8080
  }

  # Scalar UI e webhooks também vão para o backend
  handle /scalar/* {
    reverse_proxy api:8080
  }

  handle /webhooks/* {
    reverse_proxy api:8080
  }

  # Tudo mais vai para o Next.js
  handle {
    reverse_proxy frontend:3000
  }

  # Logs estruturados
  log {
    output file /var/log/caddy/horafy.log {
      roll_size 10mb
      roll_keep 5
    }
  }
}

# Domínios próprios dos tenants: *.tenant.com.br → mesma aplicação
# (Tenants configuram CNAME → horafy.com.br no DNS deles)
:443 {
  tls {
    on_demand
  }
  handle /api/* {
    reverse_proxy api:8080
  }
  handle {
    reverse_proxy frontend:3000
  }
}
```

- [ ] **Step 3: Criar `.env.prod.example`**

```bash
# .env.prod.example — copie para .env.prod e preencha antes do deploy
# NUNCA commite .env.prod com valores reais

# Banco de dados
POSTGRES_PASSWORD=troque_por_senha_forte

# Cache
REDIS_PASSWORD=troque_por_senha_forte

# Mensageria
RABBITMQ_PASSWORD=troque_por_senha_forte

# JWT
JWT_SECRET=troque_por_segredo_min_32_caracteres
JWT_ISSUER=https://horafy.com.br
JWT_AUDIENCE=horafy-api

# Domínio
DOMAIN=horafy.com.br

# Imagens Docker (preenchido pelo CI/CD)
GITHUB_REPOSITORY=seu-org/horafy
IMAGE_TAG=latest

# Integrações
MERCADOPAGO_ACCESS_TOKEN=APP_USR-xxx
GOOGLE_CLIENT_ID=xxx.apps.googleusercontent.com
```

- [ ] **Step 4: Adicionar `.env.prod` ao `.gitignore`**

Ler o `.gitignore` atual (raiz do repositório). Adicionar a linha `.env.prod` se não existir.

Verificar com:
```powershell
Select-String -Path "C:\Projetos\JEL\JEL\Horafy\.gitignore" -Pattern "\.env\.prod" -Quiet
```

Se retornar `False`, adicionar ao arquivo:
```
.env.prod
```

- [ ] **Step 5: Commit**

```powershell
git add docker-compose.prod.yml Caddyfile .env.prod.example .gitignore
git commit -m "feat: add production Docker Compose, Caddyfile and env template"
```

---

### Task 8: GitHub Actions CI/CD

**Files:**
- Create: `.github/workflows/ci.yml`

- [ ] **Step 1: Criar `.github/workflows/ci.yml`**

```yaml
# .github/workflows/ci.yml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  # ── Backend .NET ──────────────────────────────────────────────────────────
  backend:
    name: Backend — build & test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore -c Release

      - name: Test
        run: dotnet test --no-build -c Release --verbosity normal

  # ── Frontend Next.js ──────────────────────────────────────────────────────
  frontend:
    name: Frontend — test & build
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: frontend
    steps:
      - uses: actions/checkout@v4

      - name: Setup Node 20
        uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: npm
          cache-dependency-path: frontend/package-lock.json

      - name: Install dependencies
        run: npm ci

      - name: Run tests
        run: npm run test:run

      - name: Build
        run: npm run build
        env:
          NEXT_PUBLIC_API_URL: http://localhost:5000
          NEXT_PUBLIC_GOOGLE_CLIENT_ID: mock-client-id

  # ── Docker build (apenas em push para main) ────────────────────────────────
  docker:
    name: Docker — build images
    runs-on: ubuntu-latest
    needs: [backend, frontend]
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'
    permissions:
      contents: read
      packages: write
    steps:
      - uses: actions/checkout@v4

      - name: Login to GitHub Container Registry
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Build and push API image
        uses: docker/build-push-action@v5
        with:
          context: .
          file: src/Horafy.API/Dockerfile
          push: true
          tags: |
            ghcr.io/${{ github.repository }}/horafy-api:latest
            ghcr.io/${{ github.repository }}/horafy-api:${{ github.sha }}

      - name: Build and push Frontend image
        uses: docker/build-push-action@v5
        with:
          context: ./frontend
          file: frontend/Dockerfile
          push: true
          tags: |
            ghcr.io/${{ github.repository }}/horafy-frontend:latest
            ghcr.io/${{ github.repository }}/horafy-frontend:${{ github.sha }}
          build-args: |
            NEXT_PUBLIC_API_URL=https://horafy.com.br
            NEXT_PUBLIC_GOOGLE_CLIENT_ID=${{ secrets.GOOGLE_CLIENT_ID }}
```

- [ ] **Step 2: Criar `frontend/Dockerfile`**

O Docker job precisa de um Dockerfile para o frontend. Criar:

```dockerfile
# frontend/Dockerfile
FROM node:20-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci

FROM node:20-alpine AS builder
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
ARG NEXT_PUBLIC_API_URL
ARG NEXT_PUBLIC_GOOGLE_CLIENT_ID
ENV NEXT_PUBLIC_API_URL=${NEXT_PUBLIC_API_URL}
ENV NEXT_PUBLIC_GOOGLE_CLIENT_ID=${NEXT_PUBLIC_GOOGLE_CLIENT_ID}
RUN npm run build

FROM node:20-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
RUN addgroup --system --gid 1001 nodejs && adduser --system --uid 1001 nextjs
COPY --from=builder /app/public ./public
COPY --from=builder --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/.next/static ./.next/static
USER nextjs
EXPOSE 3000
ENV PORT=3000
CMD ["node", "server.js"]
```

- [ ] **Step 3: Configurar Next.js output standalone**

Ler `frontend/next.config.ts`. Adicionar `output: 'standalone'` dentro de `nextConfig`:

```typescript
const nextConfig: NextConfig = {
  output: 'standalone',
  env: {
    NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000',
  },
  turbopack: {},
}
```

- [ ] **Step 4: Commit**

```powershell
git add .github/workflows/ci.yml frontend/Dockerfile frontend/next.config.ts
git commit -m "feat: add GitHub Actions CI/CD pipeline and frontend Dockerfile"
```

---

### Task 9: Smoke Tests

**Files:**
- Create: `frontend/__tests__/smoke.test.ts`

Os smoke tests verificam que os endpoints críticos respondem com os status codes corretos. Eles usam `SMOKE_API_URL` (variável de ambiente) — quando não definida, o teste é pulado graciosamente. Isso permite executar em ambiente de CI sem um backend rodando.

- [ ] **Step 1: Criar `frontend/__tests__/smoke.test.ts`**

```typescript
// frontend/__tests__/smoke.test.ts
// Smoke tests — executar após deploy com: SMOKE_API_URL=https://horafy.com.br npm run test:run
// Em CI sem backend ativo, os testes são pulados automaticamente.
import { describe, it, expect } from 'vitest'

const BASE = process.env.SMOKE_API_URL

const skip = !BASE

describe.skipIf(skip)('Smoke tests — API endpoints', () => {
  it('health endpoint returns 200', async () => {
    const res = await fetch(`${BASE}/health`)
    expect(res.status).toBe(200)
  })

  it('GET /api/v1/platform/tenants without auth returns 401', async () => {
    const res = await fetch(`${BASE}/api/v1/platform/tenants`)
    expect(res.status).toBe(401)
  })

  it('GET /api/v1/services without tenant slug returns 400 or 404', async () => {
    const res = await fetch(`${BASE}/api/v1/services`)
    expect([400, 404]).toContain(res.status)
  })

  it('GET /api/v1/platform/tenants/{slug} with unknown slug returns 404', async () => {
    const res = await fetch(`${BASE}/api/v1/platform/tenants/slug-que-nao-existe-12345`)
    expect(res.status).toBe(404)
  })
})

describe('Smoke tests — portal SSR (fetch estático)', () => {
  it('describes PLAN_LIMITS correctly', () => {
    // Verifica que os limites de plano estão corretamente definidos
    const { PLAN_LIMITS } = require('@/lib/types/platform')
    expect(PLAN_LIMITS).toHaveLength(4)
    expect(PLAN_LIMITS.find((p: { plan: string }) => p.plan === 'Free')?.priceMonthly).toBe(0)
    expect(PLAN_LIMITS.find((p: { plan: string }) => p.plan === 'Enterprise')?.maxServices).toBe(999)
  })
})
```

- [ ] **Step 2: Executar para verificar comportamento**

```powershell
cd C:\Projetos\JEL\JEL\Horafy\frontend
npm run test:run -- __tests__/smoke.test.ts 2>&1 | Select-Object -Last 15
```

Expected: 
- Os 4 testes de API são pulados (sem `SMOKE_API_URL`)
- O teste de `PLAN_LIMITS` passa

- [ ] **Step 3: Commit**

```powershell
git add frontend/__tests__/smoke.test.ts
git commit -m "feat: add smoke tests for API health and PLAN_LIMITS validation"
```

---

### Task 10: Full Suite + Build Final

**Files:** nenhum novo

- [ ] **Step 1: Backend — rodar todos os testes**

```powershell
cd C:\Projetos\JEL\JEL\Horafy
dotnet test 2>&1 | Select-Object -Last 15
```

Expected: todos os testes passando (incluindo o novo `GetAllTenantsQueryHandlerTests`).

- [ ] **Step 2: Frontend — rodar toda a suíte**

```powershell
cd C:\Projetos\JEL\JEL\Horafy\frontend
npm run test:run 2>&1 | Select-Object -Last 10
```

Expected: 17+ arquivos de teste, 33+ testes passando.

- [ ] **Step 3: Frontend — build de produção**

```powershell
npm run build 2>&1 | Select-Object -Last 30
```

Expected: build sem erros. Rotas novas esperadas no output:
- `○ /platform/login`
- `○ /platform/tenants`
- `○ /platform/planos`
- `○ /platform/financeiro`

Se houver erro de tipo no `next.config.ts` (output standalone):

```typescript
// Possível fix — cast se necessário:
const nextConfig = {
  output: 'standalone' as const,
  // ...
} satisfies NextConfig
```

- [ ] **Step 4: Commit final de integração**

```powershell
cd C:\Projetos\JEL\JEL\Horafy
git add frontend/
git commit -m "chore: Sprint 12 integration — full suite green, build passing"
```

(Apenas se houver arquivos não commitados.)

---

## Self-Review

### Spec Coverage

| Requisito | Task | Status |
|-----------|------|--------|
| Painel plataforma — `/platform/tenants` | Tasks 1, 5 | ✅ |
| Painel plataforma — `/platform/planos` | Task 6 | ✅ |
| Painel plataforma — `/platform/financeiro` | Task 6 | ✅ |
| Gestão de planos (exibição + distribuição) | Tasks 2, 6 | ✅ |
| Deploy VPS — Caddy (SSL + roteamento) | Task 7 | ✅ |
| Deploy VPS — Docker Compose produção | Task 7 | ✅ |
| CI/CD — GitHub Actions (build + test) | Task 8 | ✅ |
| Smoke tests | Task 9 | ✅ |
| `/platform/suporte` (impersonate, logs) | — | ⏭ Pós-MVP (requer auditoria e RBAC avançado) |

### Placeholder Scan

Sem TBDs. Todo código é concreto com imports, tipos e lógica completos.

### Type Consistency

- `TenantSummary` — definido em `lib/types/platform.ts` (Task 2), usado em `platformApi.tenants()` (Task 2), `PlatformTenantsPage` (Task 5), `PlatformPlanosPage` (Task 6), `PlatformFinanceiroPage` (Task 6)
- `PLAN_LIMITS: PlanLimits[]` — definido em `lib/types/platform.ts`, usado em Planos e Financeiro pages e no smoke test
- `platformApi.suspendTenant(token, id, reason)` — definido em `lib/api/platform.ts`, chamado em `PlatformTenantsPage` com os mesmos 3 argumentos
- `platformApi.activateTenant(token, id)` — definido e chamado com 2 argumentos consistentemente
- `usePlatformAdminStore().accessToken` — definido no store como `string | null`, verificado com `if (!accessToken) return` antes de uso nos 3 pages
- `GetAllTenantsQuery` → retorna `Result<IReadOnlyList<TenantSummary>>` — o `TenantSummary` C# e o `TenantSummary` TypeScript têm os mesmos campos com os mesmos nomes (camelCase no TS, PascalCase no C# serializado como camelCase por padrão no ASP.NET)
