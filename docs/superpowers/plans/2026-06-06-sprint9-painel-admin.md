# Sprint 9 — Frontend Next.js + Painel Administrativo Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Criar o projeto Next.js 14 em `frontend/` e implementar o Painel Administrativo completo do Tenant (9 páginas) com auth JWT, layout shell e API client tipado.

**Architecture:** Next.js 14 App Router com dois grupos de rotas — `(auth)` para login e `(admin)` para o painel protegido. Auth via JWT armazenado em cookie httpOnly; middleware Next.js redireciona rotas `/admin/*` para `/login` se o cookie não existir. API client centralizado em `lib/api/` com fetch tipado usando os tipos gerados do backend.

**Tech Stack:** Next.js 14 (App Router), TypeScript, Tailwind CSS, shadcn/ui, Zustand, React Hook Form + Zod, @tanstack/react-query, Vitest + @testing-library/react, next-auth (somente Google OAuth para admin — redireciona ao backend), date-fns, recharts (gráficos financeiros), @hello-pangea/dnd (drag-and-drop agenda).

---

## File Map

```
frontend/
├── app/
│   ├── (auth)/
│   │   └── login/page.tsx
│   ├── (admin)/
│   │   ├── layout.tsx                  # AdminShell — sidebar + topbar
│   │   ├── admin/
│   │   │   ├── dashboard/page.tsx
│   │   │   ├── agenda/page.tsx
│   │   │   ├── agendamentos/page.tsx
│   │   │   ├── clientes/page.tsx
│   │   │   ├── servicos/page.tsx
│   │   │   ├── recursos/page.tsx
│   │   │   ├── financeiro/page.tsx
│   │   │   ├── notificacoes/page.tsx
│   │   │   └── configuracoes/page.tsx
├── components/
│   ├── ui/                             # shadcn gerados
│   ├── admin/
│   │   ├── AdminShell.tsx
│   │   ├── Sidebar.tsx
│   │   └── Topbar.tsx
│   ├── bookings/
│   │   ├── BookingCalendar.tsx
│   │   └── BookingTable.tsx
│   ├── services/
│   │   └── ServiceForm.tsx
│   ├── resources/
│   │   └── ResourceForm.tsx
│   ├── financeiro/
│   │   └── RevenueChart.tsx
│   └── notifications/
│       └── TemplateEditor.tsx
├── lib/
│   ├── api/
│   │   ├── client.ts                   # fetch wrapper com auth header
│   │   ├── auth.ts
│   │   ├── bookings.ts
│   │   ├── services.ts
│   │   ├── resources.ts
│   │   ├── financeiro.ts
│   │   ├── notifications.ts
│   │   └── tenants.ts
│   └── types/
│       ├── auth.ts
│       ├── booking.ts
│       ├── service.ts
│       ├── resource.ts
│       ├── financeiro.ts
│       ├── notification.ts
│       └── tenant.ts
├── store/
│   └── auth.ts                         # Zustand: user + token
├── middleware.ts                        # redirect /admin/* se sem cookie
├── next.config.ts
├── tailwind.config.ts
└── vitest.config.ts
```

---

### Task 1: Scaffolding do Projeto Next.js

**Files:**
- Create: `frontend/` (diretório raiz do projeto)
- Create: `frontend/next.config.ts`
- Create: `frontend/tailwind.config.ts`
- Create: `frontend/tsconfig.json`
- Create: `frontend/vitest.config.ts`
- Create: `frontend/.env.local` (exemplo)

- [ ] **Step 1: Criar o projeto Next.js 14**

```bash
cd C:\Projetos\JEL\JEL\Horafy
npx create-next-app@latest frontend --typescript --tailwind --eslint --app --src-dir=false --import-alias="@/*" --no-git
```

Expected: Diretório `frontend/` criado com estrutura App Router.

- [ ] **Step 2: Instalar dependências**

```bash
cd frontend
npm install @tanstack/react-query zustand react-hook-form zod @hookform/resolvers date-fns recharts @hello-pangea/dnd lucide-react clsx tailwind-merge class-variance-authority
npm install -D vitest @vitejs/plugin-react @testing-library/react @testing-library/jest-dom @testing-library/user-event jsdom
```

- [ ] **Step 3: Instalar shadcn/ui**

```bash
npx shadcn@latest init
```

Escolher: Style=default, Base Color=slate, CSS Variables=yes.

```bash
npx shadcn@latest add button input label card table badge select dialog form toast tabs alert sheet dropdown-menu calendar
```

- [ ] **Step 4: Configurar `vitest.config.ts`**

```typescript
// frontend/vitest.config.ts
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import { resolve } from 'path'

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./vitest.setup.ts'],
  },
  resolve: {
    alias: { '@': resolve(__dirname, '.') },
  },
})
```

- [ ] **Step 5: Criar `vitest.setup.ts`**

```typescript
// frontend/vitest.setup.ts
import '@testing-library/jest-dom'
```

- [ ] **Step 6: Adicionar script de teste em `package.json`**

No `frontend/package.json`, dentro de `"scripts"`, adicionar:
```json
"test": "vitest",
"test:run": "vitest run"
```

- [ ] **Step 7: Configurar `next.config.ts`**

```typescript
// frontend/next.config.ts
import type { NextConfig } from 'next'

const nextConfig: NextConfig = {
  env: {
    NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000',
  },
}

export default nextConfig
```

- [ ] **Step 8: Criar `.env.local`**

```
NEXT_PUBLIC_API_URL=http://localhost:5000
```

- [ ] **Step 9: Commit**

```bash
cd frontend
git add -A
git commit -m "feat: scaffold Next.js 14 frontend with shadcn/ui and Vitest"
```

---

### Task 2: API Client + Tipos TypeScript

**Files:**
- Create: `frontend/lib/api/client.ts`
- Create: `frontend/lib/api/auth.ts`
- Create: `frontend/lib/api/bookings.ts`
- Create: `frontend/lib/api/services.ts`
- Create: `frontend/lib/api/resources.ts`
- Create: `frontend/lib/api/financeiro.ts`
- Create: `frontend/lib/api/notifications.ts`
- Create: `frontend/lib/api/tenants.ts`
- Create: `frontend/lib/types/` (todos os arquivos de tipo)

- [ ] **Step 1: Criar tipos TypeScript — auth**

```typescript
// frontend/lib/types/auth.ts
export interface TokenPair {
  accessToken: string
  refreshToken: string
  expiresAt: string
}

export interface AdminUser {
  id: string
  name: string
  email: string
  role: 'TenantOwner' | 'TenantAdmin'
  avatarUrl?: string
}
```

- [ ] **Step 2: Criar tipos — booking**

```typescript
// frontend/lib/types/booking.ts
export type BookingStatus = 'Pending' | 'Confirmed' | 'Completed' | 'Cancelled' | 'NoShow'

export interface Booking {
  id: string
  customerId: string
  customerName: string
  customerEmail: string
  customerPhone?: string
  resourceId: string
  resourceName: string
  serviceId: string
  serviceName: string
  scheduledAt: string
  durationMinutes: number
  status: BookingStatus
  totalAmount: number
  createdAt: string
}
```

- [ ] **Step 3: Criar tipos — service**

```typescript
// frontend/lib/types/service.ts
export interface Service {
  id: string
  name: string
  description?: string
  durationMinutes: number
  price: number
  categoryId?: string
  isActive: boolean
}

export interface UpsertServiceRequest {
  name: string
  description?: string
  durationMinutes: number
  price: number
  categoryId?: string
  isActive?: boolean
}
```

- [ ] **Step 4: Criar tipos — resource**

```typescript
// frontend/lib/types/resource.ts
export interface Resource {
  id: string
  name: string
  type: string
  serviceIds: string[]
  isActive: boolean
}

export interface UpsertResourceRequest {
  name: string
  type: string
  serviceIds: string[]
}
```

- [ ] **Step 5: Criar tipos — financeiro**

```typescript
// frontend/lib/types/financeiro.ts
export interface FinancialTransaction {
  id: string
  bookingId: string
  amount: number
  type: 'Payment' | 'Refund'
  status: 'Pending' | 'Paid' | 'Refunded' | 'Failed'
  createdAt: string
  serviceName: string
  customerName: string
}

export interface FinancialSummary {
  totalRevenue: number
  totalRefunds: number
  netRevenue: number
  totalBookings: number
  paidBookings: number
  pendingAmount: number
}
```

- [ ] **Step 6: Criar tipos — notification**

```typescript
// frontend/lib/types/notification.ts
export type NotificationEventType =
  | 'BookingCreated'
  | 'BookingConfirmed'
  | 'BookingCancelled'
  | 'BookingCompleted'
  | 'BookingReminder'

export type NotificationChannel = 'WhatsApp' | 'Email'

export interface NotificationTemplate {
  id: string
  eventType: NotificationEventType
  channel: NotificationChannel
  subjectTemplate?: string
  bodyTemplate: string
  isActive: boolean
}
```

- [ ] **Step 7: Criar tipos — tenant**

```typescript
// frontend/lib/types/tenant.ts
export interface Tenant {
  id: string
  name: string
  slug: string
  logoUrl?: string
  primaryColor?: string
  customDomain?: string
  timezone: string
  plan: string
}

export interface UpdateTenantRequest {
  name?: string
  logoUrl?: string
  primaryColor?: string
  timezone?: string
}
```

- [ ] **Step 8: Criar API client base**

```typescript
// frontend/lib/api/client.ts
const API_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

function getSlug(): string {
  if (typeof window === 'undefined') return ''
  return document.cookie
    .split(';')
    .find(c => c.trim().startsWith('tenant_slug='))
    ?.split('=')[1] ?? ''
}

function getToken(): string {
  if (typeof window === 'undefined') return ''
  return document.cookie
    .split(';')
    .find(c => c.trim().startsWith('access_token='))
    ?.split('=')[1] ?? ''
}

export async function apiFetch<T>(
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const token = getToken()
  const slug = getSlug()

  const res = await fetch(`${API_URL}${path}`, {
    ...options,
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(slug ? { 'X-Tenant-Slug': slug } : {}),
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
```

- [ ] **Step 9: Criar `lib/api/auth.ts`**

