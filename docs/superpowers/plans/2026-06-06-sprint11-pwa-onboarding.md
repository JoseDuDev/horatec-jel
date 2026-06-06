# Sprint 11 — PWA + Onboarding + Avaliações + Upsell

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transformar o portal em PWA instalável, criar wizard de onboarding de 5 passos para novos tenants, adicionar formulário de avaliação no portal do cliente e seção de upsell no detalhe do serviço.

**Architecture:** Quatro features independentes, todas frontend-only (todos os endpoints já existem no backend). PWA via `@ducanh2912/next-pwa` no `next.config.ts`. Onboarding em `/admin/onboarding` com 5 etapas em estado local. Avaliações em `/[slug]/minha-conta` para bookings com status `Completed`. Upsell na página `/[slug]/servicos/[id]` filtrando por categoria.

**Tech Stack:** Next.js 16 (existente), `@ducanh2912/next-pwa`, shadcn/ui (existente), React Hook Form + Zod (existente), Vitest + Testing Library (existente).

---

## File Map

```
frontend/
├── public/
│   ├── manifest.json                       # PWA manifest
│   ├── icon-192.png                        # ícone PWA (placeholder)
│   └── icon-512.png                        # ícone PWA (placeholder)
├── app/
│   ├── layout.tsx                          # (modify) adicionar meta tags PWA
│   └── (admin)/
│       └── admin/
│           └── onboarding/
│               └── page.tsx                # Wizard de onboarding
├── components/
│   ├── portal/
│   │   └── ReviewForm.tsx                  # Formulário de avaliação
│   └── onboarding/
│       ├── OnboardingStepTenant.tsx        # Etapa 1: info básica
│       ├── OnboardingStepTheme.tsx         # Etapa 2: identidade visual
│       ├── OnboardingStepService.tsx       # Etapa 3: primeiro serviço
│       ├── OnboardingStepResource.tsx      # Etapa 4: primeiro recurso
│       └── OnboardingStepHours.tsx         # Etapa 5: horários
├── lib/
│   └── api/
│       └── onboarding.ts                   # chamadas de API do onboarding
└── next.config.ts                          # (modify) adicionar withPWA
```

---

### Task 1: PWA Setup

**Files:**
- Modify: `frontend/next.config.ts`
- Create: `frontend/public/manifest.json`
- Modify: `frontend/app/layout.tsx`

- [ ] **Step 1: Instalar `@ducanh2912/next-pwa`**

```bash
cd frontend
npm install @ducanh2912/next-pwa
npm install -D webpack
```

- [ ] **Step 2: Ler e atualizar `frontend/next.config.ts`**

Ler o arquivo atual primeiro, depois substituir por:

```typescript
// frontend/next.config.ts
import type { NextConfig } from 'next'
import withPWAInit from '@ducanh2912/next-pwa'

const withPWA = withPWAInit({
  dest: 'public',
  cacheOnFrontEndNav: true,
  aggressiveFrontEndNavCaching: true,
  reloadOnOnline: true,
  disable: process.env.NODE_ENV === 'development',
})

const nextConfig: NextConfig = {
  env: {
    NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000',
  },
}

export default withPWA(nextConfig)
```

- [ ] **Step 3: Criar `frontend/public/manifest.json`**

```json
{
  "name": "Horafy",
  "short_name": "Horafy",
  "description": "Agendamentos online fáceis e rápidos",
  "start_url": "/",
  "display": "standalone",
  "background_color": "#ffffff",
  "theme_color": "#6366f1",
  "orientation": "portrait",
  "icons": [
    {
      "src": "/icon-192.png",
      "sizes": "192x192",
      "type": "image/png",
      "purpose": "any maskable"
    },
    {
      "src": "/icon-512.png",
      "sizes": "512x512",
      "type": "image/png",
      "purpose": "any maskable"
    }
  ]
}
```

- [ ] **Step 4: Criar ícones placeholder**

Criar dois arquivos SVG renomeados como PNG (placeholder — em produção seriam ícones reais):

```bash
cd frontend/public
# Criar icon-192.png e icon-512.png como cópias do favicon existente
# Se não existir favicon, criar um SVG simples e converter
```

Na prática, crie um arquivo `icon-192.png` copiando qualquer PNG existente em public/, ou crie um arquivo mínimo. Use PowerShell:

