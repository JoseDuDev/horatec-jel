import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import LoginPage from '@/app/(auth)/login/page'

vi.mock('@/lib/api/auth', () => ({
  authApi: {
    login: vi.fn(),
    me: vi.fn(),
  },
}))

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  useSearchParams: () => ({ get: () => null }),
}))

describe('LoginPage', () => {
  it('shows validation error when fields are empty', async () => {
    render(<LoginPage />)
    fireEvent.click(screen.getByRole('button', { name: /entrar/i }))
    await waitFor(() => {
      expect(screen.getByText(/email obrigatório/i)).toBeInTheDocument()
    })
  })

  it('shows error message on failed login', async () => {
    const { authApi } = await import('@/lib/api/auth')
    vi.mocked(authApi.login).mockRejectedValue(new Error('Credenciais inválidas'))

    render(<LoginPage />)
    await userEvent.type(screen.getByLabelText(/slug do tenant/i), 'meu-negocio')
    await userEvent.type(screen.getByLabelText(/email/i), 'admin@teste.com')
    await userEvent.type(screen.getByLabelText(/senha/i), 'wrongpass')
    fireEvent.click(screen.getByRole('button', { name: /entrar/i }))

    await waitFor(() => {
      expect(screen.getByText(/credenciais inválidas/i)).toBeInTheDocument()
    })
  })
})
