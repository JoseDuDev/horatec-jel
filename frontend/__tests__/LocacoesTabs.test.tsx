import { render, screen, waitFor } from '@testing-library/react'
import { vi } from 'vitest'

vi.mock('@/lib/api/rentals', () => ({
  rentalsApi: { list: vi.fn(), create: vi.fn() },
}))

vi.mock('@/lib/api/bookings', () => ({
  bookingsApi: { list: vi.fn() },
}))

import LocacoesPage from '@/app/(admin)/admin/locacoes/page'
import { rentalsApi } from '@/lib/api/rentals'
import { bookingsApi } from '@/lib/api/bookings'

const mock = (fn: unknown) => fn as ReturnType<typeof vi.fn>

describe('Locações — abas Reservas e Itens', () => {
  beforeEach(() => {
    mock(rentalsApi.list).mockResolvedValue([])
    mock(bookingsApi.list).mockResolvedValue([])
  })

  it('abre na aba Reservas — é o dia a dia de quem só tem locação', async () => {
    render(<LocacoesPage />)

    expect(screen.getByText('Reservas')).toBeInTheDocument()
    expect(screen.getByText('Itens')).toBeInTheDocument()
    await waitFor(() => expect(bookingsApi.list).toHaveBeenCalled())
  })

  it('lista apenas reservas de locação, ignorando agendamentos de serviço', async () => {
    mock(bookingsApi.list).mockResolvedValue([
      {
        id: '1', customerName: 'Maria', customerEmail: 'maria@x.com',
        serviceName: 'Furadeira', resourceName: '-', scheduledAt: '2026-09-12T10:00:00',
        status: 'Confirmed', totalAmount: 90, kind: 'Rental', rentalStatus: 'Reserved',
      },
      {
        id: '2', customerName: 'João', customerEmail: 'joao@x.com',
        serviceName: 'Corte', resourceName: 'Cadeira 1', scheduledAt: '2026-09-12T11:00:00',
        status: 'Confirmed', totalAmount: 50, kind: 'Service',
      },
    ])

    render(<LocacoesPage />)

    await waitFor(() => expect(screen.getByText('Maria')).toBeInTheDocument())
    expect(screen.queryByText('João')).not.toBeInTheDocument()
  })

  it('a reserva reservada oferece Retirar — o botão que faltava a quem só tem locação', async () => {
    mock(bookingsApi.list).mockResolvedValue([
      {
        id: '1', customerName: 'Maria', customerEmail: 'maria@x.com',
        serviceName: 'Furadeira', resourceName: '-', scheduledAt: '2026-09-12T10:00:00',
        status: 'Confirmed', totalAmount: 90, kind: 'Rental', rentalStatus: 'Reserved',
      },
    ])

    render(<LocacoesPage />)

    await waitFor(() => expect(screen.getByText('Retirar')).toBeInTheDocument())
  })
})
