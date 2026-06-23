import { Suspense } from 'react'
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react'
import { vi } from 'vitest'

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/slug/minha-conta',
}))

vi.mock('@/store/portal-auth', () => ({
  usePortalAuthStore: () => ({
    accessToken: 'tok123',
    customer: { id: 'c1', name: 'Cliente', email: 'c@test.com' },
  }),
}))

vi.mock('@/lib/api/portal', () => ({
  portalApi: {
    myBookings: vi.fn().mockResolvedValue([
      {
        id: 'b1', serviceName: 'Corte', resourceName: 'João',
        scheduledAt: new Date(Date.now() + 86400000).toISOString(),
        durationMinutes: 30, status: 'Confirmed', totalAmount: 100,
      },
    ]),
    myFavorites: vi.fn().mockResolvedValue([]),
    cancelBooking: vi.fn().mockResolvedValue(undefined),
    createReview: vi.fn(),
    removeFavorite: vi.fn(),
  },
}))

vi.mock('@/lib/api/wallet', () => ({
  portalWalletApi: {
    getWallet: vi.fn().mockResolvedValue({ balance: 0, transactions: [] }),
  },
}))

vi.mock('@/components/portal/GoogleSignInButton', () => ({
  GoogleSignInButton: () => <button>Google</button>,
}))

vi.mock('@/components/portal/ReviewForm', () => ({
  ReviewForm: () => <div>ReviewForm</div>,
}))

vi.mock('@/components/portal/WalletWidget', () => ({
  WalletWidget: () => <div>WalletWidget</div>,
}))

import MinhaContaPage from '@/app/(portal)/[slug]/minha-conta/page'

// params é um Promise (Next 16 / React 19 — desempacotado via use()); precisa de um
// boundary de Suspense, e de um act() assíncrono para liberar a resolução da Promise
// (e o retry do componente suspenso) antes das asserções.
const renderPage = async () => {
  await act(async () => {
    render(
      <Suspense fallback={null}>
        <MinhaContaPage params={Promise.resolve({ slug: 'barb' })} />
      </Suspense>
    )
  })
}

describe('MinhaContaPage — cancel booking', () => {
  it('shows cancel button for upcoming confirmed bookings', async () => {
    await renderPage()
    await waitFor(() => {
      expect(screen.getByText('Corte')).toBeInTheDocument()
    })
    expect(screen.getByRole('button', { name: /cancelar/i })).toBeInTheDocument()
  })

  it('calls cancelBooking on confirm', async () => {
    const { portalApi } = await import('@/lib/api/portal')
    await renderPage()
    await waitFor(() => screen.getByText('Corte'))

    fireEvent.click(screen.getByRole('button', { name: /^cancelar$/i }))
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /confirmar cancelamento/i })).toBeInTheDocument()
    })
    fireEvent.click(screen.getByRole('button', { name: /confirmar cancelamento/i }))
    await waitFor(() => {
      expect(portalApi.cancelBooking).toHaveBeenCalledWith('barb', 'tok123', 'b1')
    })
  })
})