```powershell
# Copiar favicon.ico como PNG placeholder se existir, senão criar arquivo vazio
if (Test-Path "frontend/public/favicon.ico") {
    Copy-Item "frontend/public/favicon.ico" "frontend/public/icon-192.png"
    Copy-Item "frontend/public/favicon.ico" "frontend/public/icon-512.png"
} else {
    # Criar arquivo PNG mínimo (1x1 pixel PNG)
    $bytes = [byte[]](137,80,78,71,13,10,26,10,0,0,0,13,73,72,68,82,0,0,0,1,0,0,0,1,8,2,0,0,0,144,119,83,222,0,0,0,12,73,68,65,84,8,215,99,248,207,192,0,0,0,2,0,1,226,33,188,51,0,0,0,0,73,69,78,68,174,66,96,130)
    [System.IO.File]::WriteAllBytes("$PWD/frontend/public/icon-192.png", $bytes)
    [System.IO.File]::WriteAllBytes("$PWD/frontend/public/icon-512.png", $bytes)
}
```

- [ ] **Step 5: Ler e atualizar `frontend/app/layout.tsx`**

Ler o arquivo atual. Adicionar as meta tags PWA no `<head>`. O `layout.tsx` raiz provavelmente tem uma estrutura básica — adicionar dentro do `<head>`:

```typescript
// Adicionar ao export const metadata ou ao <head> do RootLayout:
export const metadata = {
  // ... existente ...
  manifest: '/manifest.json',
  themeColor: '#6366f1',
  appleWebApp: {
    capable: true,
    statusBarStyle: 'default',
    title: 'Horafy',
  },
  viewport: {
    width: 'device-width',
    initialScale: 1,
    maximumScale: 1,
  },
}
```

Se o layout.tsx raiz não tiver `export const metadata`, adicione-o. Se já tiver, merge os campos acima.

Também adicionar dentro do `<head>` do `RootLayout`:
```tsx
<link rel="manifest" href="/manifest.json" />
<meta name="theme-color" content="#6366f1" />
<link rel="apple-touch-icon" href="/icon-192.png" />
```

- [ ] **Step 6: Verificar build**

```bash
cd frontend
npm run build
```

Expected: build sem erros. O PWA gera `public/sw.js` e `public/workbox-*.js` automaticamente em produção (`NODE_ENV=production`). Em desenvolvimento fica desativado.

- [ ] **Step 7: Commit**

```bash
git add frontend/next.config.ts frontend/public/manifest.json frontend/public/icon-192.png frontend/public/icon-512.png frontend/app/layout.tsx
git commit -m "feat: add PWA support with manifest and service worker"
```

---

### Task 2: Onboarding API Client

**Files:**
- Create: `frontend/lib/api/onboarding.ts`

- [ ] **Step 1: Criar `frontend/lib/api/onboarding.ts`**

```typescript
// frontend/lib/api/onboarding.ts
import { apiFetch } from './client'

export interface OnboardingTenantData {
  name: string
  timezone: string
}

export interface OnboardingThemeData {
  primaryColor: string
  logoUrl?: string
}

export interface OnboardingServiceData {
  name: string
  description?: string
  durationMinutes: number
  price: number
}

export interface OnboardingResourceData {
  name: string
  type: string
}

export interface OnboardingHoursData {
  schedule: Array<{
    dayOfWeek: number   // 0=Sunday ... 6=Saturday
    isOpen: boolean
    openTime: string    // "HH:mm:ss"
    closeTime: string   // "HH:mm:ss"
  }>
}

export const onboardingApi = {
  updateTenant: (data: OnboardingTenantData) =>
    apiFetch<void>('/api/v1/tenants/me', {
      method: 'PUT',
      body: JSON.stringify(data),
    }),

  updateTheme: (data: OnboardingThemeData) =>
    apiFetch<void>('/api/v1/tenants/me/theme', {
      method: 'PUT',
      body: JSON.stringify(data),
    }),

  createService: (data: OnboardingServiceData) =>
    apiFetch<{ id: string }>('/api/v1/services', {
      method: 'POST',
      body: JSON.stringify({ ...data, isActive: true }),
    }),

  createResource: (data: OnboardingResourceData) =>
    apiFetch<{ id: string }>('/api/v1/resources', {
      method: 'POST',
      body: JSON.stringify({ ...data, serviceIds: [] }),
    }),

  setBusinessHours: (dayOfWeek: number, isOpen: boolean, openTime: string, closeTime: string) =>
    apiFetch<void>('/api/v1/availability/business-hours', {
      method: 'PUT',
      body: JSON.stringify({ dayOfWeek, isOpen, openTime, closeTime }),
    }),
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/lib/api/onboarding.ts
git commit -m "feat: add onboarding API client"
```

---

### Task 3: Onboarding Step Components

**Files:**
- Create: `frontend/components/onboarding/OnboardingStepTenant.tsx`
- Create: `frontend/components/onboarding/OnboardingStepTheme.tsx`
- Create: `frontend/components/onboarding/OnboardingStepService.tsx`
- Create: `frontend/components/onboarding/OnboardingStepResource.tsx`
- Create: `frontend/components/onboarding/OnboardingStepHours.tsx`
- Create: `frontend/__tests__/OnboardingStepTenant.test.tsx`

