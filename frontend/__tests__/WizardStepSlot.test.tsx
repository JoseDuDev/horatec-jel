import { render, screen, fireEvent } from '@testing-library/react'
import { vi } from 'vitest'
import { WizardStepSlot } from '@/components/portal/WizardStepSlot'

const slots = [
  '2026-06-10T09:00:00Z',
  '2026-06-10T10:00:00Z',
  '2026-06-10T11:00:00Z',
]

// Rótulo esperado é a hora LOCAL de quem roda o teste, não a hora em UTC do
// slot — os slots vêm em UTC da API e o componente converte para exibição.
const labelFor = (iso: string) => {
  const d = new Date(iso)
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

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
    expect(screen.getByText(labelFor(slots[0]))).toBeInTheDocument()
    expect(screen.getByText(labelFor(slots[1]))).toBeInTheDocument()
    expect(screen.getByText(labelFor(slots[2]))).toBeInTheDocument()
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
    fireEvent.click(screen.getByText(labelFor(slots[0])))
    expect(onSlotSelect).toHaveBeenCalledWith('2026-06-10T09:00:00Z')
  })
})