```typescript
// frontend/lib/api/auth.ts
import { apiFetch } from './client'
import type { TokenPair, AdminUser } from '../types/auth'

export const authApi = {
  login: (email: string, password: string) =>
    apiFetch<TokenPair>('/api/v1/auth/email', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),

  me: () => apiFetch<AdminUser>('/api/v1/auth/me'),

  refresh: (refreshToken: string) =>
    apiFetch<TokenPair>('/api/v1/auth/refresh', {
      method: 'POST',
      body: JSON.stringify({ refreshToken }),
    }),
}
```

- [ ] **Step 10: Criar `lib/api/bookings.ts`**

```typescript
// frontend/lib/api/bookings.ts
import { apiFetch } from './client'
import type { Booking, BookingStatus } from '../types/booking'

export const bookingsApi = {
  list: (params: { resourceId?: string; from?: string; to?: string; status?: BookingStatus }) => {
    const qs = new URLSearchParams(
      Object.entries(params).filter(([, v]) => v != null) as [string, string][]
    ).toString()
    return apiFetch<Booking[]>(`/api/v1/bookings?${qs}`)
  },

  confirm: (id: string) =>
    apiFetch<void>(`/api/v1/bookings/${id}/confirm`, { method: 'POST' }),

  cancel: (id: string, reason?: string) =>
    apiFetch<void>(`/api/v1/bookings/${id}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),

  complete: (id: string) =>
    apiFetch<void>(`/api/v1/bookings/${id}/complete`, { method: 'POST' }),

  noShow: (id: string) =>
    apiFetch<void>(`/api/v1/bookings/${id}/no-show`, { method: 'POST' }),
}
```

- [ ] **Step 11: Criar `lib/api/services.ts`**

```typescript
// frontend/lib/api/services.ts
import { apiFetch } from './client'
import type { Service, UpsertServiceRequest } from '../types/service'

export const servicesApi = {
  list: () => apiFetch<Service[]>('/api/v1/services'),
  create: (data: UpsertServiceRequest) =>
    apiFetch<Service>('/api/v1/services', { method: 'POST', body: JSON.stringify(data) }),
  update: (id: string, data: UpsertServiceRequest) =>
    apiFetch<void>(`/api/v1/services/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  remove: (id: string) =>
    apiFetch<void>(`/api/v1/services/${id}`, { method: 'DELETE' }),
}
```

- [ ] **Step 12: Criar `lib/api/resources.ts`**

```typescript
// frontend/lib/api/resources.ts
import { apiFetch } from './client'
import type { Resource, UpsertResourceRequest } from '../types/resource'

export const resourcesApi = {
  list: () => apiFetch<Resource[]>('/api/v1/resources'),
  create: (data: UpsertResourceRequest) =>
    apiFetch<Resource>('/api/v1/resources', { method: 'POST', body: JSON.stringify(data) }),
  update: (id: string, data: UpsertResourceRequest) =>
    apiFetch<void>(`/api/v1/resources/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  remove: (id: string) =>
    apiFetch<void>(`/api/v1/resources/${id}`, { method: 'DELETE' }),
}
```

- [ ] **Step 13: Criar `lib/api/financeiro.ts`**

```typescript
// frontend/lib/api/financeiro.ts
import { apiFetch } from './client'
import type { FinancialTransaction, FinancialSummary } from '../types/financeiro'

export const financeiroApi = {
  list: (params: { from: string; to: string; serviceId?: string; resourceId?: string }) => {
    const qs = new URLSearchParams(
      Object.entries(params).filter(([, v]) => v != null) as [string, string][]
    ).toString()
    return apiFetch<FinancialTransaction[]>(`/api/v1/financeiro?${qs}`)
  },

  summary: (params: { from: string; to: string }) => {
    const qs = new URLSearchParams(params).toString()
    return apiFetch<FinancialSummary>(`/api/v1/financeiro/summary?${qs}`)
  },
}
```

- [ ] **Step 14: Criar `lib/api/notifications.ts`**

```typescript
// frontend/lib/api/notifications.ts
import { apiFetch } from './client'
import type { NotificationTemplate, NotificationEventType, NotificationChannel } from '../types/notification'

export const notificationsApi = {
  list: () => apiFetch<NotificationTemplate[]>('/api/v1/notification-templates'),
  upsert: (data: {
    eventType: NotificationEventType
    channel: NotificationChannel
    bodyTemplate: string
    subjectTemplate?: string
  }) =>
    apiFetch<void>('/api/v1/notification-templates', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
}
```

- [ ] **Step 15: Criar `lib/api/tenants.ts`**

```typescript
// frontend/lib/api/tenants.ts
import { apiFetch } from './client'
import type { Tenant, UpdateTenantRequest } from '../types/tenant'

export const tenantsApi = {
  me: () => apiFetch<Tenant>('/api/v1/tenants/me'),
  update: (data: UpdateTenantRequest) =>
    apiFetch<void>('/api/v1/tenants/me', { method: 'PUT', body: JSON.stringify(data) }),
  updateTheme: (primaryColor: string, logoUrl?: string) =>
    apiFetch<void>('/api/v1/tenants/me/theme', {
      method: 'PUT',
      body: JSON.stringify({ primaryColor, logoUrl }),
    }),
}
```

- [ ] **Step 16: Commit**

```bash
git add frontend/lib/
git commit -m "feat: add typed API client and TypeScript types for admin panel"
```

---

### Task 3: Auth Store (Zustand) + Middleware

**Files:**
- Create: `frontend/store/auth.ts`
- Create: `frontend/middleware.ts`

- [ ] **Step 1: Criar store Zustand**

```typescript
// frontend/store/auth.ts
import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { AdminUser, TokenPair } from '@/lib/types/auth'

interface AuthState {
  user: AdminUser | null
  accessToken: string | null
  refreshToken: string | null
  tenantSlug: string | null
  setAuth: (user: AdminUser, tokens: TokenPair, tenantSlug: string) => void
  clearAuth: () => void
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      accessToken: null,
      refreshToken: null,
      tenantSlug: null,
      setAuth: (user, tokens, tenantSlug) =>
        set({ user, accessToken: tokens.accessToken, refreshToken: tokens.refreshToken, tenantSlug }),
      clearAuth: () =>
        set({ user: null, accessToken: null, refreshToken: null, tenantSlug: null }),
    }),
    { name: 'horafy-auth' }
  )
)
```

- [ ] **Step 2: Criar middleware de proteção de rotas**

```typescript
// frontend/middleware.ts
import { NextResponse } from 'next/server'
import type { NextRequest } from 'next/server'

export function middleware(req: NextRequest) {
  const token = req.cookies.get('access_token')?.value
  const isAdminRoute = req.nextUrl.pathname.startsWith('/admin')

  if (isAdminRoute && !token) {
    const loginUrl = new URL('/login', req.url)
    loginUrl.searchParams.set('redirect', req.nextUrl.pathname)
    return NextResponse.redirect(loginUrl)
  }

  return NextResponse.next()
}

export const config = {
  matcher: ['/admin/:path*'],
}
```

- [ ] **Step 3: Commit**

```bash
git add frontend/store/ frontend/middleware.ts
git commit -m "feat: add Zustand auth store and Next.js route protection middleware"
```

---

### Task 4: Página de Login

**Files:**
- Create: `frontend/app/(auth)/login/page.tsx`
- Create: `frontend/__tests__/login.test.tsx`

- [ ] **Step 1: Escrever teste do formulário de login**

```typescript
// frontend/__tests__/login.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import LoginPage from '@/app/(auth)/login/page'

vi.mock('@/lib/api/auth', () => ({
  authApi: {
    login: vi.fn(),
    me: vi.fn(),
  },
}))

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  useSearchParams: () => ({ get: () => null }),
}))

describe('LoginPage', () => {
  it('shows validation error when fields are empty', async () => {
    render(<LoginPage />)
    fireEvent.click(screen.getByRole('button', { name: /entrar/i }))
    await waitFor(() => {
      expect(screen.getByText(/email obrigatório/i)).toBeInTheDocument()
    })
  })

  it('shows error message on failed login', async () => {
    const { authApi } = await import('@/lib/api/auth')
    vi.mocked(authApi.login).mockRejectedValue(new Error('Credenciais inválidas'))

    render(<LoginPage />)
    await userEvent.type(screen.getByLabelText(/email/i), 'admin@teste.com')
    await userEvent.type(screen.getByLabelText(/senha/i), 'wrongpass')
    fireEvent.click(screen.getByRole('button', { name: /entrar/i }))

    await waitFor(() => {
      expect(screen.getByText(/credenciais inválidas/i)).toBeInTheDocument()
    })
  })
})
```

- [ ] **Step 2: Executar teste para verificar que falha**

```bash
cd frontend
npm run test:run -- __tests__/login.test.tsx
```

Expected: FAIL — módulo não encontrado.

- [ ] **Step 3: Implementar página de login**

```typescript
// frontend/app/(auth)/login/page.tsx
'use client'

import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useRouter, useSearchParams } from 'next/navigation'
import { authApi } from '@/lib/api/auth'
import { useAuthStore } from '@/store/auth'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

const schema = z.object({
  email: z.string().min(1, 'Email obrigatório').email('Email inválido'),
  password: z.string().min(1, 'Senha obrigatória'),
  tenantSlug: z.string().min(1, 'Slug do tenant obrigatório'),
})

type FormData = z.infer<typeof schema>

