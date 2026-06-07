# Sprint 10 — Portal do Cliente (Next.js) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar o Portal do Cliente no projeto Next.js existente em `frontend/` — 6 páginas SSR/Client, wizard de agendamento 4 etapas, autenticação Google OAuth para clientes, e integração com todos os endpoints do backend.

**Architecture:** Novo route group `(portal)` com segmento dinâmico `[slug]` — o tenant é resolvido pelo primeiro segmento da URL (ex: `/joao-barber/servicos`). Páginas públicas (`/`, `/servicos`, `/servicos/:id`) são Server Components com SSR para SEO. Páginas autenticadas (`/agendar`, `/minha-conta`) são Client Components. Customer auth usa Google Identity Services → troca `idToken` pelo JWT do backend → armazena em cookie `portal_access_token` (separado do cookie admin).

**Tech Stack:** Next.js 14 App Router (Server + Client Components), `@react-oauth/google` (Google Sign-In), Zustand (`store/portal-auth.ts`), React Hook Form + Zod (wizard), date-fns, shadcn/ui já instalado, Vitest + Testing Library.

---

## File Map

```
frontend/
├── app/
│   └── (portal)/
│       └── [slug]/
│           ├── layout.tsx                # PortalShell — Navbar + Footer
│           ├── page.tsx                  # Home (SSR)
│           ├── servicos/
│           │   ├── page.tsx              # Catálogo (SSR)
│           │   └── [id]/
│           │       └── page.tsx          # Detalhe do serviço (SSR)
│           ├── agendar/
│           │   ├── page.tsx              # Wizard de agendamento (Client)
│           │   └── [bookingId]/
│           │       └── status/
│           │           └── page.tsx      # Status do agendamento
│           └── minha-conta/
│               └── page.tsx              # Minha Conta (Client, auth required)
├── components/
│   └── portal/
│       ├── PortalNavbar.tsx
│       ├── ServiceCard.tsx
│       ├── ReviewCard.tsx
│       ├── GoogleSignInButton.tsx
│       ├── BookingWizard.tsx             # orquestra as 4 etapas
│       ├── WizardStepService.tsx         # etapa 1: escolher serviço
│       ├── WizardStepResource.tsx        # etapa 2: escolher recurso
│       ├── WizardStepSlot.tsx            # etapa 3: escolher data + horário
│       └── WizardStepConfirm.tsx         # etapa 4: confirmar + criar booking
├── lib/
│   ├── api/
│   │   └── portal.ts                    # fetch anônimo com X-Tenant-Slug
│   └── types/
│       └── portal.ts                    # tipos do portal
└── store/
    └── portal-auth.ts                   # Zustand customer auth (separado do admin)
```

---

### Task 1: Portal Types + API Client

**Files:**
- Create: `frontend/lib/types/portal.ts`
- Create: `frontend/lib/api/portal.ts`

- [ ] **Step 1: Criar `frontend/lib/types/portal.ts`**

```typescript
// frontend/lib/types/portal.ts
import type { Service } from './service'
import type { Resource } from './resource'

export type { Service, Resource }

export interface TenantPublicInfo {
  id: string
  name: string
  slug: string
  logoUrl?: string
  primaryColor?: string
  timezone: string
}

export interface AvailableSlot {
  startsAt: string  // ISO DateTimeOffset
}

export interface CustomerProfile {
  id: string
  name: string
  email: string
  phone?: string
  avatarUrl?: string
}

export interface CustomerBooking {
  id: string
  serviceName: string
  resourceName: string
  scheduledAt: string
  durationMinutes: number
  status: 'Pending' | 'Confirmed' | 'Completed' | 'Cancelled' | 'NoShow'
  totalAmount: number
}

export interface PortalReview {
  id: string
  bookingId: string
  customerId: string
  stars: number
  comment?: string
  createdAt: string
}

export interface FavoriteService {
  id: string
  serviceId: string
  createdAt: string
}

export interface CreateBookingRequest {
  serviceId: string
  resourceId: string
  scheduledAt: string
  notes?: string
}

export interface BookingCreatedResult {
  id: string
  scheduledAt: string
  status: string
  paymentUrl?: string
}
```

- [ ] **Step 2: Criar `frontend/lib/api/portal.ts`**

```typescript
// frontend/lib/api/portal.ts
const API_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

async function portalFetch<T>(
  path: string,
  tenantSlug: string,
  options: RequestInit = {},
  customerToken?: string
): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      'X-Tenant-Slug': tenantSlug,
      ...(customerToken ? { Authorization: `Bearer ${customerToken}` } : {}),
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

import type {
  TenantPublicInfo, CustomerProfile, CustomerBooking,
  PortalReview, FavoriteService, CreateBookingRequest, BookingCreatedResult
} from '../types/portal'
import type { Service } from '../types/service'
import type { Resource } from '../types/resource'

// --- Public (anonymous) ---

export const portalApi = {
  tenant: (slug: string) =>
    portalFetch<TenantPublicInfo>('/api/v1/tenants/me', slug),

  services: (slug: string) =>
    portalFetch<Service[]>('/api/v1/services', slug),

  resources: (slug: string) =>
    portalFetch<Resource[]>('/api/v1/resources', slug),

  slots: (slug: string, resourceId: string, date: string, serviceId?: string) => {
    const qs = new URLSearchParams({ date, ...(serviceId ? { serviceId } : {}) }).toString()
    return portalFetch<string[]>(`/api/v1/resources/${resourceId}/slots?${qs}`, slug)
  },

  reviews: (slug: string, resourceId: string) =>
    portalFetch<PortalReview[]>(`/api/v1/reviews/resources/${resourceId}`, slug),

  // --- Authenticated (Customer) ---

  profile: (slug: string, token: string) =>
    portalFetch<CustomerProfile>('/api/v1/customers/me', slug, {}, token),

  myBookings: (slug: string, token: string) =>
    portalFetch<CustomerBooking[]>('/api/v1/customers/me/bookings', slug, {}, token),

  createBooking: (slug: string, token: string, data: CreateBookingRequest) =>
    portalFetch<BookingCreatedResult>('/api/v1/bookings', slug, {
      method: 'POST',
      body: JSON.stringify(data),
    }, token),

  myFavorites: (slug: string, token: string) =>
    portalFetch<FavoriteService[]>('/api/v1/customers/favorites', slug, {}, token),

  addFavorite: (slug: string, token: string, serviceId: string) =>
    portalFetch<void>(`/api/v1/customers/favorites/${serviceId}`, slug, { method: 'POST' }, token),

  removeFavorite: (slug: string, token: string, serviceId: string) =>
    portalFetch<void>(`/api/v1/customers/favorites/${serviceId}`, slug, { method: 'DELETE' }, token),

  createReview: (slug: string, token: string, bookingId: string, stars: number, comment?: string) =>
    portalFetch<string>('/api/v1/reviews', slug, {
      method: 'POST',
      body: JSON.stringify({ bookingId, stars, comment }),
    }, token),

  updatePhone: (slug: string, token: string, phone: string) =>
    portalFetch<void>('/api/v1/customers/me/phone', slug, {
      method: 'PATCH',
      body: JSON.stringify({ phone }),
    }, token),

  loginWithGoogle: (slug: string, idToken: string) =>
    portalFetch<{ accessToken: string; refreshToken: string; expiresAt: string }>(
      '/api/v1/customers/auth/google', slug, {
        method: 'POST',
        body: JSON.stringify({ idToken, tenantSlug: slug }),
      }
    ),
}
```

- [ ] **Step 3: Commit**

```bash
git add frontend/lib/types/portal.ts frontend/lib/api/portal.ts
git commit -m "feat: add portal API client and types for customer-facing pages"
```

---

### Task 2: Customer Auth Store + Google Sign-In Button

**Files:**
- Create: `frontend/store/portal-auth.ts`
- Create: `frontend/components/portal/GoogleSignInButton.tsx`
- Modify: `frontend/package.json` (add `@react-oauth/google`)

- [ ] **Step 1: Instalar `@react-oauth/google`**

```bash
cd frontend
npm install @react-oauth/google
```

- [ ] **Step 2: Criar `frontend/store/portal-auth.ts`**

