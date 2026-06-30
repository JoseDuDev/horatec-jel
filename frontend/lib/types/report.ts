/** Item agregado da carteira de clientes (GET /reports/customers). */
export interface CustomerReportItem {
  customerId: string
  name: string
  email: string
  phone?: string
  bookingCount: number
  lastBookingAt?: string
  totalSpent: number
}