- [ ] **Step 1: Escrever teste do step de tenant**

```typescript
// frontend/__tests__/OnboardingStepTenant.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { OnboardingStepTenant } from '@/components/onboarding/OnboardingStepTenant'

describe('OnboardingStepTenant', () => {
  it('shows validation error when name is empty', async () => {
    render(<OnboardingStepTenant onNext={vi.fn()} />)
    fireEvent.click(screen.getByRole('button', { name: /próximo/i }))
    await waitFor(() => {
      expect(screen.getByText(/nome obrigatório/i)).toBeInTheDocument()
    })
  })

  it('calls onNext with form data when valid', async () => {
    const onNext = vi.fn()
    render(<OnboardingStepTenant onNext={onNext} />)
    await userEvent.type(screen.getByLabelText(/nome do negócio/i), 'Barbearia do João')
    fireEvent.click(screen.getByRole('button', { name: /próximo/i }))
    await waitFor(() => {
      expect(onNext).toHaveBeenCalledWith(
        expect.objectContaining({ name: 'Barbearia do João' })
      )
    })
  })
})
```

- [ ] **Step 2: Executar teste para verificar falha**

```bash
cd frontend && npm run test:run -- __tests__/OnboardingStepTenant.test.tsx
```

Expected: FAIL.

- [ ] **Step 3: Criar `frontend/components/onboarding/OnboardingStepTenant.tsx`**

```typescript
// frontend/components/onboarding/OnboardingStepTenant.tsx
'use client'

import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { OnboardingTenantData } from '@/lib/api/onboarding'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

const schema = z.object({
  name: z.string().min(1, 'Nome obrigatório'),
  timezone: z.string().min(1, 'Fuso obrigatório'),
})

type FormData = z.infer<typeof schema>

const TIMEZONES = [
  'America/Sao_Paulo',
  'America/Manaus',
  'America/Belem',
  'America/Fortaleza',
  'America/Recife',
  'America/Bahia',
  'America/Cuiaba',
  'America/Porto_Velho',
  'America/Boa_Vista',
  'America/Rio_Branco',
  'America/Noronha',
]

interface Props {
  initial?: Partial<OnboardingTenantData>
  onNext: (data: OnboardingTenantData) => void
}

export function OnboardingStepTenant({ initial, onNext }: Props) {
  const { register, handleSubmit, setValue, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: initial?.name ?? '',
      timezone: initial?.timezone ?? 'America/Sao_Paulo',
    },
  })

  return (
    <form onSubmit={handleSubmit((data) => onNext(data))} className="space-y-6">
      <div>
        <h2 className="text-xl font-bold mb-1">Informações do negócio</h2>
        <p className="text-sm text-slate-500 mb-6">Como seu negócio se chama e onde está localizado?</p>
      </div>
      <div>
        <Label htmlFor="name">Nome do Negócio</Label>
        <Input id="name" {...register('name')} placeholder="Ex: Barbearia do João" />
        {errors.name && <p className="text-sm text-red-500 mt-1">{errors.name.message}</p>}
      </div>
      <div>
        <Label htmlFor="timezone">Fuso Horário</Label>
        <Select
          defaultValue={initial?.timezone ?? 'America/Sao_Paulo'}
          onValueChange={v => setValue('timezone', v)}
        >
          <SelectTrigger id="timezone">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {TIMEZONES.map(tz => (
              <SelectItem key={tz} value={tz}>{tz.replace('America/', '')}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        {errors.timezone && <p className="text-sm text-red-500 mt-1">{errors.timezone.message}</p>}
      </div>
      <Button type="submit" className="w-full">Próximo →</Button>
    </form>
  )
}
```

- [ ] **Step 4: Criar `frontend/components/onboarding/OnboardingStepTheme.tsx`**

