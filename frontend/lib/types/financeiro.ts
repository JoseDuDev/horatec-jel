export interface FinancialTransaction {
  id: string
  bookingId: string
  amount: number
  type: 'Payment' | 'Refund'
  status: 'Pending' | 'Paid' | 'Refunded' | 'Failed'
  createdAt: string
  serviceName: string
  customerName: string
}

export interface DailySummary {
  date: string
  revenue: number
  count: number
}

export interface FinancialSummary {
  totalRevenue: number
  totalRefunded: number
  netRevenue: number
  byDay: DailySummary[]
  // Não retornados pela API de summary atual — usados apenas pelo dashboard com fallback (?? 0).
  paidBookings?: number
  totalBookings?: number
}

export interface RentalFinancialSummary {
  rentalCount: number
  rentalRevenue: number
  lateFeesCollected: number
  depositsCharged: number
  depositsRefunded: number
  depositsHeld: number
  netRevenue: number
}
