import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { ServiceForm } from '@/components/services/ServiceForm'

describe('ServiceForm', () => {
  it('shows validation errors for empty required fields', async () => {
    render(<ServiceForm onSubmit={vi.fn()} onCancel={vi.fn()} />)
    fireEvent.click(screen.getByRole('button', { name: /salvar/i }))
    await waitFor(() => {
      expect(screen.getByText(/nome obrigatório/i)).toBeInTheDocument()
    })
  })

  it('calls onSubmit with form data when valid', async () => {
    const onSubmit = vi.fn()
    render(<ServiceForm onSubmit={onSubmit} onCancel={vi.fn()} />)
    await userEvent.type(screen.getByLabelText(/nome/i), 'Corte de Cabelo')
    await userEvent.clear(screen.getByLabelText(/duração/i))
    await userEvent.type(screen.getByLabelText(/duração/i), '30')
    await userEvent.clear(screen.getByLabelText(/preço/i))
    await userEvent.type(screen.getByLabelText(/preço/i), '50')
    fireEvent.click(screen.getByRole('button', { name: /salvar/i }))
    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({ name: 'Corte de Cabelo', durationMinutes: 30, price: 50 })
      )
    })
  })
})