```typescript
// frontend/components/onboarding/OnboardingStepTheme.tsx
'use client'

import { useState } from 'react'
import type { OnboardingThemeData } from '@/lib/api/onboarding'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

const PRESET_COLORS = [
  '#6366f1', '#8b5cf6', '#ec4899', '#f97316',
  '#10b981', '#3b82f6', '#ef4444', '#1e293b',
]

interface Props {
  onNext: (data: OnboardingThemeData) => void
  onBack: () => void
}

export function OnboardingStepTheme({ onNext, onBack }: Props) {
  const [color, setColor] = useState('#6366f1')
  const [logoUrl, setLogoUrl] = useState('')

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold mb-1">Identidade Visual</h2>
        <p className="text-sm text-slate-500 mb-6">Escolha a cor principal e adicione seu logo.</p>
      </div>

      <div>
        <Label>Cor Principal</Label>
        <div className="flex gap-2 mt-2 flex-wrap">
          {PRESET_COLORS.map(c => (
            <button
              key={c}
              type="button"
              onClick={() => setColor(c)}
              className="h-9 w-9 rounded-full border-2 transition-all"
              style={{
                backgroundColor: c,
                borderColor: color === c ? '#1e293b' : 'transparent',
              }}
            />
          ))}
          <input
            type="color"
            value={color}
            onChange={e => setColor(e.target.value)}
            className="h-9 w-9 rounded-full border cursor-pointer"
          />
        </div>
        <div className="mt-3 p-3 rounded-lg text-white text-sm font-medium" style={{ backgroundColor: color }}>
          Prévia da cor selecionada
        </div>
      </div>

      <div>
        <Label htmlFor="logoUrl">URL do Logo (opcional)</Label>
        <Input
          id="logoUrl"
          value={logoUrl}
          onChange={e => setLogoUrl(e.target.value)}
          placeholder="https://exemplo.com/logo.png"
        />
        {logoUrl && (
          <img src={logoUrl} alt="Preview" className="mt-2 h-12 object-contain" onError={() => setLogoUrl('')} />
        )}
      </div>

      <div className="flex gap-3">
        <Button variant="outline" onClick={onBack} className="flex-1">← Voltar</Button>
        <Button
          onClick={() => onNext({ primaryColor: color, logoUrl: logoUrl || undefined })}
          className="flex-1"
        >
          Próximo →
        </Button>
      </div>
    </div>
  )
}
```

- [ ] **Step 5: Criar `frontend/components/onboarding/OnboardingStepService.tsx`**

```typescript
// frontend/components/onboarding/OnboardingStepService.tsx
'use client'

import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { OnboardingServiceData } from '@/lib/api/onboarding'
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
  onNext: (data: OnboardingServiceData) => void
  onBack: () => void
}

export function OnboardingStepService({ onNext, onBack }: Props) {
  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { durationMinutes: 60, price: 0 },
  })

  return (
    <form onSubmit={handleSubmit((data) => onNext(data))} className="space-y-6">
      <div>
        <h2 className="text-xl font-bold mb-1">Primeiro serviço</h2>
        <p className="text-sm text-slate-500 mb-6">Cadastre o principal serviço que você oferece.</p>
      </div>
      <div>
        <Label htmlFor="svc-name">Nome do Serviço</Label>
        <Input id="svc-name" {...register('name')} placeholder="Ex: Corte de Cabelo" />
        {errors.name && <p className="text-sm text-red-500 mt-1">{errors.name.message}</p>}
      </div>
      <div>
        <Label htmlFor="svc-desc">Descrição (opcional)</Label>
        <Input id="svc-desc" {...register('description')} placeholder="Breve descrição..." />
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div>
          <Label htmlFor="svc-duration">Duração (min)</Label>
          <Input id="svc-duration" type="number" {...register('durationMinutes')} />
          {errors.durationMinutes && <p className="text-sm text-red-500 mt-1">{errors.durationMinutes.message}</p>}
        </div>
        <div>
          <Label htmlFor="svc-price">Preço (R$)</Label>
          <Input id="svc-price" type="number" step="0.01" {...register('price')} />
          {errors.price && <p className="text-sm text-red-500 mt-1">{errors.price.message}</p>}
        </div>
      </div>
      <div className="flex gap-3">
        <Button type="button" variant="outline" onClick={onBack} className="flex-1">← Voltar</Button>
        <Button type="submit" className="flex-1">Próximo →</Button>
      </div>
    </form>
  )
}
```

- [ ] **Step 6: Criar `frontend/components/onboarding/OnboardingStepResource.tsx`**

```typescript
// frontend/components/onboarding/OnboardingStepResource.tsx
'use client'

import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { OnboardingResourceData } from '@/lib/api/onboarding'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

const schema = z.object({
  name: z.string().min(1, 'Nome obrigatório'),
  type: z.string().min(1, 'Tipo obrigatório'),
})

type FormData = z.infer<typeof schema>

const RESOURCE_TYPES = ['Professional', 'PhysicalSpace', 'Equipment', 'Court']
const RESOURCE_TYPE_LABELS: Record<string, string> = {
  Professional: 'Profissional',
  PhysicalSpace: 'Espaço Físico',
  Equipment: 'Equipamento',
  Court: 'Quadra',
}

interface Props {
  onNext: (data: OnboardingResourceData) => void
  onBack: () => void
}

export function OnboardingStepResource({ onNext, onBack }: Props) {
  const { register, handleSubmit, setValue, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { type: 'Professional' },
  })

  return (
    <form onSubmit={handleSubmit((data) => onNext(data))} className="space-y-6">
      <div>
        <h2 className="text-xl font-bold mb-1">Primeiro recurso</h2>
        <p className="text-sm text-slate-500 mb-6">
          Quem ou o que executa o serviço? (profissional, sala, equipamento...)
        </p>
      </div>
      <div>
        <Label htmlFor="res-type">Tipo de Recurso</Label>
        <Select defaultValue="Professional" onValueChange={v => setValue('type', v)}>
          <SelectTrigger id="res-type">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {RESOURCE_TYPES.map(t => (
              <SelectItem key={t} value={t}>{RESOURCE_TYPE_LABELS[t]}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        {errors.type && <p className="text-sm text-red-500 mt-1">{errors.type.message}</p>}
      </div>
      <div>
        <Label htmlFor="res-name">Nome</Label>
        <Input id="res-name" {...register('name')} placeholder="Ex: João Silva" />
        {errors.name && <p className="text-sm text-red-500 mt-1">{errors.name.message}</p>}
      </div>
      <div className="flex gap-3">
        <Button type="button" variant="outline" onClick={onBack} className="flex-1">← Voltar</Button>
        <Button type="submit" className="flex-1">Próximo →</Button>
      </div>
    </form>
  )
}
```