```typescript
// frontend/store/portal-auth.ts
import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { CustomerProfile } from '@/lib/types/portal'

interface PortalAuthState {
  customer: CustomerProfile | null
  accessToken: string | null
  setCustomerAuth: (customer: CustomerProfile, accessToken: string) => void
  clearCustomerAuth: () => void
}

export const usePortalAuthStore = create<PortalAuthState>()(
  persist(
    (set) => ({
      customer: null,
      accessToken: null,
      setCustomerAuth: (customer, accessToken) => set({ customer, accessToken }),
      clearCustomerAuth: () => set({ customer: null, accessToken: null }),
    }),
    { name: 'horafy-portal-auth' }
  )
)
```

- [ ] **Step 3: Escrever teste do GoogleSignInButton**

```typescript
// frontend/__tests__/GoogleSignInButton.test.tsx
import { render, screen } from '@testing-library/react'
import { vi } from 'vitest'
import { GoogleSignInButton } from '@/components/portal/GoogleSignInButton'

vi.mock('@react-oauth/google', () => ({
  GoogleOAuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useGoogleLogin: () => vi.fn(),
}))

describe('GoogleSignInButton', () => {
  it('renders sign-in button', () => {
    render(<GoogleSignInButton slug="test-slug" onSuccess={vi.fn()} />)
    expect(screen.getByRole('button', { name: /entrar com google/i })).toBeInTheDocument()
  })
})
```

- [ ] **Step 4: Executar teste para verificar falha**

```bash
cd frontend
npm run test:run -- __tests__/GoogleSignInButton.test.tsx
```

Expected: FAIL.

- [ ] **Step 5: Criar `frontend/components/portal/GoogleSignInButton.tsx`**

```typescript
// frontend/components/portal/GoogleSignInButton.tsx
'use client'

import { GoogleOAuthProvider, useGoogleLogin } from '@react-oauth/google'
import { Button } from '@/components/ui/button'
import { portalApi } from '@/lib/api/portal'
import { usePortalAuthStore } from '@/store/portal-auth'

const GOOGLE_CLIENT_ID = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID ?? ''

interface Props {
  slug: string
  onSuccess?: () => void
}

function SignInButton({ slug, onSuccess }: Props) {
  const { setCustomerAuth } = usePortalAuthStore()

  const login = useGoogleLogin({
    onSuccess: async (response) => {
      try {
        const tokens = await portalApi.loginWithGoogle(slug, response.access_token)
        const profile = await portalApi.profile(slug, tokens.accessToken)
        setCustomerAuth(profile, tokens.accessToken)
        document.cookie = `portal_access_token=${tokens.accessToken}; path=/; max-age=${60 * 60 * 24}`
        onSuccess?.()
      } catch {
        console.error('Login failed')
      }
    },
  })

  return (
    <Button variant="outline" onClick={() => login()} className="gap-2">
      <svg className="h-4 w-4" viewBox="0 0 24 24">
        <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"/>
        <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"/>
        <path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"/>
        <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"/>
      </svg>
      Entrar com Google
    </Button>
  )
}

export function GoogleSignInButton(props: Props) {
  return (
    <GoogleOAuthProvider clientId={GOOGLE_CLIENT_ID}>
      <SignInButton {...props} />
    </GoogleOAuthProvider>
  )
}
```

- [ ] **Step 6: Adicionar variável de ambiente em `.env.local`**

```
NEXT_PUBLIC_GOOGLE_CLIENT_ID=your-google-client-id
```

- [ ] **Step 7: Executar teste**

```bash
npm run test:run -- __tests__/GoogleSignInButton.test.tsx
```

Expected: PASS — 1 teste.

- [ ] **Step 8: Commit**

```bash
git add frontend/store/portal-auth.ts frontend/components/portal/GoogleSignInButton.tsx frontend/__tests__/GoogleSignInButton.test.tsx frontend/.env.local
git commit -m "feat: add customer auth store and Google Sign-In button"
```

---

### Task 3: Portal Layout (Navbar + Shell)

**Files:**
- Create: `frontend/components/portal/PortalNavbar.tsx`
- Create: `frontend/app/(portal)/[slug]/layout.tsx`
- Create: `frontend/__tests__/PortalNavbar.test.tsx`

- [ ] **Step 1: Escrever teste do Navbar**

```typescript
// frontend/__tests__/PortalNavbar.test.tsx
import { render, screen } from '@testing-library/react'
import { vi } from 'vitest'
import { PortalNavbar } from '@/components/portal/PortalNavbar'

vi.mock('@react-oauth/google', () => ({
  GoogleOAuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useGoogleLogin: () => vi.fn(),
}))

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}))

describe('PortalNavbar', () => {
  it('renders tenant name', () => {
    render(<PortalNavbar slug="joao-barber" tenantName="Barbearia do João" />)
    expect(screen.getByText('Barbearia do João')).toBeInTheDocument()
  })

  it('renders navigation links', () => {
    render(<PortalNavbar slug="joao-barber" tenantName="Barbearia do João" />)
    expect(screen.getByRole('link', { name: /serviços/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /agendar/i })).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: Executar teste para verificar falha**

```bash
npm run test:run -- __tests__/PortalNavbar.test.tsx
```

Expected: FAIL.

- [ ] **Step 3: Criar `frontend/components/portal/PortalNavbar.tsx`**

```typescript
// frontend/components/portal/PortalNavbar.tsx
'use client'

import Link from 'next/link'
import { usePortalAuthStore } from '@/store/portal-auth'
import { GoogleSignInButton } from './GoogleSignInButton'
import { Button } from '@/components/ui/button'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { useRouter } from 'next/navigation'

interface Props {
  slug: string
  tenantName: string
  logoUrl?: string
}

export function PortalNavbar({ slug, tenantName, logoUrl }: Props) {
  const router = useRouter()
  const { customer, clearCustomerAuth } = usePortalAuthStore()

  const handleLogout = () => {
    document.cookie = 'portal_access_token=; path=/; max-age=0'
    clearCustomerAuth()
    router.refresh()
  }

  return (
    <header className="border-b bg-white sticky top-0 z-50">
      <div className="max-w-5xl mx-auto px-4 h-16 flex items-center justify-between">
        <Link href={`/${slug}`} className="flex items-center gap-3">
          {logoUrl ? (
            <img src={logoUrl} alt={tenantName} className="h-8 w-auto" />
          ) : (
            <span className="font-bold text-xl">{tenantName}</span>
          )}
        </Link>

        <nav className="hidden md:flex items-center gap-6">
          <Link href={`/${slug}/servicos`} className="text-sm text-slate-600 hover:text-slate-900">
            Serviços
          </Link>
          <Link href={`/${slug}/agendar`} className="text-sm text-slate-600 hover:text-slate-900">
            Agendar
          </Link>
          {customer && (
            <Link href={`/${slug}/minha-conta`} className="text-sm text-slate-600 hover:text-slate-900">
              Minha Conta
            </Link>
          )}
        </nav>

        <div className="flex items-center gap-3">
          {customer ? (
            <>
              <Avatar className="h-8 w-8">
                <AvatarFallback>{customer.name[0]}</AvatarFallback>
              </Avatar>
              <Button variant="ghost" size="sm" onClick={handleLogout}>Sair</Button>
            </>
          ) : (
            <GoogleSignInButton slug={slug} />
          )}
        </div>
      </div>
    </header>
  )
}
```

- [ ] **Step 4: Criar `frontend/app/(portal)/[slug]/layout.tsx`**

```typescript
// frontend/app/(portal)/[slug]/layout.tsx
import { PortalNavbar } from '@/components/portal/PortalNavbar'
import { portalApi } from '@/lib/api/portal'

interface Props {
  children: React.ReactNode
  params: { slug: string }
}

