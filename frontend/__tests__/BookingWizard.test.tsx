import { render, screen } from '@testing-library/react'
import { vi } from 'vitest'
import { BookingWizard } from '@/components/portal/BookingWizard'
import type { Service } from '@/lib/types/service'
import type { Resource } from '@/lib/types/resource'

vi.mock('@/lib/api/portal', () => ({
  portalApi: {
    slots: vi.fn().mockResolvedValue([]),
    createBooking: vi.fn(),
  },
}))

vi.mock('@/store/portal-auth', () => ({
  usePortalAuthStore: () => ({ customer: null, accessToken: null }),
}))

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}))

const services: Service[] = [
  { id: 's1', name: 'Corte', durationMinutes: 30, price: 45, isActive: true },
]
const resources: Resource[] = [
  { id: 'r1', name: 'João', type: 'Professional', serviceIds: ['s1'], isActive: true },
]

describe('BookingWizard', () => {
  it('renders step 1 (service selection) by default', () => {
    render(
      <BookingWizard slug="joao-barber" services={services} resources={resources} />
    )
    expect(screen.getByText(/escolha o serviço/i)).toBeInTheDocument()
  })

  it('shows step indicator', () => {
    render(
      <BookingWizard slug="joao-barber" services={services} resources={resources} />
    )
    expect(screen.getByText('1')).toBeInTheDocument()
  })
})
