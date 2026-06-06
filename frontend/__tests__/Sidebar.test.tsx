import { render, screen } from '@testing-library/react'
import { vi } from 'vitest'
import { Sidebar } from '@/components/admin/Sidebar'

vi.mock('next/navigation', () => ({
  usePathname: () => '/admin/dashboard',
}))

describe('Sidebar', () => {
  it('renders all navigation links', () => {
    render(<Sidebar />)
    expect(screen.getByText('Dashboard')).toBeInTheDocument()
    expect(screen.getByText('Agenda')).toBeInTheDocument()
    expect(screen.getByText('Agendamentos')).toBeInTheDocument()
    expect(screen.getByText('Clientes')).toBeInTheDocument()
    expect(screen.getByText('Serviços')).toBeInTheDocument()
    expect(screen.getByText('Recursos')).toBeInTheDocument()
    expect(screen.getByText('Financeiro')).toBeInTheDocument()
    expect(screen.getByText('Notificações')).toBeInTheDocument()
    expect(screen.getByText('Configurações')).toBeInTheDocument()
  })

  it('marks the current route as active', () => {
    render(<Sidebar />)
    const dashboardLink = screen.getByRole('link', { name: /dashboard/i })
    expect(dashboardLink).toHaveClass('bg-slate-100')
  })
})