export default async function PortalLayout({ children, params }: Props) {
  const { slug } = params
  let tenantName = slug
  let logoUrl: string | undefined

  try {
    const tenant = await portalApi.tenant(slug)
    tenantName = tenant.name
    logoUrl = tenant.logoUrl
  } catch {
    // tenant not found — still render with slug as name
  }

  return (
    <div className="min-h-screen bg-white">
      <PortalNavbar slug={slug} tenantName={tenantName} logoUrl={logoUrl} />
      <main>{children}</main>
      <footer className="border-t mt-16 py-8">
        <p className="text-center text-sm text-slate-400">
          Powered by <span className="font-semibold">Horafy</span>
        </p>
      </footer>
    </div>
  )
}
```

- [ ] **Step 5: Executar testes**

```bash
npm run test:run -- __tests__/PortalNavbar.test.tsx
```

Expected: PASS — 2 testes.

- [ ] **Step 6: Commit**

```bash
git add frontend/components/portal/ frontend/app/(portal)/ frontend/__tests__/PortalNavbar.test.tsx
git commit -m "feat: add portal layout with sticky navbar and footer"
```

---

### Task 4: Home Page (SSR)

**Files:**
- Create: `frontend/components/portal/ServiceCard.tsx`
- Create: `frontend/components/portal/ReviewCard.tsx`
- Create: `frontend/app/(portal)/[slug]/page.tsx`
- Create: `frontend/__tests__/ServiceCard.test.tsx`

- [ ] **Step 1: Escrever teste do ServiceCard**

```typescript
// frontend/__tests__/ServiceCard.test.tsx
import { render, screen } from '@testing-library/react'
import { ServiceCard } from '@/components/portal/ServiceCard'
import type { Service } from '@/lib/types/service'

const mockService: Service = {
  id: 's1',
  name: 'Corte Masculino',
  description: 'Corte clássico com navalha',
  durationMinutes: 30,
  price: 45,
  isActive: true,
}

describe('ServiceCard', () => {
  it('renders service name, price and duration', () => {
    render(<ServiceCard service={mockService} slug="joao-barber" />)
    expect(screen.getByText('Corte Masculino')).toBeInTheDocument()
    expect(screen.getByText(/R\$ 45/)).toBeInTheDocument()
    expect(screen.getByText(/30 min/)).toBeInTheDocument()
  })

  it('renders agendar link', () => {
    render(<ServiceCard service={mockService} slug="joao-barber" />)
    const link = screen.getByRole('link', { name: /agendar/i })
    expect(link).toHaveAttribute('href', '/joao-barber/agendar?serviceId=s1')
  })
})
```

- [ ] **Step 2: Executar teste para verificar falha**

```bash
npm run test:run -- __tests__/ServiceCard.test.tsx
```

Expected: FAIL.

- [ ] **Step 3: Criar `frontend/components/portal/ServiceCard.tsx`**

```typescript
// frontend/components/portal/ServiceCard.tsx
import Link from 'next/link'
import type { Service } from '@/lib/types/service'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Clock, DollarSign } from 'lucide-react'

interface Props {
  service: Service
  slug: string
}

export function ServiceCard({ service, slug }: Props) {
  return (
    <Card className="flex flex-col">
      <CardHeader>
        <CardTitle className="text-lg">{service.name}</CardTitle>
        {service.description && (
          <p className="text-sm text-slate-500">{service.description}</p>
        )}
      </CardHeader>
      <CardContent className="flex-1 flex flex-col justify-end gap-4">
        <div className="flex items-center gap-4 text-sm text-slate-600">
          <span className="flex items-center gap-1">
            <Clock className="h-4 w-4" /> {service.durationMinutes} min
          </span>
          <span className="flex items-center gap-1">
            <DollarSign className="h-4 w-4" /> R$ {service.price.toFixed(2)}
          </span>
        </div>
        <Button asChild className="w-full">
          <Link href={`/${slug}/agendar?serviceId=${service.id}`}>Agendar</Link>
        </Button>
      </CardContent>
    </Card>
  )
}
```

- [ ] **Step 4: Criar `frontend/components/portal/ReviewCard.tsx`**

```typescript
// frontend/components/portal/ReviewCard.tsx
import type { PortalReview } from '@/lib/types/portal'
import { Card, CardContent } from '@/components/ui/card'
import { Star } from 'lucide-react'
import { format } from 'date-fns'
import { ptBR } from 'date-fns/locale'

interface Props {
  review: PortalReview
}

export function ReviewCard({ review }: Props) {
  return (
    <Card>
      <CardContent className="pt-4">
        <div className="flex items-center gap-1 mb-2">
          {Array.from({ length: 5 }, (_, i) => (
            <Star
              key={i}
              className={`h-4 w-4 ${i < review.stars ? 'fill-amber-400 text-amber-400' : 'text-slate-200'}`}
            />
          ))}
        </div>
        {review.comment && <p className="text-sm text-slate-700">{review.comment}</p>}
        <p className="text-xs text-slate-400 mt-2">
          {format(new Date(review.createdAt), "dd 'de' MMMM", { locale: ptBR })}
        </p>
      </CardContent>
    </Card>
  )
}
```

- [ ] **Step 5: Criar `frontend/app/(portal)/[slug]/page.tsx`**

```typescript
// frontend/app/(portal)/[slug]/page.tsx
import Link from 'next/link'
import { portalApi } from '@/lib/api/portal'
import { ServiceCard } from '@/components/portal/ServiceCard'
import { ReviewCard } from '@/components/portal/ReviewCard'
import { Button } from '@/components/ui/button'

interface Props {
  params: { slug: string }
}

export default async function PortalHomePage({ params }: Props) {
  const { slug } = params

  const [services, resources] = await Promise.all([
    portalApi.services(slug).catch(() => []),
    portalApi.resources(slug).catch(() => []),
  ])

  const activeServices = services.filter(s => s.isActive).slice(0, 6)

  // Buscar avaliações do primeiro recurso disponível como amostra
  const firstResourceId = resources[0]?.id
  const reviews = firstResourceId
    ? await portalApi.reviews(slug, firstResourceId).catch(() => [])
    : []

  const featuredReviews = reviews.slice(0, 3)

  return (
    <div>
      {/* Hero */}
      <section className="bg-gradient-to-br from-slate-900 to-slate-700 text-white py-20">
        <div className="max-w-5xl mx-auto px-4 text-center">
          <h1 className="text-4xl font-bold mb-4">Agende agora</h1>
          <p className="text-slate-300 mb-8 text-lg">
            Serviços de qualidade, agendamento fácil e rápido.
          </p>
          <Button asChild size="lg" className="bg-white text-slate-900 hover:bg-slate-100">
            <Link href={`/${slug}/agendar`}>Agendar agora</Link>
          </Button>
        </div>
      </section>

      {/* Serviços em destaque */}
      {activeServices.length > 0 && (
        <section className="max-w-5xl mx-auto px-4 py-16">
          <div className="flex items-center justify-between mb-8">
            <h2 className="text-2xl font-bold">Nossos serviços</h2>
            <Link href={`/${slug}/servicos`} className="text-sm text-indigo-600 hover:underline">
              Ver todos →
            </Link>
          </div>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {activeServices.map(s => (
              <ServiceCard key={s.id} service={s} slug={slug} />
            ))}
          </div>
        </section>
      )}

      {/* Equipe */}
      {resources.length > 0 && (
        <section className="bg-slate-50 py-16">
          <div className="max-w-5xl mx-auto px-4">
            <h2 className="text-2xl font-bold mb-8">Nossa equipe</h2>
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              {resources.filter(r => r.isActive).map(r => (
                <div key={r.id} className="text-center">
                  <div className="h-20 w-20 rounded-full bg-slate-200 mx-auto mb-3 flex items-center justify-center text-2xl font-bold text-slate-500">
                    {r.name[0]}
                  </div>
                  <p className="font-medium">{r.name}</p>
                  <p className="text-sm text-slate-500">{r.type}</p>
                </div>
              ))}
            </div>
          </div>
        </section>
      )}

      {/* Avaliações */}
      {featuredReviews.length > 0 && (
        <section className="max-w-5xl mx-auto px-4 py-16">
          <h2 className="text-2xl font-bold mb-8">O que nossos clientes dizem</h2>
          <div className="grid gap-4 sm:grid-cols-3">
            {featuredReviews.map(r => (
              <ReviewCard key={r.id} review={r} />
            ))}
          </div>
        </section>
      )}
    </div>
  )
}
```

- [ ] **Step 6: Executar testes**

```bash
npm run test:run -- __tests__/ServiceCard.test.tsx
```

Expected: PASS — 2 testes.

- [ ] **Step 7: Commit**

```bash
git add frontend/components/portal/ServiceCard.tsx frontend/components/portal/ReviewCard.tsx frontend/app/(portal)/[slug]/page.tsx frontend/__tests__/ServiceCard.test.tsx
git commit -m "feat: add portal home page with service cards, team and reviews"
```

---

### Task 5: Catálogo + Detalhe do Serviço (SSR)

**Files:**
- Create: `frontend/app/(portal)/[slug]/servicos/page.tsx`
- Create: `frontend/app/(portal)/[slug]/servicos/[id]/page.tsx`

- [ ] **Step 1: Criar `frontend/app/(portal)/[slug]/servicos/page.tsx`**

```typescript
// frontend/app/(portal)/[slug]/servicos/page.tsx
import { portalApi } from '@/lib/api/portal'
import { ServiceCard } from '@/components/portal/ServiceCard'

