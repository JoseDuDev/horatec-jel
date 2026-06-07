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
  startsAt: string
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

export interface CreatePaymentPortalRequest {
  bookingId: string
  amount: number
  method: 'Pix' | 'CreditCard' | 'DebitCard' | 'Boleto'
  backUrl: string
  voucherCode?: string
  useWalletCredits?: boolean
}

export interface PaymentPortalResult {
  paymentId: string
  preferenceId: string
  paymentUrl?: string
}