- [ ] **Step 7: Criar `frontend/components/onboarding/OnboardingStepHours.tsx`**

```typescript
// frontend/components/onboarding/OnboardingStepHours.tsx
'use client'

import { useState } from 'react'
import type { OnboardingHoursData } from '@/lib/api/onboarding'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

const DAYS = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb']

const DEFAULT_SCHEDULE = DAYS.map((_, i) => ({
  dayOfWeek: i,
  isOpen: i >= 1 && i <= 6,  // Seg-Sáb
  openTime: '09:00:00',
  closeTime: '18:00:00',
}))

interface Props {
  onFinish: (data: OnboardingHoursData) => void
  onBack: () => void
  loading: boolean
}

export function OnboardingStepHours({ onFinish, onBack, loading }: Props) {
  const [schedule, setSchedule] = useState(DEFAULT_SCHEDULE)

  const update = (index: number, field: string, value: string | boolean) => {
    setSchedule(s => s.map((day, i) => i === index ? { ...day, [field]: value } : day))
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold mb-1">Horários de funcionamento</h2>
        <p className="text-sm text-slate-500 mb-6">Defina quando seu negócio está aberto.</p>
      </div>

      <div className="space-y-3">
        {schedule.map((day, i) => (
          <div key={day.dayOfWeek} className="flex items-center gap-3">
            <label className="flex items-center gap-2 w-24 shrink-0 cursor-pointer">
              <input
                type="checkbox"
                checked={day.isOpen}
                onChange={e => update(i, 'isOpen', e.target.checked)}
                className="rounded"
              />
              <span className="text-sm font-medium">{DAYS[day.dayOfWeek]}</span>
            </label>
            {day.isOpen ? (
              <>
                <Input
                  type="time"
                  value={day.openTime.slice(0, 5)}
                  onChange={e => update(i, 'openTime', `${e.target.value}:00`)}
                  className="w-28"
                />
                <span className="text-slate-400 text-sm">até</span>
                <Input
                  type="time"
                  value={day.closeTime.slice(0, 5)}
                  onChange={e => update(i, 'closeTime', `${e.target.value}:00`)}
                  className="w-28"
                />
              </>
            ) : (
              <span className="text-sm text-slate-400">Fechado</span>
            )}
          </div>
        ))}
      </div>

      <div className="flex gap-3">
        <Button variant="outline" onClick={onBack} className="flex-1">← Voltar</Button>
        <Button
          onClick={() => onFinish({ schedule })}
          disabled={loading}
          className="flex-1"
        >
          {loading ? 'Salvando...' : 'Concluir ✓'}
        </Button>
      </div>
    </div>
  )
}
```

- [ ] **Step 8: Executar teste**

```bash
cd frontend && npm run test:run -- __tests__/OnboardingStepTenant.test.tsx
```

Expected: PASS — 2 testes.

- [ ] **Step 9: Commit**

```bash
git add frontend/components/onboarding/ frontend/__tests__/OnboardingStepTenant.test.tsx
git commit -m "feat: add onboarding step components (tenant, theme, service, resource, hours)"
```

---

### Task 4: Onboarding Page

**Files:**
- Create: `frontend/app/(admin)/admin/onboarding/page.tsx`

- [ ] **Step 1: Criar `frontend/app/(admin)/admin/onboarding/page.tsx`**