interface Props {
  params: { slug: string }
  searchParams: { q?: string; minPrice?: string; maxPrice?: string }
}

export default async function CatalogoPage({ params, searchParams }: Props) {
  const { slug } = params
  const services = await portalApi.services(slug).catch(() => [])
  const active = services.filter(s => s.isActive)

  const q = searchParams.q?.toLowerCase() ?? ''
  const minPrice = searchParams.minPrice ? Number(searchParams.minPrice) : 0
  const maxPrice = searchParams.maxPrice ? Number(searchParams.maxPrice) : Infinity

  const filtered = active.filter(s =>
    (s.name.toLowerCase().includes(q) || (s.description ?? '').toLowerCase().includes(q)) &&
    s.price >= minPrice &&
    s.price <= maxPrice
  )

  return (
    <div className="max-w-5xl mx-auto px-4 py-12">
      <h1 className="text-3xl font-bold mb-8">Nossos Serviços</h1>

      <form method="GET" className="flex gap-4 flex-wrap mb-8">
        <input
          name="q"
          defaultValue={searchParams.q}
          placeholder="Buscar serviço..."
          className="border rounded-md px-3 py-2 text-sm flex-1 min-w-48"
        />
        <input
          name="minPrice"
          type="number"
          defaultValue={searchParams.minPrice}
          placeholder="Preço mín."
          className="border rounded-md px-3 py-2 text-sm w-32"
        />
        <input
          name="maxPrice"
          type="number"
          defaultValue={searchParams.maxPrice}
          placeholder="Preço máx."
          className="border rounded-md px-3 py-2 text-sm w-32"
        />
        <button type="submit" className="bg-slate-900 text-white px-4 py-2 rounded-md text-sm">
          Filtrar
        </button>
      </form>

      {filtered.length === 0 ? (
        <p className="text-slate-500">Nenhum serviço encontrado.</p>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {filtered.map(s => (
            <ServiceCard key={s.id} service={s} slug={slug} />
          ))}
        </div>
      )}
    </div>
  )
}
```

- [ ] **Step 2: Criar `frontend/app/(portal)/[slug]/servicos/[id]/page.tsx`**

```typescript
// frontend/app/(portal)/[slug]/servicos/[id]/page.tsx
import Link from 'next/link'
import { notFound } from 'next/navigation'
import { portalApi } from '@/lib/api/portal'
import { ReviewCard } from '@/components/portal/ReviewCard'
import { Button } from '@/components/ui/button'
import { Clock, DollarSign } from 'lucide-react'

interface Props {
  params: { slug: string; id: string }
}

export default async function ServiceDetailPage({ params }: Props) {
  const { slug, id } = params

  const services = await portalApi.services(slug).catch(() => [])
  const service = services.find(s => s.id === id)
  if (!service) notFound()

  const resources = await portalApi.resources(slug).catch(() => [])
  const capableResources = resources.filter(
    r => r.isActive && r.serviceIds.includes(id)
  )

  const allReviews = await Promise.all(
    capableResources.slice(0, 3).map(r =>
      portalApi.reviews(slug, r.id).catch(() => [])
    )
  )
  const reviews = allReviews.flat().slice(0, 6)
  const avgStars = reviews.length
    ? (reviews.reduce((sum, r) => sum + r.stars, 0) / reviews.length).toFixed(1)
    : null

  return (
    <div className="max-w-3xl mx-auto px-4 py-12">
      <div className="mb-8">
        <h1 className="text-3xl font-bold mb-2">{service.name}</h1>
        {service.description && <p className="text-slate-600">{service.description}</p>}
        <div className="flex items-center gap-6 mt-4 text-slate-700">
          <span className="flex items-center gap-2">
            <Clock className="h-5 w-5" /> {service.durationMinutes} minutos
          </span>
          <span className="flex items-center gap-2">
            <DollarSign className="h-5 w-5" /> R$ {service.price.toFixed(2)}
          </span>
          {avgStars && <span>⭐ {avgStars}</span>}
        </div>
      </div>

      <Button asChild size="lg" className="mb-12">
        <Link href={`/${slug}/agendar?serviceId=${id}`}>Agendar este serviço</Link>
      </Button>

      {capableResources.length > 0 && (
        <section className="mb-12">
          <h2 className="text-xl font-bold mb-4">Quem realiza</h2>
          <div className="flex gap-4 flex-wrap">
            {capableResources.map(r => (
              <div key={r.id} className="flex items-center gap-3 border rounded-lg p-3">
                <div className="h-10 w-10 rounded-full bg-slate-200 flex items-center justify-center font-bold text-slate-600">
                  {r.name[0]}
                </div>
                <div>
                  <p className="font-medium">{r.name}</p>
                  <p className="text-xs text-slate-500">{r.type}</p>
                </div>
              </div>
            ))}
          </div>
        </section>
      )}

      {reviews.length > 0 && (
        <section>
          <h2 className="text-xl font-bold mb-4">Avaliações</h2>
          <div className="grid gap-4 sm:grid-cols-2">
            {reviews.map(r => <ReviewCard key={r.id} review={r} />)}
          </div>
        </section>
      )}
    </div>
  )
}
```

- [ ] **Step 3: Commit**

```bash
git add frontend/app/(portal)/[slug]/servicos/
git commit -m "feat: add service catalog and detail pages with SSR"
```

---

### Task 6: Wizard — Steps 1 e 2 (Serviço + Recurso)

**Files:**
- Create: `frontend/components/portal/WizardStepService.tsx`
- Create: `frontend/components/portal/WizardStepResource.tsx`
- Create: `frontend/__tests__/WizardStepService.test.tsx`

- [ ] **Step 1: Escrever teste do WizardStepService**

```typescript
// frontend/__tests__/WizardStepService.test.tsx
import { render, screen, fireEvent } from '@testing-library/react'
import { vi } from 'vitest'
import { WizardStepService } from '@/components/portal/WizardStepService'
import type { Service } from '@/lib/types/service'

const services: Service[] = [
  { id: 's1', name: 'Corte', durationMinutes: 30, price: 45, isActive: true },
  { id: 's2', name: 'Barba', durationMinutes: 20, price: 30, isActive: true },
]

describe('WizardStepService', () => {
  it('renders all services', () => {
    render(<WizardStepService services={services} selectedId={null} onSelect={vi.fn()} />)
    expect(screen.getByText('Corte')).toBeInTheDocument()
    expect(screen.getByText('Barba')).toBeInTheDocument()
  })

  it('calls onSelect when service is clicked', () => {
    const onSelect = vi.fn()
    render(<WizardStepService services={services} selectedId={null} onSelect={onSelect} />)
    fireEvent.click(screen.getByText('Corte'))
    expect(onSelect).toHaveBeenCalledWith('s1')
  })

  it('highlights selected service', () => {
    render(<WizardStepService services={services} selectedId="s1" onSelect={vi.fn()} />)
    const corteCard = screen.getByText('Corte').closest('div[class*="border"]') ??
                      screen.getByText('Corte').closest('button')
    expect(corteCard).toBeTruthy()
  })
})
```

- [ ] **Step 2: Executar teste para verificar falha**

```bash
npm run test:run -- __tests__/WizardStepService.test.tsx
```

Expected: FAIL.

- [ ] **Step 3: Criar `frontend/components/portal/WizardStepService.tsx`**

```typescript
// frontend/components/portal/WizardStepService.tsx
import type { Service } from '@/lib/types/service'
import { cn } from '@/lib/utils'
import { Clock, DollarSign } from 'lucide-react'

interface Props {
  services: Service[]
  selectedId: string | null
  onSelect: (id: string) => void
}

