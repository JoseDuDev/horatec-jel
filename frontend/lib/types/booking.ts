export type BookingStatus = 'Pending' | 'Confirmed' | 'Completed' | 'Cancelled' | 'NoShow'

export type BookingKind = 'Appointment' | 'Rental'

export type RentalStatus = 'Reserved' | 'PickedUp' | 'Returned'

export interface Booking {
  id: string
  customerId: string
  customerName: string
  customerEmail: string
  customerPhone?: string
  resourceId: string
  resourceName: string
  serviceId: string
  serviceName: string
  scheduledAt: string
  durationMinutes: number
  status: BookingStatus
  totalAmount: number
  createdAt: string
  kind?: BookingKind
  rentalStatus?: RentalStatus | null
}

export interface AdminCreateBookingRequest {
  serviceIds: string[]
  resourceId: string
  scheduledAt: string   // ISO 8601 — ex: "2026-06-10T14:00:00Z"
  customerName: string
  customerEmail?: string
  customerPhone?: string
  notes?: string
}
