import { render, screen, waitFor } from '@testing-library/react'
import { vi } from 'vitest'
import DashboardPage from '@/app/(admin)/admin/dashboard/page'

vi.mock('@/lib/api/bookings', () => ({
  bookingsApi: { list: vi.fn().mockResolvedValue([
    { id: '1', status: 'Confirmed', scheduledAt: new Date().toISOString(), totalAmount: 150 },
    { id: '2', status: 'Cancelled', scheduledAt: new Date().toISOString(), totalAmount: 0 },
  ]) },
}))

vi.mock('@/lib/api/financeiro', () => ({
  financeiroApi: { summary: vi.fn().mockResolvedValue({
    totalRevenue: 1500,
    netRevenue: 1200,
    totalBookings: 10,
    paidBookings: 8,
    totalRefunds: 300,
    pendingAmount: 0,
  }) },
}))

describe('DashboardPage', () => {
  it('renders metric cards', async () => {
    render(<DashboardPage />)
    await waitFor(() => {
      expect(screen.getByText(/agendamentos hoje/i)).toBeInTheDocument()
      expect(screen.getByText(/receita/i)).toBeInTheDocument()
    })
  })
})
