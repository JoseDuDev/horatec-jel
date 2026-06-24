import type { TenantSummary, TenantPlan } from '../types/platform'

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
}
