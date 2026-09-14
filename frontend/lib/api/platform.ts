import { parseJsonBody } from './json'
import type { TenantSummary, TenantPlan, TenantVertical, PlanConfig } from '../types/platform'

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

  return parseJsonBody<T>(res)
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

export interface CreateTenantBody {
  name: string
  slug: string
  vertical: TenantVertical
  email?: string
  phone?: string
  city?: string
  state?: string
  ownerName: string
  ownerEmail: string
  ownerPassword: string
  capabilities: string
  plan: TenantPlan
}

export interface CreateTenantResult {
  tenantId: string
  slug: string
  tokens: { accessToken: string; refreshToken: string; expiresAt: string }
}

export const platformApi = {
  tenants: (token: string) =>
    platformFetch<TenantSummary[]>('/api/v1/platform/tenants', token),

  // Onboarding completo (cria o tenant + o usuário TenantOwner) — endpoint já
  // existente (usado hoje pelo self-signup público), agora exposto pelo painel.
  createTenant: (token: string, body: CreateTenantBody) =>
    platformFetch<CreateTenantResult>('/api/v1/platform/tenants', token, {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  // Cria um novo PlatformAdmin (superadmin) — evita que o admin seedado via
  // env var vire ponto único de falha.
  createAdmin: (token: string, body: { email: string; password: string; name: string }) =>
    platformFetch<{ id: string; email: string; name: string }>('/api/v1/platform/admins', token, {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  suspendTenant: (token: string, id: string, reason: string) =>
    platformFetch<void>(`/api/v1/platform/tenants/${id}/suspend`, token, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),

  activateTenant: (token: string, id: string) =>
    platformFetch<void>(`/api/v1/platform/tenants/${id}/activate`, token, {
      method: 'POST',
    }),

  // Define o pacote contratado do tenant (capacidades + plano).
  updateTenantPlan: (
    token: string,
    id: string,
    body: { capabilities: string; plan: TenantPlan },
  ) =>
    platformFetch<void>(`/api/v1/platform/tenants/${id}/plan`, token, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),

  // Planos e seus limites (editáveis pela plataforma).
  plans: (token: string) =>
    platformFetch<PlanConfig[]>('/api/v1/platform/plans', token),

  updatePlan: (
    token: string,
    plan: string,
    body: { maxServices: number; maxResources: number; maxRentableItems: number },
  ) =>
    platformFetch<void>(`/api/v1/platform/plans/${plan}`, token, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),
}