```typescript
// frontend/app/(admin)/admin/onboarding/page.tsx
'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { onboardingApi } from '@/lib/api/onboarding'
import type { OnboardingTenantData, OnboardingThemeData, OnboardingServiceData, OnboardingResourceData, OnboardingHoursData } from '@/lib/api/onboarding'
import { OnboardingStepTenant } from '@/components/onboarding/OnboardingStepTenant'
import { OnboardingStepTheme } from '@/components/onboarding/OnboardingStepTheme'
import { OnboardingStepService } from '@/components/onboarding/OnboardingStepService'
import { OnboardingStepResource } from '@/components/onboarding/OnboardingStepResource'
import { OnboardingStepHours } from '@/components/onboarding/OnboardingStepHours'
import { cn } from '@/lib/utils'

const STEP_LABELS = [
  'Negócio',
  'Visual',
  'Serviço',
  'Recurso',
  'Horários',
]

export default function OnboardingPage() {
  const router = useRouter()
  const [step, setStep] = useState(0)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Saved data from each step
  const [tenantData, setTenantData] = useState<OnboardingTenantData | null>(null)
  const [themeData, setThemeData] = useState<OnboardingThemeData | null>(null)
  const [serviceData, setServiceData] = useState<OnboardingServiceData | null>(null)
  const [resourceData, setResourceData] = useState<OnboardingResourceData | null>(null)

  const handleTenantNext = async (data: OnboardingTenantData) => {
    setLoading(true)
    setError(null)
    try {
      await onboardingApi.updateTenant(data)
      setTenantData(data)
      setStep(1)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao salvar')
    } finally {
      setLoading(false)
    }
  }

  const handleThemeNext = async (data: OnboardingThemeData) => {
    setLoading(true)
    setError(null)
    try {
      await onboardingApi.updateTheme(data)
      setThemeData(data)
      setStep(2)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao salvar')
    } finally {
      setLoading(false)
    }
  }

  const handleServiceNext = async (data: OnboardingServiceData) => {
    setLoading(true)
    setError(null)
    try {
      await onboardingApi.createService(data)
      setServiceData(data)
      setStep(3)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao salvar')
    } finally {
      setLoading(false)
    }
  }

  const handleResourceNext = async (data: OnboardingResourceData) => {
    setLoading(true)
    setError(null)
    try {
      await onboardingApi.createResource(data)
      setResourceData(data)
      setStep(4)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao salvar')
    } finally {
      setLoading(false)
    }
  }

  const handleHoursFinish = async (data: OnboardingHoursData) => {
    setLoading(true)
    setError(null)
    try {
      await Promise.all(
        data.schedule.map(d =>
          onboardingApi.setBusinessHours(d.dayOfWeek, d.isOpen, d.openTime, d.closeTime)
        )
      )
      router.push('/admin/dashboard')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao salvar')
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-slate-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-sm border w-full max-w-lg p-8">
        {/* Header */}
        <div className="text-center mb-8">
          <h1 className="text-2xl font-bold text-slate-900">Configurar seu negócio</h1>
          <p className="text-sm text-slate-500 mt-1">Passo {step + 1} de {STEP_LABELS.length}</p>
        </div>

        {/* Step indicators */}
        <div className="flex justify-center gap-2 mb-8">
          {STEP_LABELS.map((label, i) => (
            <div key={label} className="flex flex-col items-center gap-1">
              <div className={cn(
                'h-8 w-8 rounded-full flex items-center justify-center text-xs font-bold',
                i < step ? 'bg-indigo-600 text-white' :
                i === step ? 'bg-indigo-100 text-indigo-700 border-2 border-indigo-600' :
                'bg-slate-100 text-slate-400'
              )}>
                {i < step ? '✓' : i + 1}
              </div>
              <span className="text-[10px] text-slate-400 hidden sm:block">{label}</span>
            </div>
          ))}
        </div>

        {/* Error */}
        {error && (
          <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-lg text-sm text-red-700">
            {error}
          </div>
        )}

        {/* Steps */}
        {step === 0 && (
          <OnboardingStepTenant initial={tenantData ?? undefined} onNext={handleTenantNext} />
        )}
        {step === 1 && (
          <OnboardingStepTheme onNext={handleThemeNext} onBack={() => setStep(0)} />
        )}
        {step === 2 && (
          <OnboardingStepService onNext={handleServiceNext} onBack={() => setStep(1)} />
        )}
        {step === 3 && (
          <OnboardingStepResource onNext={handleResourceNext} onBack={() => setStep(2)} />
        )}
        {step === 4 && (
          <OnboardingStepHours onFinish={handleHoursFinish} onBack={() => setStep(3)} loading={loading} />
        )}
      </div>
    </div>
  )
}
```

- [ ] **Step 2: Adicionar link de onboarding no Sidebar**

Ler `frontend/components/admin/Sidebar.tsx`. Adicionar item ao array `NAV`:

```typescript
{ href: '/admin/onboarding', label: 'Onboarding', icon: Rocket },
```

Importar `Rocket` de `lucide-react`. Adicionar antes de `Settings`.

- [ ] **Step 3: Commit**