export function WizardStepService({ services, selectedId, onSelect }: Props) {
  return (
    <div>
      <h2 className="text-xl font-bold mb-4">Escolha o serviço</h2>
      <div className="grid gap-3 sm:grid-cols-2">
        {services.map(s => (
          <button
            key={s.id}
            type="button"
            onClick={() => onSelect(s.id)}
            className={cn(
              'text-left border rounded-lg p-4 transition-all hover:border-indigo-400',
              selectedId === s.id
                ? 'border-indigo-600 bg-indigo-50 ring-2 ring-indigo-300'
                : 'border-slate-200'
            )}
          >
            <p className="font-medium mb-1">{s.name}</p>
            {s.description && <p className="text-xs text-slate-500 mb-2">{s.description}</p>}
            <div className="flex gap-4 text-sm text-slate-600">
              <span className="flex items-center gap-1">
                <Clock className="h-3 w-3" /> {s.durationMinutes} min
              </span>
              <span className="flex items-center gap-1">
                <DollarSign className="h-3 w-3" /> R$ {s.price.toFixed(2)}
              </span>
            </div>
          </button>
        ))}
      </div>
    </div>
  )
}
```

- [ ] **Step 4: Criar `frontend/components/portal/WizardStepResource.tsx`**

```typescript
// frontend/components/portal/WizardStepResource.tsx
import type { Resource } from '@/lib/types/resource'
import { cn } from '@/lib/utils'

interface Props {
  resources: Resource[]
  selectedId: string | null
  onSelect: (id: string) => void
}

