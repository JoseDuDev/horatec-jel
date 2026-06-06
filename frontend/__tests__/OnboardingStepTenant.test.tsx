import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { OnboardingStepTenant } from '@/components/onboarding/OnboardingStepTenant'

describe('OnboardingStepTenant', () => {
  it('shows validation error when name is empty', async () => {
    render(<OnboardingStepTenant onNext={vi.fn()} />)
    fireEvent.click(screen.getByRole('button', { name: /próximo/i }))
    await waitFor(() => {
      expect(screen.getByText(/nome obrigatório/i)).toBeInTheDocument()
    })
  })

  it('calls onNext with form data when valid', async () => {
    const onNext = vi.fn()
    render(<OnboardingStepTenant onNext={onNext} />)
    await userEvent.type(screen.getByLabelText(/nome do negócio/i), 'Barbearia do João')
    fireEvent.click(screen.getByRole('button', { name: /próximo/i }))
    await waitFor(() => {
      expect(onNext).toHaveBeenCalledWith(
        expect.objectContaining({ name: 'Barbearia do João' })
      )
    })
  })
})