```bash
git add frontend/app/(admin)/admin/onboarding/ frontend/components/admin/Sidebar.tsx
git commit -m "feat: add 5-step tenant onboarding wizard"
```

---

### Task 5: ReviewForm no Portal

**Files:**
- Create: `frontend/components/portal/ReviewForm.tsx`
- Modify: `frontend/app/(portal)/[slug]/minha-conta/page.tsx`
- Create: `frontend/__tests__/ReviewForm.test.tsx`

- [ ] **Step 1: Escrever teste do ReviewForm**

```typescript
// frontend/__tests__/ReviewForm.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { vi } from 'vitest'
import { ReviewForm } from '@/components/portal/ReviewForm'

describe('ReviewForm', () => {
  it('renders star rating buttons', () => {
    render(<ReviewForm bookingId="b1" onSubmit={vi.fn()} onCancel={vi.fn()} />)
    const stars = screen.getAllByRole('button', { name: /estrela/i })
    expect(stars).toHaveLength(5)
  })

  it('calls onSubmit with stars and comment', async () => {
    const onSubmit = vi.fn()
    render(<ReviewForm bookingId="b1" onSubmit={onSubmit} onCancel={vi.fn()} />)
    // Click 4th star
    const stars = screen.getAllByRole('button', { name: /estrela/i })
    fireEvent.click(stars[3])
    fireEvent.click(screen.getByRole('button', { name: /enviar avaliação/i }))
    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith({ bookingId: 'b1', stars: 4, comment: undefined })
    })
  })
})
```

- [ ] **Step 2: Executar teste para verificar falha**

```bash
cd frontend && npm run test:run -- __tests__/ReviewForm.test.tsx
```

Expected: FAIL.

- [ ] **Step 3: Criar `frontend/components/portal/ReviewForm.tsx`**

```typescript
// frontend/components/portal/ReviewForm.tsx
'use client'

import { useState } from 'react'
import { Star } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

interface ReviewSubmitData {
  bookingId: string
  stars: number
  comment?: string
}

interface Props {
  bookingId: string
  onSubmit: (data: ReviewSubmitData) => void
  onCancel: () => void
}

export function ReviewForm({ bookingId, onSubmit, onCancel }: Props) {
  const [stars, setStars] = useState(0)
  const [hovered, setHovered] = useState(0)
  const [comment, setComment] = useState('')

  const handleSubmit = () => {
    onSubmit({
      bookingId,
      stars,
      comment: comment.trim() || undefined,
    })
  }

  return (
    <div className="space-y-4 p-4 border rounded-lg bg-slate-50">
      <p className="font-medium text-sm">Como foi sua experiência?</p>

      {/* Star rating */}
      <div className="flex gap-1">
        {[1, 2, 3, 4, 5].map(s => (
          <button
            key={s}
            type="button"
            aria-label={`${s} estrela${s > 1 ? 's' : ''}`}
            onClick={() => setStars(s)}
            onMouseEnter={() => setHovered(s)}
            onMouseLeave={() => setHovered(0)}
            className="focus:outline-none"
          >
            <Star
              className={cn(
                'h-8 w-8 transition-colors',
                s <= (hovered || stars)
                  ? 'fill-amber-400 text-amber-400'
                  : 'text-slate-300'
              )}
            />
          </button>
        ))}
      </div>

      {/* Comment */}
      <textarea
        value={comment}
        onChange={e => setComment(e.target.value)}
        placeholder="Conte como foi... (opcional)"
        className="w-full border rounded-lg px-3 py-2 text-sm min-h-[70px]"
      />

      <div className="flex gap-2">
        <Button variant="outline" size="sm" onClick={onCancel}>Cancelar</Button>
        <Button size="sm" onClick={handleSubmit} disabled={stars === 0}>
          Enviar avaliação
        </Button>
      </div>
    </div>
  )
}
```

- [ ] **Step 4: Atualizar `frontend/app/(portal)/[slug]/minha-conta/page.tsx`**

Ler o arquivo atual. Fazer as seguintes alterações:

**a)** Adicionar import do `ReviewForm` e `portalApi`:

```typescript
import { ReviewForm } from '@/components/portal/ReviewForm'
```

**b)** Adicionar estado para controlar qual booking está sendo avaliado:

```typescript
const [reviewingBookingId, setReviewingBookingId] = useState<string | null>(null)
```

**c)** Adicionar função de submit de review:

```typescript
const handleReviewSubmit = async (data: { bookingId: string; stars: number; comment?: string }) => {
  if (!accessToken) return
  try {
    await portalApi.createReview(slug, accessToken, data.bookingId, data.stars, data.comment)
    setReviewingBookingId(null)
  } catch {
    alert('Erro ao enviar avaliação.')
  }
}
```

