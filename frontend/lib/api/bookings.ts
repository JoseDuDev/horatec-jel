import { apiFetch } from './client'
import type { Booking, BookingStatus, AdminCreateBookingRequest } from '../types/booking'

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

  // Locação: retirada e devolução (a devolução retorna multa/estorno).
  rentalPickup: (id: string) =>
    apiFetch<void>(`/api/v1/rentals/bookings/${id}/pickup`, { method: 'POST' }),

  // refundToGateway: estorna a caução no meio de pagamento original (cartão/PIX) em vez
  // da carteira. Com fallback para a carteira no backend se o gateway falhar.
  rentalReturn: (id: string, refundToGateway = false) =>
    apiFetch<{
      bookingId: string
      lateDays: number
      lateFee: number
      depositRefunded: number
      destination: 'None' | 'Wallet' | 'Gateway'
    }>(
      `/api/v1/rentals/bookings/${id}/return${refundToGateway ? '?refundToGateway=true' : ''}`,
      { method: 'POST' }),

  adminCreate: (data: AdminCreateBookingRequest) =>
    apiFetch<string>('/api/v1/bookings/admin', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
}
