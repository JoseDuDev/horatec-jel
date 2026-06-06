export type BookingStatus = 'Pending' | 'Confirmed' | 'Completed' | 'Cancelled' | 'NoShow'

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
}
