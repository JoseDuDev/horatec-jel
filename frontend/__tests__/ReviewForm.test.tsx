import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { vi } from 'vitest'
import { ReviewForm } from '@/components/portal/ReviewForm'

describe('ReviewForm', () => {
  it('renders star rating buttons', () => {
    render(<ReviewForm bookingId="b1" onSubmit={vi.fn()} onCancel={vi.fn()} />)
    const stars = screen.getAllByRole('button', { name: /estrela/i })
    expect(stars).toHaveLength(5)
  })

  it('calls onSubmit with stars and comment', async () => {
    const onSubmit = vi.fn()
    render(<ReviewForm bookingId="b1" onSubmit={onSubmit} onCancel={vi.fn()} />)
    const stars = screen.getAllByRole('button', { name: /estrela/i })
    fireEvent.click(stars[3])
    fireEvent.click(screen.getByRole('button', { name: /enviar avaliação/i }))
    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith({ bookingId: 'b1', stars: 4, comment: undefined })
    })
  })
})
