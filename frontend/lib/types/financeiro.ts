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

export interface FinancialSummary {
  totalRevenue: number
  totalRefunds: number
  netRevenue: number
  totalBookings: number
  paidBookings: number
  pendingAmount: number
}
