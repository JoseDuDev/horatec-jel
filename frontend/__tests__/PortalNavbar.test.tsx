import { render, screen } from '@testing-library/react'
import { vi } from 'vitest'
import { PortalNavbar } from '@/components/portal/PortalNavbar'

vi.mock('@react-oauth/google', () => ({
  GoogleOAuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useGoogleLogin: () => vi.fn(),
}))

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}))

vi.mock('@/store/portal-auth', () => ({
  usePortalAuthStore: () => ({ customer: null, clearCustomerAuth: vi.fn() }),
}))

describe('PortalNavbar', () => {
  it('renders tenant name', () => {
    render(<PortalNavbar slug="joao-barber" tenantName="Barbearia do João" />)
    expect(screen.getByText('Barbearia do João')).toBeInTheDocument()
  })

  it('renders navigation links', () => {
    render(<PortalNavbar slug="joao-barber" tenantName="Barbearia do João" />)
    expect(screen.getByRole('link', { name: /serviços/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /agendar/i })).toBeInTheDocument()
  })
})
