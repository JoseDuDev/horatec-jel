import { render, screen, fireEvent } from '@testing-library/react'
import { vi } from 'vitest'
import { WizardStepService } from '@/components/portal/WizardStepService'
import type { Service } from '@/lib/types/service'

const services: Service[] = [
  { id: 's1', name: 'Corte', durationMinutes: 30, price: 45, isActive: true },
  { id: 's2', name: 'Barba', durationMinutes: 20, price: 30, isActive: true },
]

describe('WizardStepService', () => {
  it('renders all services', () => {
    render(<WizardStepService services={services} selectedId={null} onSelect={vi.fn()} />)
    expect(screen.getByText('Corte')).toBeInTheDocument()
    expect(screen.getByText('Barba')).toBeInTheDocument()
  })

  it('calls onSelect when service is clicked', () => {
    const onSelect = vi.fn()
    render(<WizardStepService services={services} selectedId={null} onSelect={onSelect} />)
    fireEvent.click(screen.getByText('Corte'))
    expect(onSelect).toHaveBeenCalledWith('s1')
  })

  it('highlights selected service', () => {
    render(<WizardStepService services={services} selectedId="s1" onSelect={vi.fn()} />)
    // The selected button should have the indigo ring class
    const buttons = screen.getAllByRole('button')
    const corteButton = buttons.find(b => b.textContent?.includes('Corte'))
    expect(corteButton).toHaveClass('border-indigo-600')
  })
})
