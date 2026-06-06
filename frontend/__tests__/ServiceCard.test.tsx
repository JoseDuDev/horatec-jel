import { render, screen } from '@testing-library/react'
import { ServiceCard } from '@/components/portal/ServiceCard'
import type { Service } from '@/lib/types/service'

const mockService: Service = {
  id: 's1',
  name: 'Corte Masculino',
  description: 'Corte clássico com navalha',
  durationMinutes: 30,
  price: 45,
  isActive: true,
}

describe('ServiceCard', () => {
  it('renders service name, price and duration', () => {
    render(<ServiceCard service={mockService} slug="joao-barber" />)
    expect(screen.getByText('Corte Masculino')).toBeInTheDocument()
    expect(screen.getByText(/R\$ 45/)).toBeInTheDocument()
    expect(screen.getByText(/30 min/)).toBeInTheDocument()
  })

  it('renders agendar link', () => {
    render(<ServiceCard service={mockService} slug="joao-barber" />)
    const link = screen.getByRole('link', { name: /agendar/i })
    expect(link).toHaveAttribute('href', '/joao-barber/agendar?serviceId=s1')
  })
})