**d)** Nos cards de bookings passados (`past.map`), adicionar botão "Avaliar" para bookings com status `Completed` e sem avaliação pendente, e exibir o `ReviewForm` quando selecionado:

```typescript
// Dentro do past.map, após o Badge:
{b.status === 'Completed' && (
  reviewingBookingId === b.id ? (
    <ReviewForm
      bookingId={b.id}
      onSubmit={handleReviewSubmit}
      onCancel={() => setReviewingBookingId(null)}
    />
  ) : (
    <Button size="sm" variant="outline" onClick={() => setReviewingBookingId(b.id)}>
      Avaliar
    </Button>
  )
)}
```

- [ ] **Step 5: Executar testes**

```bash
cd frontend && npm run test:run -- __tests__/ReviewForm.test.tsx
```

Expected: PASS — 2 testes.

- [ ] **Step 6: Commit**

```bash
git add frontend/components/portal/ReviewForm.tsx frontend/app/(portal)/[slug]/minha-conta/page.tsx frontend/__tests__/ReviewForm.test.tsx
git commit -m "feat: add review form in minha-conta for completed bookings"
```

---

### Task 6: Upsell na Página de Detalhe do Serviço

**Files:**
- Modify: `frontend/app/(portal)/[slug]/servicos/[id]/page.tsx`

- [ ] **Step 1: Ler o arquivo atual**

Ler `frontend/app/(portal)/[slug]/servicos/[id]/page.tsx` para ver o estado atual.

- [ ] **Step 2: Adicionar seção de upsell**

O backend tem `Service.Category` (string opcional). A estratégia de upsell é: mostrar outros serviços ativos da mesma categoria. Se o serviço não tiver categoria, mostrar os 3 primeiros serviços ativos do catálogo (excluindo o atual).

Adicionar após a seção de avaliações:

```typescript
{/* Upsell — Serviços relacionados */}
{(() => {
  const related = services
    .filter(s => s.isActive && s.id !== id)
    .filter(s => service.categoryId
      ? s.categoryId === service.categoryId
      : true
    )
    .slice(0, 3)

  if (related.length === 0) return null

  return (
    <section className="mt-12">
      <h2 className="text-xl font-bold mb-4">Você também pode gostar</h2>
      <div className="grid gap-4 sm:grid-cols-3">
        {related.map(s => (
          <ServiceCard key={s.id} service={s} slug={slug} />
        ))}
      </div>
    </section>
  )
})()}
```

Importar `ServiceCard` se ainda não estiver importado:
```typescript
import { ServiceCard } from '@/components/portal/ServiceCard'
```

- [ ] **Step 3: Commit**

```bash
git add frontend/app/(portal)/[slug]/servicos/[id]/page.tsx
git commit -m "feat: add upsell section on service detail page"
```

---

### Task 7: Full Test Suite + Build

**Files:** nenhum novo

- [ ] **Step 1: Executar toda a suíte**

```bash
cd frontend
npm run test:run
```

Expected: PASS — todos os testes (15+ arquivos, 30+ testes).

- [ ] **Step 2: Build de produção**

```bash
npm run build
```

Fix de erros comuns:
- `@ducanh2912/next-pwa` pode precisar de `webpack` como peer dep — já instalado no Task 1
- `withPWA` wrapper pode exigir tipagem específica — se houver erro de tipo, adicione `as any` temporariamente
- Ícones PNG placeholder podem causar warning de size — aceitável

- [ ] **Step 3: Commit de integração**

```bash
cd ..
git add frontend/
git commit -m "feat: complete Sprint 11 — PWA, onboarding wizard, reviews, upsell"
```

(Apenas se houver arquivos com alterações não commitadas.)

---

## Self-Review

### Spec Coverage

| Requisito | Task | Status |
|-----------|------|--------|
| PWA — next-pwa, manifest, service worker | Task 1 | ✅ |
| Wizard de onboarding 5 passos | Tasks 3-4 | ✅ |
| Avaliações no portal (clientes) | Task 5 | ✅ |
| Upsell (serviços complementares) | Task 6 | ✅ |
| Wallet de créditos/vouchers | — | ⏭ Sprint 12 (complexidade backend) |

### Placeholder Scan

Sem TBDs. Todo código é concreto.

### Type Consistency

- `OnboardingTenantData`, `OnboardingThemeData`, `OnboardingServiceData`, `OnboardingResourceData`, `OnboardingHoursData` — definidos em `lib/api/onboarding.ts`, usados nos 5 step components e na página
- `ReviewForm` props: `{ bookingId: string; stars: number; comment?: string }` — consistente entre componente, teste e uso em `minha-conta`
- `portalApi.createReview` aceita `(slug, token, bookingId, stars, comment?)` — assinatura definida em Task 1 da Sprint 10