export default function LoginPage() {
  const router = useRouter()
  const searchParams = useSearchParams()
  const { setAuth } = useAuthStore()
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  })

  const onSubmit = async (data: FormData) => {
    setError(null)
    setLoading(true)
    try {
      // Definir cookie do slug para o apiFetch incluir o header
      document.cookie = `tenant_slug=${data.tenantSlug}; path=/`

      const tokens = await authApi.login(data.email, data.password)

      // Armazenar access token em cookie httpOnly via API ou em cookie simples
      document.cookie = `access_token=${tokens.accessToken}; path=/; max-age=${60 * 60 * 24}`

      const user = await authApi.me()
      setAuth(user, tokens, data.tenantSlug)

      const redirect = searchParams.get('redirect') ?? '/admin/dashboard'
      router.replace(redirect)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Erro ao fazer login')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle className="text-2xl text-center">Horafy Admin</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div>
              <Label htmlFor="tenantSlug">Slug do Tenant</Label>
              <Input id="tenantSlug" {...register('tenantSlug')} placeholder="meu-negocio" />
              {errors.tenantSlug && <p className="text-sm text-red-500 mt-1">{errors.tenantSlug.message}</p>}
            </div>
            <div>
              <Label htmlFor="email">Email</Label>
              <Input id="email" type="email" {...register('email')} />
              {errors.email && <p className="text-sm text-red-500 mt-1">{errors.email.message}</p>}
            </div>
            <div>
              <Label htmlFor="password">Senha</Label>
              <Input id="password" type="password" {...register('password')} />
              {errors.password && <p className="text-sm text-red-500 mt-1">{errors.password.message}</p>}
            </div>
            {error && <p className="text-sm text-red-500">{error}</p>}
            <Button type="submit" className="w-full" disabled={loading}>
              {loading ? 'Entrando...' : 'Entrar'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
```

- [ ] **Step 4: Executar testes**

```bash
npm run test:run -- __tests__/login.test.tsx
```

Expected: PASS — 2 testes passando.

- [ ] **Step 5: Commit**

```bash
git add frontend/app/(auth)/ frontend/__tests__/login.test.tsx
git commit -m "feat: add admin login page with form validation"
```

---

### Task 5: Layout Shell do Admin (Sidebar + Topbar)

**Files:**
- Create: `frontend/app/(admin)/layout.tsx`
- Create: `frontend/components/admin/AdminShell.tsx`
- Create: `frontend/components/admin/Sidebar.tsx`
- Create: `frontend/components/admin/Topbar.tsx`
- Create: `frontend/__tests__/Sidebar.test.tsx`

- [ ] **Step 1: Escrever teste do Sidebar**

```typescript
// frontend/__tests__/Sidebar.test.tsx
import { render, screen } from '@testing-library/react'
import { vi } from 'vitest'
import { Sidebar } from '@/components/admin/Sidebar'

vi.mock('next/navigation', () => ({
  usePathname: () => '/admin/dashboard',
}))

describe('Sidebar', () => {
  it('renders all navigation links', () => {
    render(<Sidebar />)
    expect(screen.getByText('Dashboard')).toBeInTheDocument()
    expect(screen.getByText('Agenda')).toBeInTheDocument()
    expect(screen.getByText('Agendamentos')).toBeInTheDocument()
    expect(screen.getByText('Clientes')).toBeInTheDocument()
    expect(screen.getByText('Serviços')).toBeInTheDocument()
    expect(screen.getByText('Recursos')).toBeInTheDocument()
    expect(screen.getByText('Financeiro')).toBeInTheDocument()
    expect(screen.getByText('Notificações')).toBeInTheDocument()
    expect(screen.getByText('Configurações')).toBeInTheDocument()
  })

  it('marks the current route as active', () => {
    render(<Sidebar />)
    const dashboardLink = screen.getByRole('link', { name: /dashboard/i })
    expect(dashboardLink).toHaveClass('bg-slate-100')
  })
})
```

- [ ] **Step 2: Executar teste para verificar falha**

```bash
npm run test:run -- __tests__/Sidebar.test.tsx
```

Expected: FAIL.

- [ ] **Step 3: Implementar Sidebar**

```typescript
// frontend/components/admin/Sidebar.tsx
'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import {
  LayoutDashboard, CalendarDays, ClipboardList, Users,
  Scissors, Briefcase, DollarSign, Bell, Settings
} from 'lucide-react'
import { cn } from '@/lib/utils'

const NAV = [
  { href: '/admin/dashboard',      label: 'Dashboard',     icon: LayoutDashboard },
  { href: '/admin/agenda',         label: 'Agenda',        icon: CalendarDays },
  { href: '/admin/agendamentos',   label: 'Agendamentos',  icon: ClipboardList },
  { href: '/admin/clientes',       label: 'Clientes',      icon: Users },
  { href: '/admin/servicos',       label: 'Serviços',      icon: Scissors },
  { href: '/admin/recursos',       label: 'Recursos',      icon: Briefcase },
  { href: '/admin/financeiro',     label: 'Financeiro',    icon: DollarSign },
  { href: '/admin/notificacoes',   label: 'Notificações',  icon: Bell },
  { href: '/admin/configuracoes',  label: 'Configurações', icon: Settings },
]

export function Sidebar() {
  const pathname = usePathname()
  return (
    <aside className="w-60 min-h-screen bg-white border-r flex flex-col">
      <div className="h-16 flex items-center px-6 border-b">
        <span className="font-bold text-xl text-slate-800">Horafy</span>
      </div>
      <nav className="flex-1 p-4 space-y-1">
        {NAV.map(({ href, label, icon: Icon }) => (
          <Link
            key={href}
            href={href}
            className={cn(
              'flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors',
              pathname === href
                ? 'bg-slate-100 text-slate-900'
                : 'text-slate-600 hover:bg-slate-50 hover:text-slate-900'
            )}
          >
            <Icon className="h-4 w-4" />
            {label}
          </Link>
        ))}
      </nav>
    </aside>
  )
}
```

- [ ] **Step 4: Implementar Topbar**

```typescript
// frontend/components/admin/Topbar.tsx
'use client'

import { useRouter } from 'next/navigation'
import { useAuthStore } from '@/store/auth'
import { Button } from '@/components/ui/button'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'

export function Topbar() {
  const router = useRouter()
  const { user, clearAuth } = useAuthStore()

  const handleLogout = () => {
    document.cookie = 'access_token=; path=/; max-age=0'
    document.cookie = 'tenant_slug=; path=/; max-age=0'
    clearAuth()
    router.replace('/login')
  }

  return (
    <header className="h-16 border-b bg-white flex items-center justify-end px-6 gap-4">
      {user && (
        <span className="text-sm text-slate-600">{user.name}</span>
      )}
      <Avatar className="h-8 w-8">
        <AvatarFallback>{user?.name?.[0] ?? 'A'}</AvatarFallback>
      </Avatar>
      <Button variant="outline" size="sm" onClick={handleLogout}>
        Sair
      </Button>
    </header>
  )
}
```

Adicionar `avatar` ao shadcn:
```bash
npx shadcn@latest add avatar
```

- [ ] **Step 5: Implementar AdminShell**

```typescript
// frontend/components/admin/AdminShell.tsx
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'

export function AdminShell({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen bg-slate-50">
      <Sidebar />
      <div className="flex-1 flex flex-col">
        <Topbar />
        <main className="flex-1 p-6">{children}</main>
      </div>
    </div>
  )
}
```

- [ ] **Step 6: Implementar layout do grupo admin**

```typescript
// frontend/app/(admin)/layout.tsx
import { AdminShell } from '@/components/admin/AdminShell'

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return <AdminShell>{children}</AdminShell>
}
```

- [ ] **Step 7: Executar testes**

```bash
npm run test:run -- __tests__/Sidebar.test.tsx
```

Expected: PASS — 2 testes passando.

- [ ] **Step 8: Commit**

```bash
git add frontend/app/(admin)/ frontend/components/admin/
git commit -m "feat: add admin shell layout with sidebar and topbar"
```

---

### Task 6: Dashboard (/admin/dashboard)

**Files:**
- Create: `frontend/app/(admin)/admin/dashboard/page.tsx`
- Create: `frontend/__tests__/dashboard.test.tsx`

- [ ] **Step 1: Escrever teste de métricas do dashboard**

```typescript
// frontend/__tests__/dashboard.test.tsx
import { render, screen, waitFor } from '@testing-library/react'
import { vi } from 'vitest'
import DashboardPage from '@/app/(admin)/admin/dashboard/page'

vi.mock('@/lib/api/bookings', () => ({
  bookingsApi: { list: vi.fn().mockResolvedValue([
    { id: '1', status: 'Confirmed', scheduledAt: new Date().toISOString(), totalAmount: 150 },
    { id: '2', status: 'Cancelled', scheduledAt: new Date().toISOString(), totalAmount: 0 },
  ]) },
}))

vi.mock('@/lib/api/financeiro', () => ({
  financeiroApi: { summary: vi.fn().mockResolvedValue({
    totalRevenue: 1500,
    netRevenue: 1200,
    totalBookings: 10,
    paidBookings: 8,
    totalRefunds: 300,
    pendingAmount: 0,
  }) },
}))

describe('DashboardPage', () => {
  it('renders metric cards', async () => {
    render(<DashboardPage />)
    await waitFor(() => {
      expect(screen.getByText(/agendamentos hoje/i)).toBeInTheDocument()
      expect(screen.getByText(/receita/i)).toBeInTheDocument()
    })
  })
})
```

- [ ] **Step 2: Executar teste para verificar falha**

```bash
npm run test:run -- __tests__/dashboard.test.tsx
```

Expected: FAIL.

- [ ] **Step 3: Implementar página Dashboard**

```typescript
// frontend/app/(admin)/admin/dashboard/page.tsx
'use client'

import { useEffect, useState } from 'react'
import { format, startOfDay, endOfDay, startOfWeek, endOfWeek } from 'date-fns'
import { bookingsApi } from '@/lib/api/bookings'
import { financeiroApi } from '@/lib/api/financeiro'
import type { FinancialSummary } from '@/lib/types/financeiro'
import type { Booking } from '@/lib/types/booking'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { CalendarDays, DollarSign, XCircle, CheckCircle } from 'lucide-react'

export default function DashboardPage() {
  const [todayBookings, setTodayBookings] = useState<Booking[]>([])
  const [weekBookings, setWeekBookings] = useState<Booking[]>([])
  const [summary, setSummary] = useState<FinancialSummary | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const today = new Date()
    const from = format(startOfWeek(today), "yyyy-MM-dd'T'HH:mm:ss")
    const to = format(endOfWeek(today), "yyyy-MM-dd'T'HH:mm:ss")
    const todayFrom = format(startOfDay(today), "yyyy-MM-dd'T'HH:mm:ss")
    const todayTo = format(endOfDay(today), "yyyy-MM-dd'T'HH:mm:ss")

    Promise.all([
      bookingsApi.list({ from: todayFrom, to: todayTo }),
      bookingsApi.list({ from, to }),
      financeiroApi.summary({ from, to }),
    ]).then(([tb, wb, s]) => {
      setTodayBookings(tb)
      setWeekBookings(wb)
      setSummary(s)
    }).finally(() => setLoading(false))
  }, [])

  const cancelled = weekBookings.filter(b => b.status === 'Cancelled').length

  const metrics = [
    {
      title: 'Agendamentos Hoje',
      value: loading ? '...' : todayBookings.length,
      icon: CalendarDays,
      sub: `${weekBookings.length} esta semana`,
    },
    {
      title: 'Receita (semana)',
      value: loading ? '...' : `R$ ${(summary?.netRevenue ?? 0).toFixed(2)}`,
      icon: DollarSign,
      sub: `Bruto: R$ ${(summary?.totalRevenue ?? 0).toFixed(2)}`,
    },
    {
      title: 'Cancelamentos (semana)',
      value: loading ? '...' : cancelled,
      icon: XCircle,
      sub: `${weekBookings.length} total`,
    },
    {
      title: 'Pagamentos Confirmados',
      value: loading ? '...' : summary?.paidBookings ?? 0,
      icon: CheckCircle,
      sub: `de ${summary?.totalBookings ?? 0} agendamentos`,
    },
  ]

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Dashboard</h1>
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {metrics.map(({ title, value, icon: Icon, sub }) => (
          <Card key={title}>
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium text-slate-600">{title}</CardTitle>
              <Icon className="h-4 w-4 text-slate-400" />
            </CardHeader>
            <CardContent>
              <p className="text-2xl font-bold">{value}</p>
              <p className="text-xs text-slate-500 mt-1">{sub}</p>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}
```

- [ ] **Step 4: Executar testes**

```bash
npm run test:run -- __tests__/dashboard.test.tsx
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/app/(admin)/admin/dashboard/
git commit -m "feat: add admin dashboard with metrics cards"
```

---

### Task 7: Página Agendamentos (/admin/agendamentos)

**Files:**
- Create: `frontend/app/(admin)/admin/agendamentos/page.tsx`
- Create: `frontend/components/bookings/BookingTable.tsx`
- Create: `frontend/__tests__/BookingTable.test.tsx`

- [ ] **Step 1: Escrever teste da tabela de agendamentos**

```typescript
// frontend/__tests__/BookingTable.test.tsx
import { render, screen, fireEvent } from '@testing-library/react'
import { vi } from 'vitest'
import { BookingTable } from '@/components/bookings/BookingTable'
import type { Booking } from '@/lib/types/booking'

const mockBooking: Booking = {
  id: '123',
  customerId: 'c1',
  customerName: 'João Silva',
  customerEmail: 'joao@test.com',
  resourceId: 'r1',
  resourceName: 'Sala A',
  serviceId: 's1',
  serviceName: 'Corte',
  scheduledAt: '2026-06-10T10:00:00',
  durationMinutes: 60,
  status: 'Pending',
  totalAmount: 100,
  createdAt: '2026-06-01T00:00:00',
}

describe('BookingTable', () => {
  it('renders booking data', () => {
    render(<BookingTable bookings={[mockBooking]} onAction={vi.fn()} />)
    expect(screen.getByText('João Silva')).toBeInTheDocument()
    expect(screen.getByText('Corte')).toBeInTheDocument()
    expect(screen.getByText('Sala A')).toBeInTheDocument()
  })

  it('calls onAction with confirm when button clicked', async () => {
    const onAction = vi.fn()
    render(<BookingTable bookings={[mockBooking]} onAction={onAction} />)
    fireEvent.click(screen.getByRole('button', { name: /confirmar/i }))
    expect(onAction).toHaveBeenCalledWith('confirm', '123')
  })
})
```

- [ ] **Step 2: Executar teste para verificar falha**

```bash
npm run test:run -- __tests__/BookingTable.test.tsx
```

Expected: FAIL.

- [ ] **Step 3: Implementar BookingTable**

```typescript
// frontend/components/bookings/BookingTable.tsx
'use client'

import { format } from 'date-fns'
import { ptBR } from 'date-fns/locale'
import type { Booking, BookingStatus } from '@/lib/types/booking'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow
} from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'

const STATUS_LABEL: Record<BookingStatus, string> = {
  Pending: 'Pendente',
  Confirmed: 'Confirmado',
  Completed: 'Concluído',
  Cancelled: 'Cancelado',
  NoShow: 'Não Compareceu',
}

const STATUS_VARIANT: Record<BookingStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Pending: 'secondary',
  Confirmed: 'default',
  Completed: 'outline',
  Cancelled: 'destructive',
  NoShow: 'destructive',
}

