import { render, screen, waitFor } from '@testing-library/react'
import { vi } from 'vitest'

vi.mock('next/navigation', () => ({ usePathname: () => '/admin/dashboard' }))

vi.mock('@/lib/api/tenants', () => ({
  tenantsApi: { me: vi.fn() },
}))

import { Sidebar } from '@/components/admin/Sidebar'
import { tenantsApi } from '@/lib/api/tenants'

describe('Sidebar — visibilidade por capacidade', () => {
  it('tenant só com Aluguel: esconde os módulos de agendamento, mantém Locações', async () => {
    ;(tenantsApi.me as ReturnType<typeof vi.fn>).mockResolvedValue({ capabilities: 'Rentals' })
    render(<Sidebar />)

    await waitFor(() => expect(screen.queryByText('Agendamentos')).not.toBeInTheDocument())
    expect(screen.queryByText('Serviços')).not.toBeInTheDocument()
    expect(screen.queryByText('Disponibilidade')).not.toBeInTheDocument()
    expect(screen.getByText('Locações')).toBeInTheDocument()
    expect(screen.getByText('Dashboard')).toBeInTheDocument() // item comum, sempre visível
  })

  it('tenant só com Agendamento: esconde Locações, mantém Agendamentos', async () => {
    ;(tenantsApi.me as ReturnType<typeof vi.fn>).mockResolvedValue({ capabilities: 'Appointments' })
    render(<Sidebar />)

    await waitFor(() => expect(screen.queryByText('Locações')).not.toBeInTheDocument())
    expect(screen.getByText('Agendamentos')).toBeInTheDocument()
    expect(screen.getByText('Serviços')).toBeInTheDocument()
  })
})
