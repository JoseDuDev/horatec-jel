// Server components run inside Docker — use internal hostname.
// Client components run in the browser — use the public-facing URL.
function getApiUrl(): string {
  if (typeof window === 'undefined') {
    return process.env.INTERNAL_API_URL ?? process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'
  }
  return process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'
}

async function portalFetch<T>(
  path: string,
  tenantSlug: string,
  options: RequestInit = {},
  customerToken?: string
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

export const portalApi = {
  tenant: (slug: string) =>
    portalFetch<TenantPublicInfo>('/api/v1/tenants/me', slug),

  services: (slug: string) =>
    portalFetch<Service[]>('/api/v1/services', slug),

  resources: (slug: string) =>
    portalFetch<Resource[]>('/api/v1/resources', slug),

  slots: (slug: string, resourceId: string, date: string, serviceId?: string) => {
    const qs = new URLSearchParams({ date, ...(serviceId ? { serviceId } : {}) }).toString()
    return portalFetch<string[]>(`/api/v1/availability/resources/${resourceId}/slots?${qs}`, slug)
  },

  reviews: (slug: string, resourceId: string) =>
    portalFetch<PortalReview[]>(`/api/v1/reviews/resources/${resourceId}`, slug),

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

  loginWithGoogle: (slug: string, idToken: string) =>
    portalFetch<{ accessToken: string; refreshToken: string; expiresAt: string }>(
      '/api/v1/customers/auth/google', slug, {
        method: 'POST',
        body: JSON.stringify({ idToken, tenantSlug: slug }),
      }
    ),
}
