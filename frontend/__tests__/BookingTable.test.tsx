import { render, screen, fireEvent } from '@testing-library/react'
import { vi } from 'vitest'
import { BookingTable } from '@/components/bookings/BookingTable'
import type { Booking } from '@/lib/types/booking'

const mockBooking: Booking = {
  id: '123',
  customerId: 'c1',
  customerName: 'João Silva',
  customerEmail: 'joao@test.com',
  resourceId: 'r1',
  resourceName: 'Sala A',
  serviceId: 's1',
  serviceName: 'Corte',
  scheduledAt: '2026-06-10T10:00:00',
  durationMinutes: 60,
  status: 'Pending',
  totalAmount: 100,
  createdAt: '2026-06-01T00:00:00',
}

describe('BookingTable', () => {
  it('renders booking data', () => {
    render(<BookingTable bookings={[mockBooking]} onAction={vi.fn()} />)
    expect(screen.getByText('João Silva')).toBeInTheDocument()
    expect(screen.getByText('Corte')).toBeInTheDocument()
    expect(screen.getByText('Sala A')).toBeInTheDocument()
  })

  it('calls onAction with confirm when button clicked', async () => {
    const onAction = vi.fn()
    render(<BookingTable bookings={[mockBooking]} onAction={onAction} />)
    fireEvent.click(screen.getByRole('button', { name: /confirmar/i }))
    expect(onAction).toHaveBeenCalledWith('confirm', '123')
  })
})
