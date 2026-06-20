import type {
  WalletResult,
  VoucherSummary,
  VoucherValidationResult,
  CreateVoucherRequest,
} from '../types/wallet'

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

// O tenant é resolvido pelo header X-Tenant-Slug (TenantMiddleware) — sem ele a
// requisição cai em 404, então TODA chamada precisa enviar o slug.
async function apiFetch<T>(
  path: string,
  token: string,
  slug: string,
  options: RequestInit = {}
): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
      'X-Tenant-Slug': slug,
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

// Admin-scoped wallet operations (uses admin JWT)
export const walletApi = {
  getVouchers: (slug: string, token: string) =>
    apiFetch<VoucherSummary[]>('/api/v1/vouchers', token, slug),

  createVoucher: (slug: string, token: string, data: CreateVoucherRequest) =>
    apiFetch<string>('/api/v1/vouchers', token, slug, {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  deactivateVoucher: (slug: string, token: string, id: string) =>
    apiFetch<void>(`/api/v1/vouchers/${id}`, token, slug, { method: 'DELETE' }),

  addCredits: (slug: string, token: string, userId: string, amount: number, description: string) =>
    apiFetch<void>(`/api/v1/wallet/users/${userId}/credits`, token, slug, {
      method: 'POST',
      body: JSON.stringify({ amount, description }),
    }),
}

// Portal-scoped wallet operations (uses customer JWT)
export const portalWalletApi = {
  getWallet: (slug: string, token: string) =>
    apiFetch<WalletResult>('/api/v1/wallet', token, slug),

  validateVoucher: (slug: string, token: string, code: string, totalPrice: number) =>
    apiFetch<VoucherValidationResult>(
      `/api/v1/vouchers/validate?code=${encodeURIComponent(code)}&totalPrice=${totalPrice}`,
      token, slug
    ),
}
