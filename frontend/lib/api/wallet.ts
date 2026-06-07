import type {
  WalletResult,
  VoucherSummary,
  VoucherValidationResult,
  CreateVoucherRequest,
} from '../types/wallet'

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

async function apiFetch<T>(
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

// Admin-scoped wallet operations (uses admin JWT)
export const walletApi = {
  getVouchers: (token: string) =>
    apiFetch<VoucherSummary[]>('/api/v1/vouchers', token),

  createVoucher: (token: string, data: CreateVoucherRequest) =>
    apiFetch<string>('/api/v1/vouchers', token, {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  deactivateVoucher: (token: string, id: string) =>
    apiFetch<void>(`/api/v1/vouchers/${id}`, token, { method: 'DELETE' }),

  addCredits: (token: string, userId: string, amount: number, description: string) =>
    apiFetch<void>(`/api/v1/wallet/users/${userId}/credits`, token, {
      method: 'POST',
      body: JSON.stringify({ amount, description }),
    }),
}

// Portal-scoped wallet operations (uses customer JWT)
export const portalWalletApi = {
  getWallet: (token: string) =>
    apiFetch<WalletResult>('/api/v1/wallet', token),

  validateVoucher: (token: string, code: string, totalPrice: number) =>
    apiFetch<VoucherValidationResult>(
      `/api/v1/vouchers/validate?code=${encodeURIComponent(code)}&totalPrice=${totalPrice}`,
      token
    ),
}
