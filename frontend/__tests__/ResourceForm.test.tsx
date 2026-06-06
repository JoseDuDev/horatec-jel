import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { ResourceForm } from '@/components/resources/ResourceForm'
import type { Service } from '@/lib/types/service'

const mockServices: Service[] = [
  { id: 's1', name: 'Corte', durationMinutes: 30, price: 50, isActive: true },
]

describe('ResourceForm', () => {
  it('shows validation error when name is empty', async () => {
    render(<ResourceForm services={mockServices} onSubmit={vi.fn()} onCancel={vi.fn()} />)
    fireEvent.click(screen.getByRole('button', { name: /salvar/i }))
    await waitFor(() => {
      expect(screen.getByText(/nome obrigatório/i)).toBeInTheDocument()
    })
  })

  it('calls onSubmit with name and type', async () => {
    const onSubmit = vi.fn()
    render(<ResourceForm services={mockServices} onSubmit={onSubmit} onCancel={vi.fn()} />)
    await userEvent.type(screen.getByLabelText(/nome/i), 'Sala B')
    await userEvent.type(screen.getByLabelText(/tipo/i), 'Sala')
    fireEvent.click(screen.getByRole('button', { name: /salvar/i }))
    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({ name: 'Sala B', type: 'Sala' })
      )
    })
  })
})