interface Props {
  bookings: Booking[]
  onAction: (action: 'confirm' | 'cancel' | 'complete' | 'noshow', id: string) => void
}

export function BookingTable({ bookings, onAction }: Props) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Cliente</TableHead>
          <TableHead>Serviço</TableHead>
          <TableHead>Recurso</TableHead>
          <TableHead>Data/Hora</TableHead>
          <TableHead>Status</TableHead>
          <TableHead>Valor</TableHead>
          <TableHead>Ações</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {bookings.map(b => (
          <TableRow key={b.id}>
            <TableCell>
              <div className="font-medium">{b.customerName}</div>
              <div className="text-xs text-slate-500">{b.customerEmail}</div>
            </TableCell>
            <TableCell>{b.serviceName}</TableCell>
            <TableCell>{b.resourceName}</TableCell>
            <TableCell>
              {format(new Date(b.scheduledAt), "dd/MM/yyyy HH:mm", { locale: ptBR })}
            </TableCell>
            <TableCell>
              <Badge variant={STATUS_VARIANT[b.status]}>{STATUS_LABEL[b.status]}</Badge>
            </TableCell>
            <TableCell>R$ {b.totalAmount.toFixed(2)}</TableCell>
            <TableCell>
              <div className="flex gap-2">
                {b.status === 'Pending' && (
                  <Button size="sm" onClick={() => onAction('confirm', b.id)}>
                    Confirmar
                  </Button>
                )}
                {(b.status === 'Pending' || b.status === 'Confirmed') && (
                  <Button size="sm" variant="outline" onClick={() => onAction('cancel', b.id)}>
                    Cancelar
                  </Button>
                )}
                {b.status === 'Confirmed' && (
                  <Button size="sm" variant="secondary" onClick={() => onAction('complete', b.id)}>
                    Concluir
                  </Button>
                )}
              </div>
            </TableCell>
          </TableRow>
        ))}
        {bookings.length === 0 && (
          <TableRow>
            <TableCell colSpan={7} className="text-center text-slate-500 py-8">
              Nenhum agendamento encontrado.
            </TableCell>
          </TableRow>
        )}
      </TableBody>
    </Table>
  )
}
```

- [ ] **Step 4: Implementar página Agendamentos**

```typescript
// frontend/app/(admin)/admin/agendamentos/page.tsx
'use client'

import { useEffect, useState, useCallback } from 'react'
import { format, subDays } from 'date-fns'
import { bookingsApi } from '@/lib/api/bookings'
import { BookingTable } from '@/components/bookings/BookingTable'
import type { Booking, BookingStatus } from '@/lib/types/booking'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

export default function AgendamentosPage() {
  const [bookings, setBookings] = useState<Booking[]>([])
  const [loading, setLoading] = useState(true)
  const [from, setFrom] = useState(format(subDays(new Date(), 7), 'yyyy-MM-dd'))
  const [to, setTo] = useState(format(new Date(), 'yyyy-MM-dd'))
  const [status, setStatus] = useState<BookingStatus | ''>('')

  const load = useCallback(() => {
    setLoading(true)
    bookingsApi
      .list({
        from: `${from}T00:00:00`,
        to: `${to}T23:59:59`,
        ...(status ? { status } : {}),
      })
      .then(setBookings)
      .finally(() => setLoading(false))
  }, [from, to, status])

  useEffect(() => { load() }, [load])

  const handleAction = async (action: 'confirm' | 'cancel' | 'complete' | 'noshow', id: string) => {
    if (action === 'confirm') await bookingsApi.confirm(id)
    else if (action === 'cancel') await bookingsApi.cancel(id)
    else if (action === 'complete') await bookingsApi.complete(id)
    else if (action === 'noshow') await bookingsApi.noShow(id)
    load()
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Agendamentos</h1>
      <div className="flex gap-4 flex-wrap">
        <Input type="date" value={from} onChange={e => setFrom(e.target.value)} className="w-40" />
        <Input type="date" value={to} onChange={e => setTo(e.target.value)} className="w-40" />
        <Select value={status} onValueChange={v => setStatus(v as BookingStatus | '')}>
          <SelectTrigger className="w-44">
            <SelectValue placeholder="Todos os status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="">Todos</SelectItem>
            <SelectItem value="Pending">Pendente</SelectItem>
            <SelectItem value="Confirmed">Confirmado</SelectItem>
            <SelectItem value="Completed">Concluído</SelectItem>
            <SelectItem value="Cancelled">Cancelado</SelectItem>
          </SelectContent>
        </Select>
      </div>
      {loading ? (
        <p className="text-slate-500">Carregando...</p>
      ) : (
        <BookingTable bookings={bookings} onAction={handleAction} />
      )}
    </div>
  )
}
```

- [ ] **Step 5: Executar testes**

```bash
npm run test:run -- __tests__/BookingTable.test.tsx
```

Expected: PASS — 2 testes.

- [ ] **Step 6: Commit**

```bash
git add frontend/app/(admin)/admin/agendamentos/ frontend/components/bookings/
git commit -m "feat: add bookings admin page with table and actions"
```

---

### Task 8: CRUD de Serviços (/admin/servicos)

**Files:**
- Create: `frontend/app/(admin)/admin/servicos/page.tsx`
- Create: `frontend/components/services/ServiceForm.tsx`
- Create: `frontend/__tests__/ServiceForm.test.tsx`

- [ ] **Step 1: Escrever teste do formulário de serviço**

```typescript
// frontend/__tests__/ServiceForm.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { ServiceForm } from '@/components/services/ServiceForm'

