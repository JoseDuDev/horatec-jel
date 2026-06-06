import { render, screen } from '@testing-library/react'
import { vi } from 'vitest'
import { BookingCalendar } from '@/components/bookings/BookingCalendar'
import type { Booking } from '@/lib/types/booking'

const bookings: Booking[] = [
  {
    id: '1',
    customerId: 'c1',
    customerName: 'Ana Lima',
    customerEmail: 'ana@test.com',
    resourceId: 'r1',
    resourceName: 'Sala A',
    serviceId: 's1',
    serviceName: 'Corte',
    scheduledAt: new Date().toISOString(),
    durationMinutes: 60,
    status: 'Confirmed',
    totalAmount: 100,
    createdAt: new Date().toISOString(),
  },
]

describe('BookingCalendar', () => {
  it('renders booking in the calendar', () => {
    render(<BookingCalendar bookings={bookings} onMove={vi.fn()} />)
    expect(screen.getByText('Ana Lima')).toBeInTheDocument()
    expect(screen.getByText('Corte')).toBeInTheDocument()
  })

  it('shows day view by default', () => {
    render(<BookingCalendar bookings={bookings} onMove={vi.fn()} />)
    expect(screen.getByRole('button', { name: /dia/i })).toBeInTheDocument()
  })
})
