import { render, screen, fireEvent } from '@testing-library/react'
import { vi } from 'vitest'
import { WizardStepSlot } from '@/components/portal/WizardStepSlot'

const slots = [
  '2026-06-10T09:00:00Z',
  '2026-06-10T10:00:00Z',
  '2026-06-10T11:00:00Z',
]

describe('WizardStepSlot', () => {
  it('renders available slots', () => {
    render(
      <WizardStepSlot
        slots={slots}
        loadingSlots={false}
        selectedDate={new Date('2026-06-10')}
        selectedSlot={null}
        onDateChange={vi.fn()}
        onSlotSelect={vi.fn()}
      />
    )
    expect(screen.getByText('09:00')).toBeInTheDocument()
    expect(screen.getByText('10:00')).toBeInTheDocument()
    expect(screen.getByText('11:00')).toBeInTheDocument()
  })

  it('calls onSlotSelect when a slot is clicked', () => {
    const onSlotSelect = vi.fn()
    render(
      <WizardStepSlot
        slots={slots}
        loadingSlots={false}
        selectedDate={new Date('2026-06-10')}
        selectedSlot={null}
        onDateChange={vi.fn()}
        onSlotSelect={onSlotSelect}
      />
    )
    fireEvent.click(screen.getByText('09:00'))
    expect(onSlotSelect).toHaveBeenCalledWith('2026-06-10T09:00:00Z')
  })
})