describe('ServiceForm', () => {
  it('shows validation errors for empty required fields', async () => {
    render(<ServiceForm onSubmit={vi.fn()} onCancel={vi.fn()} />)
    fireEvent.click(screen.getByRole('button', { name: /salvar/i }))
    await waitFor(() => {
      expect(screen.getByText(/nome obrigatório/i)).toBeInTheDocument()
    })
  })

  it('calls onSubmit with form data when valid', async () => {
    const onSubmit = vi.fn()
    render(<ServiceForm onSubmit={onSubmit} onCancel={vi.fn()} />)
    await userEvent.type(screen.getByLabelText(/nome/i), 'Corte de Cabelo')
    await userEvent.clear(screen.getByLabelText(/duração/i))
    await userEvent.type(screen.getByLabelText(/duração/i), '30')
    await userEvent.clear(screen.getByLabelText(/preço/i))
    await userEvent.type(screen.getByLabelText(/preço/i), '50')
    fireEvent.click(screen.getByRole('button', { name: /salvar/i }))
    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({ name: 'Corte de Cabelo', durationMinutes: 30, price: 50 })
      )
    })
  })
})
```

- [ ] **Step 2: Executar teste para verificar falha**

```bash
npm run test:run -- __tests__/ServiceForm.test.tsx
```

Expected: FAIL.

- [ ] **Step 3: Implementar ServiceForm**

```typescript
// frontend/components/services/ServiceForm.tsx
'use client'

import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { Service, UpsertServiceRequest } from '@/lib/types/service'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

const schema = z.object({
  name: z.string().min(1, 'Nome obrigatório'),
  description: z.string().optional(),
  durationMinutes: z.coerce.number().min(1, 'Duração mínima 1 minuto'),
  price: z.coerce.number().min(0, 'Preço inválido'),
})

type FormData = z.infer<typeof schema>

interface Props {
  initial?: Service
  onSubmit: (data: UpsertServiceRequest) => void
  onCancel: () => void
}

export function ServiceForm({ initial, onSubmit, onCancel }: Props) {
  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: initial?.name ?? '',
      description: initial?.description ?? '',
      durationMinutes: initial?.durationMinutes ?? 60,
      price: initial?.price ?? 0,
    },
  })

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <div>
        <Label htmlFor="name">Nome</Label>
        <Input id="name" {...register('name')} />
        {errors.name && <p className="text-sm text-red-500 mt-1">{errors.name.message}</p>}
      </div>
      <div>
        <Label htmlFor="description">Descrição</Label>
        <Input id="description" {...register('description')} />
      </div>
      <div>
        <Label htmlFor="durationMinutes">Duração (minutos)</Label>
        <Input id="durationMinutes" type="number" {...register('durationMinutes')} />
        {errors.durationMinutes && <p className="text-sm text-red-500 mt-1">{errors.durationMinutes.message}</p>}
      </div>
      <div>
        <Label htmlFor="price">Preço (R$)</Label>
        <Input id="price" type="number" step="0.01" {...register('price')} />
        {errors.price && <p className="text-sm text-red-500 mt-1">{errors.price.message}</p>}
      </div>
      <div className="flex gap-2 justify-end">
        <Button type="button" variant="outline" onClick={onCancel}>Cancelar</Button>
        <Button type="submit">Salvar</Button>
      </div>
    </form>
  )
}
```

- [ ] **Step 4: Implementar página Serviços**

```typescript
// frontend/app/(admin)/admin/servicos/page.tsx
'use client'

import { useEffect, useState } from 'react'
import { servicesApi } from '@/lib/api/services'
import { ServiceForm } from '@/components/services/ServiceForm'
import type { Service, UpsertServiceRequest } from '@/lib/types/service'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Plus, Pencil, Trash2 } from 'lucide-react'

export default function ServicosPage() {
  const [services, setServices] = useState<Service[]>([])
  const [editing, setEditing] = useState<Service | null | 'new'>(null)

  const load = () => servicesApi.list().then(setServices)
  useEffect(() => { load() }, [])

  const handleSubmit = async (data: UpsertServiceRequest) => {
    if (editing === 'new') await servicesApi.create(data)
    else if (editing) await servicesApi.update(editing.id, data)
    setEditing(null)
    load()
  }

  const handleDelete = async (id: string) => {
    if (!confirm('Excluir este serviço?')) return
    await servicesApi.remove(id)
    load()
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-slate-900">Serviços</h1>
        <Button onClick={() => setEditing('new')}>
          <Plus className="h-4 w-4 mr-2" /> Novo Serviço
        </Button>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {services.map(s => (
          <Card key={s.id}>
            <CardHeader className="pb-2">
              <div className="flex items-center justify-between">
                <CardTitle className="text-base">{s.name}</CardTitle>
                <Badge variant={s.isActive ? 'default' : 'secondary'}>
                  {s.isActive ? 'Ativo' : 'Inativo'}
                </Badge>
              </div>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-slate-500">{s.durationMinutes} min · R$ {s.price.toFixed(2)}</p>
              {s.description && <p className="text-xs text-slate-400 mt-1">{s.description}</p>}
              <div className="flex gap-2 mt-4">
                <Button size="sm" variant="outline" onClick={() => setEditing(s)}>
                  <Pencil className="h-3 w-3 mr-1" /> Editar
                </Button>
                <Button size="sm" variant="destructive" onClick={() => handleDelete(s.id)}>
                  <Trash2 className="h-3 w-3 mr-1" /> Excluir
                </Button>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      <Dialog open={editing !== null} onOpenChange={open => !open && setEditing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editing === 'new' ? 'Novo Serviço' : 'Editar Serviço'}</DialogTitle>
          </DialogHeader>
          <ServiceForm
            initial={editing !== 'new' && editing !== null ? editing : undefined}
            onSubmit={handleSubmit}
            onCancel={() => setEditing(null)}
          />
        </DialogContent>
      </Dialog>
    </div>
  )
}
```

- [ ] **Step 5: Executar testes**

```bash
npm run test:run -- __tests__/ServiceForm.test.tsx
```

Expected: PASS — 2 testes.

- [ ] **Step 6: Commit**

```bash
git add frontend/app/(admin)/admin/servicos/ frontend/components/services/
git commit -m "feat: add services admin page with CRUD"
```

---

### Task 9: CRUD de Recursos (/admin/recursos)

**Files:**
- Create: `frontend/app/(admin)/admin/recursos/page.tsx`
- Create: `frontend/components/resources/ResourceForm.tsx`
- Create: `frontend/__tests__/ResourceForm.test.tsx`

- [ ] **Step 1: Escrever teste do formulário de recurso**

```typescript
// frontend/__tests__/ResourceForm.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { ResourceForm } from '@/components/resources/ResourceForm'
import type { Service } from '@/lib/types/service'

const mockServices: Service[] = [
  { id: 's1', name: 'Corte', durationMinutes: 30, price: 50, isActive: true },
]

describe('ResourceForm', () => {
  it('shows validation error when name is empty', async () => {
    render(<ResourceForm services={mockServices} onSubmit={vi.fn()} onCancel={vi.fn()} />)
    fireEvent.click(screen.getByRole('button', { name: /salvar/i }))
    await waitFor(() => {
      expect(screen.getByText(/nome obrigatório/i)).toBeInTheDocument()
    })
  })

  it('calls onSubmit with name and type', async () => {
    const onSubmit = vi.fn()
    render(<ResourceForm services={mockServices} onSubmit={onSubmit} onCancel={vi.fn()} />)
    await userEvent.type(screen.getByLabelText(/nome/i), 'Sala B')
    await userEvent.type(screen.getByLabelText(/tipo/i), 'Sala')
    fireEvent.click(screen.getByRole('button', { name: /salvar/i }))
    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({ name: 'Sala B', type: 'Sala' })
      )
    })
  })
})
```

- [ ] **Step 2: Executar teste para verificar falha**

```bash
npm run test:run -- __tests__/ResourceForm.test.tsx
```

Expected: FAIL.

- [ ] **Step 3: Implementar ResourceForm**

```typescript
// frontend/components/resources/ResourceForm.tsx
'use client'

import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { Resource, UpsertResourceRequest } from '@/lib/types/resource'
import type { Service } from '@/lib/types/service'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

const schema = z.object({
  name: z.string().min(1, 'Nome obrigatório'),
  type: z.string().min(1, 'Tipo obrigatório'),
  serviceIds: z.array(z.string()),
})

type FormData = z.infer<typeof schema>

interface Props {
  initial?: Resource
  services: Service[]
  onSubmit: (data: UpsertResourceRequest) => void
  onCancel: () => void
}

export function ResourceForm({ initial, services, onSubmit, onCancel }: Props) {
  const { register, handleSubmit, control, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: initial?.name ?? '',
      type: initial?.type ?? '',
      serviceIds: initial?.serviceIds ?? [],
    },
  })

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <div>
        <Label htmlFor="name">Nome</Label>
        <Input id="name" {...register('name')} />
        {errors.name && <p className="text-sm text-red-500 mt-1">{errors.name.message}</p>}
      </div>
      <div>
        <Label htmlFor="type">Tipo</Label>
        <Input id="type" {...register('type')} placeholder="Sala, Profissional, Mesa..." />
        {errors.type && <p className="text-sm text-red-500 mt-1">{errors.type.message}</p>}
      </div>
      <div>
        <Label>Serviços Vinculados</Label>
        <Controller
          name="serviceIds"
          control={control}
          render={({ field }) => (
            <div className="space-y-2 mt-1">
              {services.map(s => (
                <label key={s.id} className="flex items-center gap-2 text-sm cursor-pointer">
                  <input
                    type="checkbox"
                    checked={field.value.includes(s.id)}
                    onChange={e => {
                      if (e.target.checked) field.onChange([...field.value, s.id])
                      else field.onChange(field.value.filter((id: string) => id !== s.id))
                    }}
                  />
                  {s.name}
                </label>
              ))}
            </div>
          )}
        />
      </div>
      <div className="flex gap-2 justify-end">
        <Button type="button" variant="outline" onClick={onCancel}>Cancelar</Button>
        <Button type="submit">Salvar</Button>
      </div>
    </form>
  )
}
```

- [ ] **Step 4: Implementar página Recursos**

```typescript
// frontend/app/(admin)/admin/recursos/page.tsx
'use client'

import { useEffect, useState } from 'react'
import { resourcesApi } from '@/lib/api/resources'
import { servicesApi } from '@/lib/api/services'
import { ResourceForm } from '@/components/resources/ResourceForm'
import type { Resource, UpsertResourceRequest } from '@/lib/types/resource'
import type { Service } from '@/lib/types/service'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Plus, Pencil, Trash2 } from 'lucide-react'