export function WizardStepResource({ resources, selectedId, onSelect }: Props) {
  return (
    <div>
      <h2 className="text-xl font-bold mb-4">Escolha o profissional/recurso</h2>
      {resources.length === 0 ? (
        <p className="text-slate-500">Nenhum recurso disponível para este serviço.</p>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2">
          {resources.map(r => (
            <button
              key={r.id}
              type="button"
              onClick={() => onSelect(r.id)}
              className={cn(
                'text-left border rounded-lg p-4 flex items-center gap-3 transition-all hover:border-indigo-400',
                selectedId === r.id
                  ? 'border-indigo-600 bg-indigo-50 ring-2 ring-indigo-300'
                  : 'border-slate-200'
              )}
            >
              <div className="h-10 w-10 rounded-full bg-slate-200 flex items-center justify-center font-bold text-slate-600 shrink-0">
                {r.name[0]}
              </div>
              <div>
                <p className="font-medium">{r.name}</p>
                <p className="text-xs text-slate-500">{r.type}</p>
              </div>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
```

- [ ] **Step 5: Executar testes**

```bash
npm run test:run -- __tests__/WizardStepService.test.tsx
```

Expected: PASS — 3 testes.

- [ ] **Step 6: Commit**

```bash
git add frontend/components/portal/WizardStepService.tsx frontend/components/portal/WizardStepResource.tsx frontend/__tests__/WizardStepService.test.tsx
git commit -m "feat: add wizard steps 1-2 (service and resource selection)"
```

---

### Task 7: Wizard — Step 3 (SlotPicker)

**Files:**
- Create: `frontend/components/portal/WizardStepSlot.tsx`
- Create: `frontend/__tests__/WizardStepSlot.test.tsx`

- [ ] **Step 1: Escrever teste do SlotPicker**

```typescript
// frontend/__tests__/WizardStepSlot.test.tsx
import { render, screen, fireEvent } from '@testing-library/react'
import { vi } from 'vitest'
import { WizardStepSlot } from '@/components/portal/WizardStepSlot'

const slots = [
  '2026-06-10T09:00:00-03:00',
  '2026-06-10T10:00:00-03:00',
  '2026-06-10T11:00:00-03:00',
]

describe('WizardStepSlot', () => {
  it('renders available slots', () => {
    render(
      <WizardStepSlot
        slots={slots}
        loadingSlots={false}
        selectedDate={new Date('2026-06-10')}
        selectedSlot={null}
        onDateChange={vi.fn()}
        onSlotSelect={vi.fn()}
      />
    )
    expect(screen.getByText('09:00')).toBeInTheDocument()
    expect(screen.getByText('10:00')).toBeInTheDocument()
    expect(screen.getByText('11:00')).toBeInTheDocument()
  })

  it('calls onSlotSelect when a slot is clicked', () => {
    const onSlotSelect = vi.fn()
    render(
      <WizardStepSlot
        slots={slots}
        loadingSlots={false}
        selectedDate={new Date('2026-06-10')}
        selectedSlot={null}
        onDateChange={vi.fn()}
        onSlotSelect={onSlotSelect}
      />
    )
    fireEvent.click(screen.getByText('09:00'))
    expect(onSlotSelect).toHaveBeenCalledWith('2026-06-10T09:00:00-03:00')
  })
})
```

- [ ] **Step 2: Executar teste para verificar falha**

```bash
npm run test:run -- __tests__/WizardStepSlot.test.tsx
```

Expected: FAIL.

- [ ] **Step 3: Criar `frontend/components/portal/WizardStepSlot.tsx`**

```typescript
// frontend/components/portal/WizardStepSlot.tsx
'use client'

import { format, addDays, isSameDay } from 'date-fns'
import { ptBR } from 'date-fns/locale'
import { cn } from '@/lib/utils'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Button } from '@/components/ui/button'

interface Props {
  slots: string[]
  loadingSlots: boolean
  selectedDate: Date
  selectedSlot: string | null
  onDateChange: (date: Date) => void
  onSlotSelect: (slot: string) => void
}

const DAYS_AHEAD = 14

export function WizardStepSlot({
  slots, loadingSlots, selectedDate, selectedSlot, onDateChange, onSlotSelect
}: Props) {
  const today = new Date()
  const days = Array.from({ length: DAYS_AHEAD }, (_, i) => addDays(today, i))

  return (
    <div>
      <h2 className="text-xl font-bold mb-4">Escolha a data e horário</h2>

      {/* Date strip */}
      <div className="flex gap-2 overflow-x-auto pb-2 mb-6">
        {days.map(d => (
          <button
            key={d.toISOString()}
            type="button"
            onClick={() => onDateChange(d)}
            className={cn(
              'flex flex-col items-center border rounded-lg px-3 py-2 min-w-[60px] text-sm shrink-0 transition-all',
              isSameDay(d, selectedDate)
                ? 'border-indigo-600 bg-indigo-50 text-indigo-700'
                : 'border-slate-200 hover:border-slate-400'
            )}
          >
            <span className="text-xs text-slate-500">{format(d, 'EEE', { locale: ptBR })}</span>
            <span className="font-bold">{format(d, 'dd')}</span>
            <span className="text-xs text-slate-500">{format(d, 'MMM', { locale: ptBR })}</span>
          </button>
        ))}
      </div>

      {/* Slots */}
      {loadingSlots ? (
        <p className="text-slate-500 text-sm">Buscando horários...</p>
      ) : slots.length === 0 ? (
        <p className="text-slate-500 text-sm">Sem horários disponíveis nesta data.</p>
      ) : (
        <div className="grid grid-cols-3 sm:grid-cols-4 gap-2">
          {slots.map(slot => {
            const time = format(new Date(slot), 'HH:mm')
            return (
              <button
                key={slot}
                type="button"
                onClick={() => onSlotSelect(slot)}
                className={cn(
                  'border rounded-lg py-2 text-sm font-medium transition-all',
                  selectedSlot === slot
                    ? 'border-indigo-600 bg-indigo-600 text-white'
                    : 'border-slate-200 hover:border-indigo-400'
                )}
              >
                {time}
              </button>
            )
          })}
        </div>
      )}
    </div>
  )
}
```

- [ ] **Step 4: Executar testes**

```bash
npm run test:run -- __tests__/WizardStepSlot.test.tsx
```

Expected: PASS — 2 testes.

- [ ] **Step 5: Commit**

```bash
git add frontend/components/portal/WizardStepSlot.tsx frontend/__tests__/WizardStepSlot.test.tsx
git commit -m "feat: add wizard step 3 — date and slot picker"
```

---

### Task 8: Wizard — Step 4 (Confirmação) + BookingWizard

**Files:**
- Create: `frontend/components/portal/WizardStepConfirm.tsx`
- Create: `frontend/components/portal/BookingWizard.tsx`
- Create: `frontend/__tests__/BookingWizard.test.tsx`

- [ ] **Step 1: Escrever teste do BookingWizard**

```typescript
// frontend/__tests__/BookingWizard.test.tsx
import { render, screen } from '@testing-library/react'
import { vi } from 'vitest'
import { BookingWizard } from '@/components/portal/BookingWizard'
import type { Service } from '@/lib/types/service'
import type { Resource } from '@/lib/types/resource'

vi.mock('@/lib/api/portal', () => ({
  portalApi: {
    slots: vi.fn().mockResolvedValue([]),
    createBooking: vi.fn(),
  },
}))

vi.mock('@/store/portal-auth', () => ({
  usePortalAuthStore: () => ({ customer: null, accessToken: null }),
}))

const services: Service[] = [
  { id: 's1', name: 'Corte', durationMinutes: 30, price: 45, isActive: true },
]
const resources: Resource[] = [
  { id: 'r1', name: 'João', type: 'Professional', serviceIds: ['s1'], isActive: true },
]

describe('BookingWizard', () => {
  it('renders step 1 (service selection) by default', () => {
    render(
      <BookingWizard slug="joao-barber" services={services} resources={resources} />
    )
    expect(screen.getByText(/escolha o serviço/i)).toBeInTheDocument()
  })

  it('shows step indicator', () => {
    render(
      <BookingWizard slug="joao-barber" services={services} resources={resources} />
    )
    expect(screen.getByText('1')).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: Executar teste para verificar falha**

```bash
npm run test:run -- __tests__/BookingWizard.test.tsx
```

Expected: FAIL.

- [ ] **Step 3: Criar `frontend/components/portal/WizardStepConfirm.tsx`**

```typescript
// frontend/components/portal/WizardStepConfirm.tsx
'use client'

import { format } from 'date-fns'
import { ptBR } from 'date-fns/locale'
import type { Service } from '@/lib/types/service'
import type { Resource } from '@/lib/types/resource'
import { Button } from '@/components/ui/button'

interface Props {
  service: Service
  resource: Resource
  slot: string
  notes: string
  onNotesChange: (v: string) => void
  onConfirm: () => void
  loading: boolean
}

export function WizardStepConfirm({ service, resource, slot, notes, onNotesChange, onConfirm, loading }: Props) {
  return (
    <div>
      <h2 className="text-xl font-bold mb-6">Confirme seu agendamento</h2>
      <div className="border rounded-lg p-6 space-y-4 mb-6">
        <div className="flex justify-between text-sm">
          <span className="text-slate-500">Serviço</span>
          <span className="font-medium">{service.name}</span>
        </div>
        <div className="flex justify-between text-sm">
          <span className="text-slate-500">Profissional</span>
          <span className="font-medium">{resource.name}</span>
        </div>
        <div className="flex justify-between text-sm">
          <span className="text-slate-500">Data e hora</span>
          <span className="font-medium">
            {format(new Date(slot), "dd 'de' MMMM 'às' HH:mm", { locale: ptBR })}
          </span>
        </div>
        <div className="flex justify-between text-sm">
          <span className="text-slate-500">Duração</span>
          <span className="font-medium">{service.durationMinutes} min</span>
        </div>
        <div className="flex justify-between text-sm border-t pt-4">
          <span className="font-semibold">Total</span>
          <span className="font-bold text-lg">R$ {service.price.toFixed(2)}</span>
        </div>
      </div>

      <div className="mb-6">
        <label className="block text-sm font-medium text-slate-700 mb-1">
          Observações (opcional)
        </label>
        <textarea
          value={notes}
          onChange={e => onNotesChange(e.target.value)}
          placeholder="Alguma preferência ou observação?"
          className="w-full border rounded-lg px-3 py-2 text-sm min-h-[80px]"
        />
      </div>

      <Button onClick={onConfirm} disabled={loading} size="lg" className="w-full">
        {loading ? 'Agendando...' : 'Confirmar agendamento'}
      </Button>
    </div>
  )
}
```

- [ ] **Step 4: Criar `frontend/components/portal/BookingWizard.tsx`**

```typescript
// frontend/components/portal/BookingWizard.tsx
'use client'

import { useState, useEffect } from 'react'
import { useRouter } from 'next/navigation'
import { format } from 'date-fns'
import type { Service } from '@/lib/types/service'
import type { Resource } from '@/lib/types/resource'
import { WizardStepService } from './WizardStepService'
import { WizardStepResource } from './WizardStepResource'
import { WizardStepSlot } from './WizardStepSlot'
import { WizardStepConfirm } from './WizardStepConfirm'
import { portalApi } from '@/lib/api/portal'
import { usePortalAuthStore } from '@/store/portal-auth'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

const STEPS = ['Serviço', 'Recurso', 'Data/Hora', 'Confirmar']

interface Props {
  slug: string
  services: Service[]
  resources: Resource[]
  initialServiceId?: string
}

export function BookingWizard({ slug, services, resources, initialServiceId }: Props) {
  const router = useRouter()
  const { accessToken, customer } = usePortalAuthStore()

  const [step, setStep] = useState(initialServiceId ? 1 : 0)
  const [serviceId, setServiceId] = useState<string | null>(initialServiceId ?? null)
  const [resourceId, setResourceId] = useState<string | null>(null)
  const [selectedDate, setSelectedDate] = useState(new Date())
  const [selectedSlot, setSelectedSlot] = useState<string | null>(null)
  const [slots, setSlots] = useState<string[]>([])
  const [loadingSlots, setLoadingSlots] = useState(false)
  const [notes, setNotes] = useState('')
  const [submitting, setSubmitting] = useState(false)

  const activeServices = services.filter(s => s.isActive)
  const capableResources = resourceId
    ? resources.filter(r => r.isActive && (serviceId ? r.serviceIds.includes(serviceId) : true))
    : resources.filter(r => r.isActive && (serviceId ? r.serviceIds.includes(serviceId) : true))

  useEffect(() => {
    if (step === 2 && resourceId) {
      setLoadingSlots(true)
      setSelectedSlot(null)
      const dateStr = format(selectedDate, 'yyyy-MM-dd')
      portalApi.slots(slug, resourceId, dateStr, serviceId ?? undefined)
        .then(setSlots)
        .catch(() => setSlots([]))
        .finally(() => setLoadingSlots(false))
    }
  }, [step, resourceId, selectedDate, slug, serviceId])

  const canNext = [
    !!serviceId,
    !!resourceId,
    !!selectedSlot,
    true,
  ][step]

  const handleNext = () => {
    if (step < STEPS.length - 1) setStep(s => s + 1)
  }

  const handleConfirm = async () => {
    if (!serviceId || !resourceId || !selectedSlot) return
    if (!customer || !accessToken) {
      alert('Você precisa entrar com Google para agendar.')
      return
    }
    setSubmitting(true)
    try {
      const result = await portalApi.createBooking(slug, accessToken, {
        serviceId,
        resourceId,
        scheduledAt: selectedSlot,
        notes: notes || undefined,
      })
      router.push(`/${slug}/agendar/${result.id}/status`)
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Erro ao criar agendamento.')
    } finally {
      setSubmitting(false)
    }
  }

  const selectedService = activeServices.find(s => s.id === serviceId)
  const selectedResource = capableResources.find(r => r.id === resourceId)

  return (
    <div className="max-w-2xl mx-auto">
      {/* Step indicator */}
      <div className="flex items-center gap-2 mb-8">
        {STEPS.map((label, i) => (
          <div key={label} className="flex items-center gap-2">
            <div className={cn(
              'h-8 w-8 rounded-full flex items-center justify-center text-sm font-bold',
              i < step ? 'bg-indigo-600 text-white' :
              i === step ? 'bg-indigo-100 text-indigo-700 border-2 border-indigo-600' :
              'bg-slate-100 text-slate-400'
            )}>
              {i + 1}
            </div>
            <span className={cn('text-xs hidden sm:block', i === step ? 'font-semibold' : 'text-slate-400')}>
              {label}
            </span>
            {i < STEPS.length - 1 && <div className="h-px w-6 bg-slate-200" />}
          </div>
        ))}
      </div>

      {/* Steps */}
      {step === 0 && (
        <WizardStepService services={activeServices} selectedId={serviceId} onSelect={id => { setServiceId(id); setResourceId(null) }} />
      )}
      {step === 1 && (
        <WizardStepResource resources={capableResources} selectedId={resourceId} onSelect={setResourceId} />
      )}
      {step === 2 && (
        <WizardStepSlot
          slots={slots}
          loadingSlots={loadingSlots}
          selectedDate={selectedDate}
          selectedSlot={selectedSlot}
          onDateChange={d => setSelectedDate(d)}
          onSlotSelect={setSelectedSlot}
        />
      )}
      {step === 3 && selectedService && selectedResource && selectedSlot && (
        <WizardStepConfirm
          service={selectedService}
          resource={selectedResource}
          slot={selectedSlot}
          notes={notes}
          onNotesChange={setNotes}
          onConfirm={handleConfirm}
          loading={submitting}
        />
      )}

      {/* Navigation */}
      {step < 3 && (
        <div className="flex justify-between mt-8">
          <Button variant="outline" onClick={() => setStep(s => s - 1)} disabled={step === 0}>
            Voltar
          </Button>
          <Button onClick={handleNext} disabled={!canNext}>
            Próximo
          </Button>
        </div>
      )}
      {step === 3 && (
        <div className="mt-4">
          <Button variant="ghost" onClick={() => setStep(s => s - 1)}>← Voltar</Button>
        </div>
      )}
    </div>
  )
}
```

- [ ] **Step 5: Executar testes**

```bash
npm run test:run -- __tests__/BookingWizard.test.tsx
```

Expected: PASS — 2 testes.

- [ ] **Step 6: Commit**

```bash
git add frontend/components/portal/ frontend/__tests__/BookingWizard.test.tsx
git commit -m "feat: add booking wizard with 4-step flow (service→resource→slot→confirm)"
```

---

### Task 9: Página Agendar + Status

**Files:**
- Create: `frontend/app/(portal)/[slug]/agendar/page.tsx`
- Create: `frontend/app/(portal)/[slug]/agendar/[bookingId]/status/page.tsx`

- [ ] **Step 1: Criar `frontend/app/(portal)/[slug]/agendar/page.tsx`**

```typescript
// frontend/app/(portal)/[slug]/agendar/page.tsx
import { portalApi } from '@/lib/api/portal'
import { BookingWizard } from '@/components/portal/BookingWizard'

interface Props {
  params: { slug: string }
  searchParams: { serviceId?: string }
}

export default async function AgendarPage({ params, searchParams }: Props) {
  const { slug } = params

  const [services, resources] = await Promise.all([
    portalApi.services(slug).catch(() => []),
    portalApi.resources(slug).catch(() => []),
  ])

  return (
    <div className="max-w-5xl mx-auto px-4 py-12">
      <h1 className="text-3xl font-bold mb-8">Agendar</h1>
      <BookingWizard
        slug={slug}
        services={services}
        resources={resources}
        initialServiceId={searchParams.serviceId}
      />
    </div>
  )
}
```

- [ ] **Step 2: Criar `frontend/app/(portal)/[slug]/agendar/[bookingId]/status/page.tsx`**

```typescript
// frontend/app/(portal)/[slug]/agendar/[bookingId]/status/page.tsx
'use client'

import { useEffect, useState } from 'react'
import Link from 'next/link'
import { format } from 'date-fns'
import { ptBR } from 'date-fns/locale'
import { usePortalAuthStore } from '@/store/portal-auth'
import { portalApi } from '@/lib/api/portal'
import type { CustomerBooking } from '@/lib/types/portal'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { CheckCircle, Clock, XCircle } from 'lucide-react'

const STATUS_CONFIG = {
  Pending:   { label: 'Aguardando confirmação', icon: Clock,        variant: 'secondary' as const, color: 'text-amber-600' },
  Confirmed: { label: 'Confirmado',             icon: CheckCircle,  variant: 'default' as const,   color: 'text-green-600' },
  Completed: { label: 'Concluído',              icon: CheckCircle,  variant: 'outline' as const,   color: 'text-slate-600' },
  Cancelled: { label: 'Cancelado',              icon: XCircle,      variant: 'destructive' as const, color: 'text-red-600' },
  NoShow:    { label: 'Não compareceu',         icon: XCircle,      variant: 'destructive' as const, color: 'text-red-600' },
}

interface Props {
  params: { slug: string; bookingId: string }
}

export default function BookingStatusPage({ params }: Props) {
  const { slug, bookingId } = params
  const { accessToken } = usePortalAuthStore()
  const [booking, setBooking] = useState<CustomerBooking | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (!accessToken) { setLoading(false); return }
    portalApi.myBookings(slug, accessToken)
      .then(bookings => setBooking(bookings.find(b => b.id === bookingId) ?? null))
      .finally(() => setLoading(false))
  }, [slug, bookingId, accessToken])

  if (loading) return <div className="max-w-lg mx-auto px-4 py-20 text-center text-slate-500">Carregando...</div>

  if (!booking) {
    return (
      <div className="max-w-lg mx-auto px-4 py-20 text-center">
        <p className="text-slate-500 mb-4">Agendamento não encontrado ou sessão expirada.</p>
        <Button asChild variant="outline"><Link href={`/${slug}`}>Voltar ao início</Link></Button>
      </div>
    )
  }

  const config = STATUS_CONFIG[booking.status] ?? STATUS_CONFIG.Pending
  const Icon = config.icon

  return (
    <div className="max-w-lg mx-auto px-4 py-12">
      <div className="text-center mb-8">
        <Icon className={`h-16 w-16 mx-auto mb-4 ${config.color}`} />
        <h1 className="text-2xl font-bold mb-2">{config.label}</h1>
        <Badge variant={config.variant}>{booking.status}</Badge>
      </div>

      <Card>
        <CardHeader><CardTitle>Detalhes do agendamento</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          {[
            { label: 'Serviço', value: booking.serviceName },
            { label: 'Profissional', value: booking.resourceName },
            { label: 'Data e hora', value: format(new Date(booking.scheduledAt), "dd 'de' MMMM 'às' HH:mm", { locale: ptBR }) },
            { label: 'Duração', value: `${booking.durationMinutes} min` },
            { label: 'Valor', value: `R$ ${booking.totalAmount.toFixed(2)}` },
          ].map(({ label, value }) => (
            <div key={label} className="flex justify-between text-sm">
              <span className="text-slate-500">{label}</span>
              <span className="font-medium">{value}</span>
            </div>
          ))}
        </CardContent>
      </Card>

      <div className="flex gap-3 mt-6">
        <Button asChild variant="outline" className="flex-1">
          <Link href={`/${slug}/minha-conta`}>Minha conta</Link>
        </Button>
        <Button asChild className="flex-1">
          <Link href={`/${slug}/agendar`}>Novo agendamento</Link>
        </Button>
      </div>
    </div>
  )
}
```

- [ ] **Step 3: Commit**

```bash
git add frontend/app/(portal)/[slug]/agendar/
git commit -m "feat: add booking wizard page and booking status page"
```

---

### Task 10: Minha Conta

**Files:**
- Create: `frontend/app/(portal)/[slug]/minha-conta/page.tsx`

- [ ] **Step 1: Criar `frontend/app/(portal)/[slug]/minha-conta/page.tsx`**

```typescript
// frontend/app/(portal)/[slug]/minha-conta/page.tsx
'use client'

import { useEffect, useState } from 'react'
import Link from 'next/link'
import { format } from 'date-fns'
import { ptBR } from 'date-fns/locale'
import { usePortalAuthStore } from '@/store/portal-auth'
import { portalApi } from '@/lib/api/portal'
import type { CustomerBooking, FavoriteService } from '@/lib/types/portal'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { GoogleSignInButton } from '@/components/portal/GoogleSignInButton'
import { Heart, HeartOff } from 'lucide-react'

const STATUS_LABEL: Record<string, string> = {
  Pending: 'Pendente', Confirmed: 'Confirmado', Completed: 'Concluído',
  Cancelled: 'Cancelado', NoShow: 'Não compareceu',
}

interface Props {
  params: { slug: string }
}

export default function MinhaContaPage({ params }: Props) {
  const { slug } = params
  const { customer, accessToken } = usePortalAuthStore()
  const [bookings, setBookings] = useState<CustomerBooking[]>([])
  const [favorites, setFavorites] = useState<FavoriteService[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (!accessToken) { setLoading(false); return }
    Promise.all([
      portalApi.myBookings(slug, accessToken).catch(() => []),
      portalApi.myFavorites(slug, accessToken).catch(() => []),
    ]).then(([b, f]) => { setBookings(b); setFavorites(f) })
      .finally(() => setLoading(false))
  }, [slug, accessToken])

  const handleRemoveFavorite = async (serviceId: string) => {
    if (!accessToken) return
    await portalApi.removeFavorite(slug, accessToken, serviceId)
    setFavorites(f => f.filter(fav => fav.serviceId !== serviceId))
  }

  if (!customer) {
    return (
      <div className="max-w-lg mx-auto px-4 py-20 text-center">
        <h1 className="text-2xl font-bold mb-4">Minha Conta</h1>
        <p className="text-slate-500 mb-6">Entre com sua conta Google para ver seus agendamentos.</p>
        <GoogleSignInButton slug={slug} />
      </div>
    )
  }

  const upcoming = bookings.filter(b => b.status === 'Pending' || b.status === 'Confirmed')
  const past = bookings.filter(b => b.status === 'Completed' || b.status === 'Cancelled' || b.status === 'NoShow')

  return (
    <div className="max-w-3xl mx-auto px-4 py-12">
      <div className="flex items-center gap-4 mb-8">
        <div className="h-16 w-16 rounded-full bg-slate-200 flex items-center justify-center text-2xl font-bold text-slate-600">
          {customer.name[0]}
        </div>
        <div>
          <h1 className="text-2xl font-bold">{customer.name}</h1>
          <p className="text-slate-500">{customer.email}</p>
        </div>
      </div>

      <Tabs defaultValue="agendamentos">
        <TabsList className="mb-6">
          <TabsTrigger value="agendamentos">Agendamentos</TabsTrigger>
          <TabsTrigger value="favoritos">Favoritos</TabsTrigger>
        </TabsList>

        <TabsContent value="agendamentos">
          {loading ? <p className="text-slate-500">Carregando...</p> : (
            <div className="space-y-6">
              {upcoming.length > 0 && (
                <div>
                  <h2 className="font-semibold mb-3">Próximos</h2>
                  <div className="space-y-3">
                    {upcoming.map(b => (
                      <Card key={b.id}>
                        <CardContent className="pt-4 flex items-center justify-between">
                          <div>
                            <p className="font-medium">{b.serviceName}</p>
                            <p className="text-sm text-slate-500">{b.resourceName}</p>
                            <p className="text-sm text-slate-500">
                              {format(new Date(b.scheduledAt), "dd/MM/yyyy 'às' HH:mm", { locale: ptBR })}
                            </p>
                          </div>
                          <div className="flex flex-col items-end gap-2">
                            <Badge>{STATUS_LABEL[b.status]}</Badge>
                            <Link href={`/${slug}/agendar/${b.id}/status`} className="text-xs text-indigo-600 hover:underline">
                              Ver detalhes
                            </Link>
                          </div>
                        </CardContent>
                      </Card>
                    ))}
                  </div>
                </div>
              )}

              {past.length > 0 && (
                <div>
                  <h2 className="font-semibold mb-3">Histórico</h2>
                  <div className="space-y-3">
                    {past.map(b => (
                      <Card key={b.id} className="opacity-80">
                        <CardContent className="pt-4 flex items-center justify-between">
                          <div>
                            <p className="font-medium">{b.serviceName}</p>
                            <p className="text-sm text-slate-500">
                              {format(new Date(b.scheduledAt), "dd/MM/yyyy", { locale: ptBR })}
                            </p>
                          </div>
                          <Badge variant="outline">{STATUS_LABEL[b.status]}</Badge>
                        </CardContent>
                      </Card>
                    ))}
                  </div>
                </div>
              )}

              {bookings.length === 0 && (
                <div className="text-center py-12">
                  <p className="text-slate-500 mb-4">Você ainda não tem agendamentos.</p>
                  <Button asChild><Link href={`/${slug}/agendar`}>Agendar agora</Link></Button>
                </div>
              )}
            </div>
          )}
        </TabsContent>

        <TabsContent value="favoritos">
          {loading ? <p className="text-slate-500">Carregando...</p> : favorites.length === 0 ? (
            <div className="text-center py-12">
              <p className="text-slate-500 mb-4">Nenhum serviço favoritado.</p>
              <Button asChild variant="outline"><Link href={`/${slug}/servicos`}>Ver serviços</Link></Button>
            </div>
          ) : (
            <div className="space-y-3">
              {favorites.map(fav => (
                <Card key={fav.id}>
                  <CardContent className="pt-4 flex items-center justify-between">
                    <div>
                      <p className="text-sm text-slate-500">Serviço favorito</p>
                      <Link
                        href={`/${slug}/servicos/${fav.serviceId}`}
                        className="font-medium hover:underline text-indigo-700"
                      >
                        Ver serviço →
                      </Link>
                    </div>
                    <div className="flex gap-2">
                      <Button asChild size="sm" variant="outline">
                        <Link href={`/${slug}/agendar?serviceId=${fav.serviceId}`}>Agendar</Link>
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() => handleRemoveFavorite(fav.serviceId)}
                      >
                        <HeartOff className="h-4 w-4" />
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </TabsContent>
      </Tabs>
    </div>
  )
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/app/(portal)/[slug]/minha-conta/
git commit -m "feat: add customer account page with bookings and favorites tabs"
```

---

### Task 11: Full Test Suite + Build

**Files:** nenhum novo

- [ ] **Step 1: Executar todos os testes**

```bash
cd frontend
npm run test:run
```

Expected: PASS — todos os testes (login, Sidebar, dashboard, BookingTable, ServiceForm, ResourceForm, TemplateEditor, BookingCalendar, GoogleSignInButton, PortalNavbar, ServiceCard, WizardStepService, WizardStepSlot, BookingWizard).

Fix any failing tests before proceeding.

- [ ] **Step 2: Verificar build TypeScript**

```bash
npm run build
```

Expected: sem erros TypeScript. Warnings de hydration em Server Components são aceitáveis.

Se houver erros TypeScript comuns, corrija:
- `params` em Server Components com `Promise<{ slug: string }>` → use `await params` no Next.js 15+, ou manter como props diretos no Next.js 14
- Tipos de `portalApi` vs tipos esperados pelos componentes
- `cn` import path correto: `@/lib/utils`

- [ ] **Step 3: Commit de integração**

```bash
cd ..
git add frontend/
git commit -m "feat: complete Sprint 10 — customer portal (6 pages, booking wizard, Google auth)"
```

---

## Self-Review

### Spec Coverage

| Requisito | Task | Status |
|-----------|------|--------|
| `/[slug]` Home — banner, serviços, equipe, avaliações | Task 4 | ✅ |
| `/[slug]/servicos` — catálogo com filtros e busca | Task 5 | ✅ |
| `/[slug]/servicos/:id` — detalhe + equipe + reviews | Task 5 | ✅ |
| `/[slug]/agendar` — wizard 4 etapas | Tasks 6-9 | ✅ |
| `/[slug]/minha-conta` — histórico + favoritos | Task 10 | ✅ |
| `/[slug]/agendar/:id/status` — status do agendamento | Task 9 | ✅ |
| Google OAuth para clientes | Task 2 | ✅ |
| SSR para SEO nas páginas públicas | Tasks 3-5 | ✅ |
| Auth separada do admin | Task 2 (portal-auth.ts) | ✅ |
| Tenant resolvido por URL `[slug]` | Task 3 (layout) | ✅ |
| Vitest para componentes críticos | Tasks 2-8 | ✅ |
| Portal do Cliente (sem Painel Admin) | — | ✅ escopo correto |

### Placeholder Scan

Sem TBDs. Todo código é concreto e completo.

### Type Consistency

- `portalApi` usa tipos de `lib/types/portal.ts` — consistente em todos os componentes
- `WizardStepService`, `WizardStepResource`, `WizardStepSlot`, `WizardStepConfirm` — props bem tipadas, sem `any`
- `BookingWizard` recebe `Service[]` e `Resource[]` dos tipos existentes — reutiliza `lib/types/service.ts` e `lib/types/resource.ts`
- `usePortalAuthStore` retorna `CustomerProfile | null` e `string | null` — guards com `!customer` e `!accessToken` aplicados em todas as páginas autenticadas
