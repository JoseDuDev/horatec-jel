// Server components run inside Docker — use internal hostname.
// Client components run in the browser — use the public-facing URL.
function getApiUrl(): string {
  if (typeof window === 'undefined') {
    return process.env.INTERNAL_API_URL ?? process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'
  }
  return process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'
}

// Deduplica renovações concorrentes: várias chamadas 401 ao mesmo tempo devem
// esperar UMA renovação, não disparar uma pra cada.
let refreshPromise: Promise<string | null> | null = null

// O refresh token do cliente final tem validade longa (ver
// CustomerRefreshTokenExpirationDays no backend) — na prática a sessão nunca
// deve expirar enquanto o navegador guardar esse token.
async function refreshCustomerToken(): Promise<string | null> {
  if (typeof window === 'undefined') return null

  // Import dinâmico evita ciclo com store/portal-auth (que não depende deste módulo hoje,
  // mas mantém portalFetch livre de acoplamento no topo do arquivo).
  const { usePortalAuthStore } = await import('@/store/portal-auth')
  const { refreshToken, customer } = usePortalAuthStore.getState()
  if (!refreshToken || !customer) return null

  try {
    const res = await fetch(`${getApiUrl()}/api/v1/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    })
    if (!res.ok) return null

    const tokens = (await res.json()) as { accessToken: string; refreshToken: string }
    usePortalAuthStore.getState().setCustomerAuth(customer, tokens.accessToken, tokens.refreshToken)
    document.cookie = `portal_access_token=${tokens.accessToken}; path=/; max-age=${60 * 60 * 24 * 365}`
    return tokens.accessToken
  } catch {
    return null
  }
}

async function portalFetch<T>(
  path: string,
  tenantSlug: string,
  options: RequestInit = {},
  customerToken?: string,
  isRetry = false
): Promise<T> {
  const res = await fetch(`${getApiUrl()}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      'X-Tenant-Slug': tenantSlug,
      ...(customerToken ? { Authorization: `Bearer ${customerToken}` } : {}),
      ...options.headers,
    },
  })

  if (res.status === 401 && customerToken && !isRetry) {
    refreshPromise ??= refreshCustomerToken().finally(() => { refreshPromise = null })
    const newToken = await refreshPromise
    if (newToken) return portalFetch<T>(path, tenantSlug, options, newToken, true)
  }

  if (!res.ok) {
    const error = await res.json().catch(() => ({ title: res.statusText }))
    throw new Error(error.title ?? `HTTP ${res.status}`)
  }

  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
}

import type {
  TenantPublicInfo, CustomerProfile, CustomerBooking,
  PortalReview, FavoriteService, CreateBookingRequest, BookingCreatedResult,
  CreatePaymentPortalRequest, PaymentPortalResult,
} from '../types/portal'
import type { Service } from '../types/service'
import type { Resource } from '../types/resource'
import type {
  RentableItem, RentalAvailability, CreateRentalBookingRequest,
} from '../types/rental'

export const portalApi = {
  // Endpoint público (AllowAnonymous) do tenant pelo slug — usado pela landing/portal.
  // (GET /tenants/me exige auth de admin; aqui precisamos de dados anônimos + capabilities.)
  tenant: async (slug: string): Promise<TenantPublicInfo> => {
    const t = await portalFetch<{
      id: string
      name: string
      slug: string
      timeZoneId: string
      capabilities: string
      theme?: { primaryColor?: string; logoUrl?: string }
    }>(`/api/v1/platform/tenants/${slug}`, slug)
    return {
      id: t.id,
      name: t.name,
      slug: t.slug,
      logoUrl: t.theme?.logoUrl,
      primaryColor: t.theme?.primaryColor,
      timezone: t.timeZoneId,
      capabilities: t.capabilities,
    }
  },

  services: (slug: string) =>
    portalFetch<Service[]>('/api/v1/services', slug),

  resources: (slug: string) =>
    portalFetch<Resource[]>('/api/v1/resources', slug),

  slots: (slug: string, resourceId: string, date: string, serviceId?: string) => {
    const qs = new URLSearchParams({ date, ...(serviceId ? { serviceId } : {}) }).toString()
    return portalFetch<string[]>(`/api/v1/availability/resources/${resourceId}/slots?${qs}`, slug)
  },

  reviews: async (slug: string, resourceId: string) => {
    const result = await portalFetch<{ page: { items: PortalReview[] } }>(
      `/api/v1/reviews/resources/${resourceId}`, slug
    )
    return result.page.items
  },

  profile: (slug: string, token: string) =>
    portalFetch<CustomerProfile>('/api/v1/customers/me', slug, {}, token),

  myBookings: (slug: string, token: string) =>
    portalFetch<CustomerBooking[]>('/api/v1/customers/me/bookings', slug, {}, token),

  // O endpoint POST /bookings retorna o Guid cru (ex.: "550e8400-..."),
  // não um objeto. Envelopamos em { id } para o wizard usar booking.id.
  createBooking: async (slug: string, token: string, data: CreateBookingRequest): Promise<BookingCreatedResult> => {
    const id = await portalFetch<string>('/api/v1/bookings', slug, {
      method: 'POST',
      body: JSON.stringify(data),
    }, token)
    return { id, scheduledAt: data.scheduledAt, status: 'Pending' }
  },

  createPayment: (slug: string, token: string, data: CreatePaymentPortalRequest) =>
    portalFetch<PaymentPortalResult>('/api/v1/payments', slug, {
      method: 'POST',
      body: JSON.stringify(data),
    }, token),

  cancelBooking: (slug: string, token: string, bookingId: string, reason?: string) =>
    portalFetch<void>(`/api/v1/bookings/${bookingId}/cancel`, slug, {
      method: 'POST',
      body: JSON.stringify({ reason: reason ?? null }),
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

  // ── Locação ─────────────────────────────────────────────────────────────────
  rentalItems: (slug: string) =>
    portalFetch<RentableItem[]>('/api/v1/rentals/items', slug),

  rentalAvailability: (slug: string, itemId: string, startDate: string, endDate: string) => {
    const qs = new URLSearchParams({ startDate, endDate }).toString()
    return portalFetch<RentalAvailability>(`/api/v1/rentals/items/${itemId}/availability?${qs}`, slug)
  },

  // POST /rentals/bookings retorna o Guid cru; envelopamos em { id }.
  createRentalBooking: async (
    slug: string, token: string, data: CreateRentalBookingRequest
  ): Promise<BookingCreatedResult> => {
    const id = await portalFetch<string>('/api/v1/rentals/bookings', slug, {
      method: 'POST',
      body: JSON.stringify(data),
    }, token)
    return { id, scheduledAt: data.startDate, status: 'Pending' }
  },

  loginWithGoogle: (slug: string, idToken: string) =>
    portalFetch<{ accessToken: string; refreshToken: string; expiresAt: string }>(
      '/api/v1/customers/auth/google', slug, {
        method: 'POST',
        body: JSON.stringify({ idToken, tenantSlug: slug }),
      }
    ),
}