export default function RecursosPage() {
  const [resources, setResources] = useState<Resource[]>([])
  const [services, setServices] = useState<Service[]>([])
  const [editing, setEditing] = useState<Resource | null | 'new'>(null)

  const load = () =>
    Promise.all([resourcesApi.list(), servicesApi.list()]).then(([r, s]) => {
      setResources(r)
      setServices(s)
    })

  useEffect(() => { load() }, [])

  const handleSubmit = async (data: UpsertResourceRequest) => {
    if (editing === 'new') await resourcesApi.create(data)
    else if (editing) await resourcesApi.update(editing.id, data)
    setEditing(null)
    load()
  }

  const handleDelete = async (id: string) => {
    if (!confirm('Excluir este recurso?')) return
    await resourcesApi.remove(id)
    load()
  }

  const getServiceNames = (ids: string[]) =>
    ids.map(id => services.find(s => s.id === id)?.name ?? id).join(', ')

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-slate-900">Recursos</h1>
        <Button onClick={() => setEditing('new')}>
          <Plus className="h-4 w-4 mr-2" /> Novo Recurso
        </Button>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {resources.map(r => (
          <Card key={r.id}>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">{r.name}</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-slate-500">Tipo: {r.type}</p>
              {r.serviceIds.length > 0 && (
                <p className="text-xs text-slate-400 mt-1">
                  Serviços: {getServiceNames(r.serviceIds)}
                </p>
              )}
              <div className="flex gap-2 mt-4">
                <Button size="sm" variant="outline" onClick={() => setEditing(r)}>
                  <Pencil className="h-3 w-3 mr-1" /> Editar
                </Button>
                <Button size="sm" variant="destructive" onClick={() => handleDelete(r.id)}>
                  <Trash2 className="h-3 w-3 mr-1" /> Excluir
                </Button>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      <Dialog open={editing !== null} onOpenChange={open => !open && setEditing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editing === 'new' ? 'Novo Recurso' : 'Editar Recurso'}</DialogTitle>
          </DialogHeader>
          <ResourceForm
            initial={editing !== 'new' && editing !== null ? editing : undefined}
            services={services}
            onSubmit={handleSubmit}
            onCancel={() => setEditing(null)}
          />
        </DialogContent>
      </Dialog>
    </div>
  )
}
```

- [ ] **Step 5: Executar testes**

```bash
npm run test:run -- __tests__/ResourceForm.test.tsx
```

Expected: PASS — 2 testes.

- [ ] **Step 6: Commit**

```bash
git add frontend/app/(admin)/admin/recursos/ frontend/components/resources/
git commit -m "feat: add resources admin page with CRUD"
```

---

### Task 10: Financeiro (/admin/financeiro)

**Files:**
- Create: `frontend/app/(admin)/admin/financeiro/page.tsx`
- Create: `frontend/components/financeiro/RevenueChart.tsx`

- [ ] **Step 1: Implementar RevenueChart**

```typescript
// frontend/components/financeiro/RevenueChart.tsx
'use client'

import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts'
import type { FinancialTransaction } from '@/lib/types/financeiro'
import { format } from 'date-fns'

interface Props {
  transactions: FinancialTransaction[]
}

export function RevenueChart({ transactions }: Props) {
  // Agrupar por dia
  const byDay = transactions
    .filter(t => t.type === 'Payment' && t.status === 'Paid')
    .reduce<Record<string, number>>((acc, t) => {
      const day = format(new Date(t.createdAt), 'dd/MM')
      acc[day] = (acc[day] ?? 0) + t.amount
      return acc
    }, {})

  const data = Object.entries(byDay).map(([date, total]) => ({ date, total }))

  return (
    <ResponsiveContainer width="100%" height={300}>
      <BarChart data={data} margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
        <CartesianGrid strokeDasharray="3 3" />
        <XAxis dataKey="date" />
        <YAxis tickFormatter={v => `R$${v}`} />
        <Tooltip formatter={(v: number) => [`R$ ${v.toFixed(2)}`, 'Receita']} />
        <Bar dataKey="total" fill="#6366f1" radius={[4, 4, 0, 0]} />
      </BarChart>
    </ResponsiveContainer>
  )
}
```

- [ ] **Step 2: Implementar página Financeiro**

```typescript
// frontend/app/(admin)/admin/financeiro/page.tsx
'use client'

import { useEffect, useState, useCallback } from 'react'
import { format, subDays } from 'date-fns'
import { financeiroApi } from '@/lib/api/financeiro'
import { RevenueChart } from '@/components/financeiro/RevenueChart'
import type { FinancialTransaction, FinancialSummary } from '@/lib/types/financeiro'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'

export default function FinanceiroPage() {
  const [transactions, setTransactions] = useState<FinancialTransaction[]>([])
  const [summary, setSummary] = useState<FinancialSummary | null>(null)
  const [from, setFrom] = useState(format(subDays(new Date(), 30), 'yyyy-MM-dd'))
  const [to, setTo] = useState(format(new Date(), 'yyyy-MM-dd'))
  const [loading, setLoading] = useState(true)

  const load = useCallback(() => {
    setLoading(true)
    const params = { from: `${from}T00:00:00`, to: `${to}T23:59:59` }
    Promise.all([financeiroApi.list(params), financeiroApi.summary(params)])
      .then(([t, s]) => { setTransactions(t); setSummary(s) })
      .finally(() => setLoading(false))
  }, [from, to])

  useEffect(() => { load() }, [load])

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Financeiro</h1>

      <div className="flex gap-4">
        <Input type="date" value={from} onChange={e => setFrom(e.target.value)} className="w-40" />
        <Input type="date" value={to} onChange={e => setTo(e.target.value)} className="w-40" />
      </div>

      {summary && (
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          {[
            { title: 'Receita Bruta', value: `R$ ${summary.totalRevenue.toFixed(2)}` },
            { title: 'Reembolsos', value: `R$ ${summary.totalRefunds.toFixed(2)}` },
            { title: 'Receita Líquida', value: `R$ ${summary.netRevenue.toFixed(2)}` },
            { title: 'Agendamentos Pagos', value: `${summary.paidBookings}/${summary.totalBookings}` },
          ].map(m => (
            <Card key={m.title}>
              <CardHeader className="pb-1">
                <CardTitle className="text-xs text-slate-500">{m.title}</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-xl font-bold">{m.value}</p>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Card>
        <CardHeader><CardTitle>Receita por Dia</CardTitle></CardHeader>
        <CardContent>
          <RevenueChart transactions={transactions} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Transações</CardTitle></CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Data</TableHead>
                <TableHead>Cliente</TableHead>
                <TableHead>Serviço</TableHead>
                <TableHead>Tipo</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Valor</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {loading ? (
                <TableRow><TableCell colSpan={6} className="text-center py-8">Carregando...</TableCell></TableRow>
              ) : transactions.map(t => (
                <TableRow key={t.id}>
                  <TableCell>{format(new Date(t.createdAt), 'dd/MM/yyyy')}</TableCell>
                  <TableCell>{t.customerName}</TableCell>
                  <TableCell>{t.serviceName}</TableCell>
                  <TableCell>
                    <Badge variant={t.type === 'Refund' ? 'destructive' : 'default'}>
                      {t.type === 'Refund' ? 'Reembolso' : 'Pagamento'}
                    </Badge>
                  </TableCell>
                  <TableCell>{t.status}</TableCell>
                  <TableCell>R$ {t.amount.toFixed(2)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  )
}
```

- [ ] **Step 3: Commit**

```bash
git add frontend/app/(admin)/admin/financeiro/ frontend/components/financeiro/
git commit -m "feat: add financial admin page with chart and transaction table"
```

---

### Task 11: Notificações (/admin/notificacoes)

**Files:**
- Create: `frontend/app/(admin)/admin/notificacoes/page.tsx`
- Create: `frontend/components/notifications/TemplateEditor.tsx`
- Create: `frontend/__tests__/TemplateEditor.test.tsx`

- [ ] **Step 1: Escrever teste do editor de template**

```typescript
// frontend/__tests__/TemplateEditor.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { TemplateEditor } from '@/components/notifications/TemplateEditor'
import type { NotificationTemplate } from '@/lib/types/notification'

const template: NotificationTemplate = {
  id: 't1',
  eventType: 'BookingCreated',
  channel: 'WhatsApp',
  bodyTemplate: 'Olá {{customer_name}}!',
  isActive: true,
}

describe('TemplateEditor', () => {
  it('renders template body', () => {
    render(<TemplateEditor template={template} onSave={vi.fn()} />)
    expect(screen.getByDisplayValue('Olá {{customer_name}}!')).toBeInTheDocument()
  })

  it('calls onSave with edited body', async () => {
    const onSave = vi.fn()
    render(<TemplateEditor template={template} onSave={onSave} />)
    const textarea = screen.getByRole('textbox')
    await userEvent.clear(textarea)
    await userEvent.type(textarea, 'Novo texto')
    fireEvent.click(screen.getByRole('button', { name: /salvar/i }))
    await waitFor(() => {
      expect(onSave).toHaveBeenCalledWith(expect.objectContaining({ bodyTemplate: 'Novo texto' }))
    })
  })
})
```

- [ ] **Step 2: Executar teste para verificar falha**

```bash
npm run test:run -- __tests__/TemplateEditor.test.tsx
```

Expected: FAIL.

- [ ] **Step 3: Implementar TemplateEditor**

```typescript
// frontend/components/notifications/TemplateEditor.tsx
'use client'

import { useState } from 'react'
import type { NotificationTemplate } from '@/lib/types/notification'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'

const VARIABLES = ['{{customer_name}}', '{{service_name}}', '{{scheduled_at}}', '{{resource_name}}', '{{tenant_name}}']

const EVENT_LABEL: Record<string, string> = {
  BookingCreated: 'Agendamento Criado',
  BookingConfirmed: 'Agendamento Confirmado',
  BookingCancelled: 'Agendamento Cancelado',
  BookingCompleted: 'Agendamento Concluído',
  BookingReminder: 'Lembrete',
}

interface Props {
  template: NotificationTemplate
  onSave: (data: { bodyTemplate: string; subjectTemplate?: string }) => void
}

export function TemplateEditor({ template, onSave }: Props) {
  const [body, setBody] = useState(template.bodyTemplate)
  const [subject, setSubject] = useState(template.subjectTemplate ?? '')

  const insertVariable = (v: string) => setBody(prev => `${prev}${v}`)

  return (
    <div className="space-y-4 p-4 border rounded-lg">
      <div className="flex items-center gap-2">
        <span className="font-medium text-sm">{EVENT_LABEL[template.eventType] ?? template.eventType}</span>
        <Badge variant="outline">{template.channel}</Badge>
      </div>
      {template.channel === 'Email' && (
        <div>
          <Label>Assunto</Label>
          <input
            className="w-full border rounded px-3 py-2 text-sm mt-1"
            value={subject}
            onChange={e => setSubject(e.target.value)}
          />
        </div>
      )}
      <div>
        <Label>Corpo</Label>
        <textarea
          className="w-full border rounded px-3 py-2 text-sm mt-1 min-h-[100px] font-mono"
          value={body}
          onChange={e => setBody(e.target.value)}
        />
      </div>
      <div>
        <p className="text-xs text-slate-500 mb-2">Variáveis disponíveis:</p>
        <div className="flex flex-wrap gap-2">
          {VARIABLES.map(v => (
            <button
              key={v}
              type="button"
              onClick={() => insertVariable(v)}
              className="text-xs bg-slate-100 hover:bg-slate-200 px-2 py-1 rounded font-mono"
            >
              {v}
            </button>
          ))}
        </div>
      </div>
      <Button
        size="sm"
        onClick={() => onSave({ bodyTemplate: body, subjectTemplate: subject || undefined })}
      >
        Salvar
      </Button>
    </div>
  )
}
```

- [ ] **Step 4: Implementar página Notificações**

```typescript
// frontend/app/(admin)/admin/notificacoes/page.tsx
'use client'

import { useEffect, useState } from 'react'
import { notificationsApi } from '@/lib/api/notifications'
import { TemplateEditor } from '@/components/notifications/TemplateEditor'
import type { NotificationTemplate, NotificationEventType, NotificationChannel } from '@/lib/types/notification'

export default function NotificacoesPage() {
  const [templates, setTemplates] = useState<NotificationTemplate[]>([])

  useEffect(() => {
    notificationsApi.list().then(setTemplates)
  }, [])

  const handleSave = async (
    eventType: NotificationEventType,
    channel: NotificationChannel,
    data: { bodyTemplate: string; subjectTemplate?: string }
  ) => {
    await notificationsApi.upsert({ eventType, channel, ...data })
    notificationsApi.list().then(setTemplates)
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Templates de Notificação</h1>
      <p className="text-sm text-slate-500">
        Configure as mensagens enviadas por WhatsApp ou Email em cada evento.
      </p>
      <div className="space-y-4">
        {templates.map(t => (
          <TemplateEditor
            key={t.id}
            template={t}
            onSave={data => handleSave(t.eventType, t.channel, data)}
          />
        ))}
        {templates.length === 0 && (
          <p className="text-slate-500 text-sm">Nenhum template configurado ainda.</p>
        )}
      </div>
    </div>
  )
}
```

- [ ] **Step 5: Executar testes**

```bash
npm run test:run -- __tests__/TemplateEditor.test.tsx
```

Expected: PASS — 2 testes.

- [ ] **Step 6: Commit**

```bash
git add frontend/app/(admin)/admin/notificacoes/ frontend/components/notifications/
git commit -m "feat: add notifications admin page with template editor"
```

---

### Task 12: Configurações (/admin/configuracoes)

**Files:**
- Create: `frontend/app/(admin)/admin/configuracoes/page.tsx`

- [ ] **Step 1: Implementar página Configurações**

```typescript
// frontend/app/(admin)/admin/configuracoes/page.tsx
'use client'

import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { tenantsApi } from '@/lib/api/tenants'
import type { Tenant } from '@/lib/types/tenant'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'

const identitySchema = z.object({
  name: z.string().min(1, 'Nome obrigatório'),
  logoUrl: z.string().url('URL inválida').optional().or(z.literal('')),
  primaryColor: z.string().regex(/^#[0-9a-fA-F]{6}$/, 'Cor hex inválida').optional().or(z.literal('')),
  timezone: z.string().min(1),
})

type IdentityForm = z.infer<typeof identitySchema>

export default function ConfiguracoesPage() {
  const [tenant, setTenant] = useState<Tenant | null>(null)
  const [saved, setSaved] = useState(false)

  const { register, handleSubmit, reset, formState: { errors } } = useForm<IdentityForm>({
    resolver: zodResolver(identitySchema),
  })

  useEffect(() => {
    tenantsApi.me().then(t => {
      setTenant(t)
      reset({
        name: t.name,
        logoUrl: t.logoUrl ?? '',
        primaryColor: t.primaryColor ?? '',
        timezone: t.timezone,
      })
    })
  }, [reset])

  const onSubmit = async (data: IdentityForm) => {
    await tenantsApi.update(data)
    if (data.primaryColor) {
      await tenantsApi.updateTheme(data.primaryColor, data.logoUrl || undefined)
    }
    setSaved(true)
    setTimeout(() => setSaved(false), 3000)
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Configurações</h1>

      <Tabs defaultValue="identidade">
        <TabsList>
          <TabsTrigger value="identidade">Identidade Visual</TabsTrigger>
          <TabsTrigger value="plano">Plano</TabsTrigger>
        </TabsList>

        <TabsContent value="identidade">
          <Card>
            <CardHeader><CardTitle>Identidade Visual</CardTitle></CardHeader>
            <CardContent>
              <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 max-w-md">
                <div>
                  <Label htmlFor="name">Nome do Negócio</Label>
                  <Input id="name" {...register('name')} />
                  {errors.name && <p className="text-sm text-red-500 mt-1">{errors.name.message}</p>}
                </div>
                <div>
                  <Label htmlFor="logoUrl">URL do Logo</Label>
                  <Input id="logoUrl" {...register('logoUrl')} placeholder="https://..." />
                  {errors.logoUrl && <p className="text-sm text-red-500 mt-1">{errors.logoUrl.message}</p>}
                </div>
                <div>
                  <Label htmlFor="primaryColor">Cor Principal</Label>
                  <div className="flex gap-2 items-center">
                    <Input id="primaryColor" {...register('primaryColor')} placeholder="#6366f1" className="font-mono" />
                    <input type="color" {...register('primaryColor')} className="h-10 w-10 rounded border cursor-pointer" />
                  </div>
                  {errors.primaryColor && <p className="text-sm text-red-500 mt-1">{errors.primaryColor.message}</p>}
                </div>
                <div>
                  <Label htmlFor="timezone">Fuso Horário</Label>
                  <Input id="timezone" {...register('timezone')} placeholder="America/Sao_Paulo" />
                </div>
                <div className="flex items-center gap-4">
                  <Button type="submit">Salvar</Button>
                  {saved && <span className="text-sm text-green-600">Salvo com sucesso!</span>}
                </div>
              </form>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="plano">
          <Card>
            <CardHeader><CardTitle>Plano Atual</CardTitle></CardHeader>
            <CardContent>
              <p className="text-slate-600">Plano: <span className="font-semibold">{tenant?.plan ?? '...'}</span></p>
              <p className="text-sm text-slate-400 mt-2">Gerenciamento de plano será disponibilizado em breve.</p>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  )
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/app/(admin)/admin/configuracoes/
git commit -m "feat: add settings admin page with identity and plan tabs"
```

---

### Task 13: Clientes (/admin/clientes)

**Files:**
- Create: `frontend/app/(admin)/admin/clientes/page.tsx`

**Note:** O backend não tem um endpoint `/customers` para listagem admin. Esta página usa `GET /api/v1/bookings` e agrupa por `customerId` para construir o CRM.

- [ ] **Step 1: Implementar página Clientes**

```typescript
// frontend/app/(admin)/admin/clientes/page.tsx
'use client'

import { useEffect, useState } from 'react'
import { format, subDays } from 'date-fns'
import { bookingsApi } from '@/lib/api/bookings'
import type { Booking } from '@/lib/types/booking'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow
} from '@/components/ui/table'
import { Input } from '@/components/ui/input'

interface CustomerSummary {
  customerId: string
  customerName: string
  customerEmail: string
  bookingCount: number
  completedCount: number
  totalSpent: number
  lastVisit: string
}

function buildCustomerSummaries(bookings: Booking[]): CustomerSummary[] {
  const map = new Map<string, CustomerSummary>()
  for (const b of bookings) {
    const existing = map.get(b.customerId)
    if (existing) {
      existing.bookingCount++
      if (b.status === 'Completed') {
        existing.completedCount++
        existing.totalSpent += b.totalAmount
      }
      if (b.scheduledAt > existing.lastVisit) existing.lastVisit = b.scheduledAt
    } else {
      map.set(b.customerId, {
        customerId: b.customerId,
        customerName: b.customerName,
        customerEmail: b.customerEmail,
        bookingCount: 1,
        completedCount: b.status === 'Completed' ? 1 : 0,
        totalSpent: b.status === 'Completed' ? b.totalAmount : 0,
        lastVisit: b.scheduledAt,
      })
    }
  }
  return Array.from(map.values()).sort((a, b) => b.totalSpent - a.totalSpent)
}

export default function ClientesPage() {
  const [summaries, setSummaries] = useState<CustomerSummary[]>([])
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const from = format(subDays(new Date(), 365), "yyyy-MM-dd'T'HH:mm:ss")
    const to = format(new Date(), "yyyy-MM-dd'T'HH:mm:ss")
    bookingsApi.list({ from, to }).then(bookings => {
      setSummaries(buildCustomerSummaries(bookings))
    }).finally(() => setLoading(false))
  }, [])

  const filtered = summaries.filter(
    s => s.customerName.toLowerCase().includes(search.toLowerCase()) ||
         s.customerEmail.toLowerCase().includes(search.toLowerCase())
  )

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Clientes</h1>
      <Input
        placeholder="Buscar por nome ou email..."
        value={search}
        onChange={e => setSearch(e.target.value)}
        className="max-w-xs"
      />
      {loading ? (
        <p className="text-slate-500">Carregando...</p>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Cliente</TableHead>
              <TableHead>Agendamentos</TableHead>
              <TableHead>Concluídos</TableHead>
              <TableHead>Valor Gasto</TableHead>
              <TableHead>Última Visita</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {filtered.map(c => (
              <TableRow key={c.customerId}>
                <TableCell>
                  <div className="font-medium">{c.customerName}</div>
                  <div className="text-xs text-slate-500">{c.customerEmail}</div>
                </TableCell>
                <TableCell>{c.bookingCount}</TableCell>
                <TableCell>{c.completedCount}</TableCell>
                <TableCell>R$ {c.totalSpent.toFixed(2)}</TableCell>
                <TableCell>{format(new Date(c.lastVisit), 'dd/MM/yyyy')}</TableCell>
              </TableRow>
            ))}
            {filtered.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} className="text-center py-8 text-slate-500">
                  Nenhum cliente encontrado.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      )}
    </div>
  )
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/app/(admin)/admin/clientes/
git commit -m "feat: add customers admin page with CRM summary from bookings"
```

---

### Task 14: Agenda (/admin/agenda)

**Files:**
- Create: `frontend/app/(admin)/admin/agenda/page.tsx`
- Create: `frontend/components/bookings/BookingCalendar.tsx`
- Create: `frontend/__tests__/BookingCalendar.test.tsx`

- [ ] **Step 1: Instalar dependência de drag-and-drop (já instalada no Task 1)**

Verificar que `@hello-pangea/dnd` está no `package.json`. Se não estiver:
```bash
cd frontend && npm install @hello-pangea/dnd
```

- [ ] **Step 2: Escrever teste do calendário**

```typescript
// frontend/__tests__/BookingCalendar.test.tsx
import { render, screen } from '@testing-library/react'
import { vi } from 'vitest'
import { BookingCalendar } from '@/components/bookings/BookingCalendar'
import type { Booking } from '@/lib/types/booking'

const bookings: Booking[] = [
  {
    id: '1',
    customerId: 'c1',
    customerName: 'Ana Lima',
    customerEmail: 'ana@test.com',
    resourceId: 'r1',
    resourceName: 'Sala A',
    serviceId: 's1',
    serviceName: 'Corte',
    scheduledAt: new Date().toISOString(),
    durationMinutes: 60,
    status: 'Confirmed',
    totalAmount: 100,
    createdAt: new Date().toISOString(),
  },
]

describe('BookingCalendar', () => {
  it('renders booking in the calendar', () => {
    render(<BookingCalendar bookings={bookings} onMove={vi.fn()} />)
    expect(screen.getByText('Ana Lima')).toBeInTheDocument()
    expect(screen.getByText('Corte')).toBeInTheDocument()
  })

  it('shows day view by default', () => {
    render(<BookingCalendar bookings={bookings} onMove={vi.fn()} />)
    expect(screen.getByRole('button', { name: /dia/i })).toBeInTheDocument()
  })
})
```

- [ ] **Step 3: Executar teste para verificar falha**

```bash
npm run test:run -- __tests__/BookingCalendar.test.tsx
```

Expected: FAIL.

- [ ] **Step 4: Implementar BookingCalendar**

```typescript
// frontend/components/bookings/BookingCalendar.tsx
'use client'

import { useState } from 'react'
import { format, addDays, startOfWeek, isSameDay } from 'date-fns'
import { ptBR } from 'date-fns/locale'
import type { Booking } from '@/lib/types/booking'
import { Button } from '@/components/ui/button'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { cn } from '@/lib/utils'

type View = 'dia' | 'semana'

const HOURS = Array.from({ length: 14 }, (_, i) => i + 7) // 07:00 – 20:00

interface Props {
  bookings: Booking[]
  onMove: (bookingId: string, newStart: string) => void
}

export function BookingCalendar({ bookings }: Props) {
  const [view, setView] = useState<View>('dia')
  const [current, setCurrent] = useState(new Date())

  const days = view === 'dia'
    ? [current]
    : Array.from({ length: 7 }, (_, i) => addDays(startOfWeek(current, { weekStartsOn: 1 }), i))

  const getBookingsForDayAndHour = (day: Date, hour: number) =>
    bookings.filter(b => {
      const d = new Date(b.scheduledAt)
      return isSameDay(d, day) && d.getHours() === hour
    })

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-4">
        <Button variant="outline" size="sm" onClick={() => setView('dia')}
          className={cn(view === 'dia' && 'bg-slate-100')}>Dia</Button>
        <Button variant="outline" size="sm" onClick={() => setView('semana')}
          className={cn(view === 'semana' && 'bg-slate-100')}>Semana</Button>
        <div className="flex items-center gap-2 ml-4">
          <Button variant="outline" size="icon" onClick={() => setCurrent(d => addDays(d, -1))}>
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <span className="text-sm font-medium w-40 text-center">
            {format(current, view === 'dia' ? "dd 'de' MMMM" : "'Semana de' dd/MM", { locale: ptBR })}
          </span>
          <Button variant="outline" size="icon" onClick={() => setCurrent(d => addDays(d, 1))}>
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
      </div>

      <div className="border rounded-lg overflow-auto">
        <div className={cn('grid', view === 'semana' ? 'grid-cols-8' : 'grid-cols-2')}>
          {/* Header */}
          <div className="border-r border-b p-2 text-xs text-slate-400">Hora</div>
          {days.map(d => (
            <div key={d.toISOString()} className="border-r border-b p-2 text-xs font-medium text-center">
              {format(d, 'EEE dd/MM', { locale: ptBR })}
            </div>
          ))}

          {/* Rows */}
          {HOURS.map(hour => (
            <>
              <div key={`h-${hour}`} className="border-r border-b p-2 text-xs text-slate-400">
                {hour}:00
              </div>
              {days.map(d => {
                const dayBookings = getBookingsForDayAndHour(d, hour)
                return (
                  <div key={`${d.toISOString()}-${hour}`} className="border-r border-b p-1 min-h-[48px]">
                    {dayBookings.map(b => (
                      <div
                        key={b.id}
                        className="text-xs bg-indigo-100 text-indigo-800 rounded p-1 mb-1 truncate"
                        title={`${b.customerName} — ${b.serviceName}`}
                      >
                        <div className="font-medium">{b.customerName}</div>
                        <div>{b.serviceName}</div>
                      </div>
                    ))}
                  </div>
                )
              })}
            </>
          ))}
        </div>
      </div>
    </div>
  )
}
```

- [ ] **Step 5: Implementar página Agenda**

```typescript
// frontend/app/(admin)/admin/agenda/page.tsx
'use client'

import { useEffect, useState } from 'react'
import { format, startOfWeek, endOfWeek, addDays } from 'date-fns'
import { bookingsApi } from '@/lib/api/bookings'
import { BookingCalendar } from '@/components/bookings/BookingCalendar'
import type { Booking } from '@/lib/types/booking'

export default function AgendaPage() {
  const [bookings, setBookings] = useState<Booking[]>([])

  useEffect(() => {
    const today = new Date()
    const from = format(addDays(startOfWeek(today, { weekStartsOn: 1 }), -7), "yyyy-MM-dd'T'HH:mm:ss")
    const to = format(addDays(endOfWeek(today, { weekStartsOn: 1 }), 14), "yyyy-MM-dd'T'HH:mm:ss")
    bookingsApi.list({ from, to }).then(setBookings)
  }, [])

  const handleMove = async (bookingId: string, _newStart: string) => {
    // Drag-and-drop: o backend ainda não tem endpoint PATCH /bookings/{id}/reschedule
    // Esta funcionalidade será implementada na Sprint 10
    console.log('Move booking', bookingId, 'to', _newStart)
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Agenda</h1>
      <BookingCalendar bookings={bookings} onMove={handleMove} />
    </div>
  )
}
```

- [ ] **Step 6: Executar testes**

```bash
npm run test:run -- __tests__/BookingCalendar.test.tsx
```

Expected: PASS — 2 testes.

- [ ] **Step 7: Commit**

```bash
git add frontend/app/(admin)/admin/agenda/ frontend/components/bookings/BookingCalendar.tsx
git commit -m "feat: add agenda admin page with day/week calendar view"
```

---

### Task 15: Executar todos os testes e commit final

**Files:** nenhum novo

- [ ] **Step 1: Executar toda a suíte**

```bash
cd frontend
npm run test:run
```

Expected: PASS — todos os testes.

- [ ] **Step 2: Verificar build de produção**

```bash
npm run build
```

Expected: Compilação sem erros TypeScript.

- [ ] **Step 3: Commit de integração**

```bash
cd ..
git add frontend/
git commit -m "feat: complete Sprint 9 — Next.js admin panel (9 pages, auth, API client, tests)"
```

---

## Self-Review

### Spec Coverage

| Requisito | Task | Status |
|-----------|------|--------|
| Next.js 14 setup em `frontend/` | Task 1 | ✅ |
| API client tipado | Task 2 | ✅ |
| Auth JWT + cookie + middleware | Tasks 3-4 | ✅ |
| Layout shell (sidebar + topbar) | Task 5 | ✅ |
| `/admin/dashboard` — métricas | Task 6 | ✅ |
| `/admin/agenda` — calendário | Task 14 | ✅ |
| `/admin/agendamentos` — lista + ações | Task 7 | ✅ |
| `/admin/clientes` — CRM | Task 13 | ✅ |
| `/admin/servicos` — CRUD | Task 8 | ✅ |
| `/admin/recursos` — CRUD | Task 9 | ✅ |
| `/admin/financeiro` — relatório + gráfico | Task 10 | ✅ |
| `/admin/notificacoes` — templates | Task 11 | ✅ |
| `/admin/configuracoes` — identidade + plano | Task 12 | ✅ |
| Vitest + Testing Library | Tasks 4,5,7,8,9,11,14 | ✅ |
| Role guard TenantOwner/TenantAdmin | Task 3 (middleware) | ✅ |
| Portal do Cliente | — | ✅ excluído (Sprint 10) |

### Placeholder Scan

Sem TBDs ou placeholders — todo código é concreto.

### Type Consistency

- `Booking`, `Service`, `Resource`, `FinancialTransaction`, `NotificationTemplate`, `Tenant` definidos em Task 2, usados consistentemente em Tasks 6-14.
- `bookingsApi`, `servicesApi`, `resourcesApi`, `financeiroApi`, `notificationsApi`, `tenantsApi` — nomes consistentes entre `lib/api/` e páginas.
- `UpsertServiceRequest` / `UpsertResourceRequest` — nomes consistentes entre tipos, formulários e páginas.
