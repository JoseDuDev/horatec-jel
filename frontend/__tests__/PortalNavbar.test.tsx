import { render, screen } from '@testing-library/react'
import { vi } from 'vitest'
import { PortalNavbar } from '@/components/portal/PortalNavbar'

vi.mock('@react-oauth/google', () => ({
  GoogleOAuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  GoogleLogin: () => <button>Sign in with Google</button>,
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

describe('PortalNavbar — links por capacidade', () => {
  it('tenant só com Aluguel: mostra Alugar, esconde Serviços/Agendar', () => {
    render(<PortalNavbar slug="x" tenantName="X" capabilities="Rentals" />)
    expect(screen.getByRole('link', { name: /alugar/i })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /serviços/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /agendar/i })).not.toBeInTheDocument()
  })

  it('tenant só com Agendamento: mostra Serviços/Agendar, esconde Alugar', () => {
    render(<PortalNavbar slug="x" tenantName="X" capabilities="Appointments" />)
    expect(screen.getByRole('link', { name: /serviços/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /agendar/i })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /alugar/i })).not.toBeInTheDocument()
  })

  it('tenant com ambos: mostra Serviços, Agendar e Alugar', () => {
    render(<PortalNavbar slug="x" tenantName="X" capabilities="Appointments, Rentals" />)
    expect(screen.getByRole('link', { name: /serviços/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /agendar/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /alugar/i })).toBeInTheDocument()
  })

  it('capabilities desconhecidas: fallback mostra agendamento, sem Alugar', () => {
    render(<PortalNavbar slug="x" tenantName="X" capabilities="" />)
    expect(screen.getByRole('link', { name: /agendar/i })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /alugar/i })).not.toBeInTheDocument()
  })
})
