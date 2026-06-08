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

vi.mock('@/lib/api/tenants', () => ({
  tenantsApi: {
    me: vi.fn(),
  },
}))

const replaceMock = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: replaceMock }),
  useSearchParams: () => ({ get: () => null }),
}))

describe('LoginPage', () => {
  beforeEach(() => {
    replaceMock.mockClear()
  })

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

  it('redirects to /admin/onboarding when onboarding not completed', async () => {
    const { authApi } = await import('@/lib/api/auth')
    const { tenantsApi } = await import('@/lib/api/tenants')

    vi.mocked(authApi.login).mockResolvedValue({
      accessToken: 'token', refreshToken: 'r', expiresAt: '',
    })
    vi.mocked(authApi.me).mockResolvedValue({
      id: '1', name: 'Owner', email: 'a@b.com', role: 'TenantOwner',
    } as never)
    vi.mocked(tenantsApi.me).mockResolvedValue({
      isOnboardingCompleted: false,
    } as never)

    render(<LoginPage />)
    await userEvent.type(screen.getByLabelText(/slug/i), 'barbearia')
    await userEvent.type(screen.getByLabelText(/email/i), 'a@b.com')
    await userEvent.type(screen.getByLabelText(/senha/i), '123456')
    fireEvent.click(screen.getByRole('button', { name: /entrar/i }))

    await waitFor(() => {
      expect(replaceMock).toHaveBeenCalledWith('/admin/onboarding')
    })
  })

  it('redirects to /admin/dashboard when onboarding is completed', async () => {
    const { authApi } = await import('@/lib/api/auth')
    const { tenantsApi } = await import('@/lib/api/tenants')

    vi.mocked(authApi.login).mockResolvedValue({
      accessToken: 'token', refreshToken: 'r', expiresAt: '',
    })
    vi.mocked(authApi.me).mockResolvedValue({
      id: '1', name: 'Owner', email: 'a@b.com', role: 'TenantOwner',
    } as never)
    vi.mocked(tenantsApi.me).mockResolvedValue({
      isOnboardingCompleted: true,
    } as never)

    render(<LoginPage />)
    await userEvent.type(screen.getByLabelText(/slug/i), 'barbearia')
    await userEvent.type(screen.getByLabelText(/email/i), 'a@b.com')
    await userEvent.type(screen.getByLabelText(/senha/i), '123456')
    fireEvent.click(screen.getByRole('button', { name: /entrar/i }))

    await waitFor(() => {
      expect(replaceMock).toHaveBeenCalledWith('/admin/dashboard')
    })
  })
})
