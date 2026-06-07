import { render, screen, waitFor } from '@testing-library/react'
import { vi } from 'vitest'

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/carteira',
}))

vi.mock('@/store/auth', () => ({
  useAuthStore: (sel: (s: { accessToken: string | null }) => unknown) =>
    sel({ accessToken: 'test-token' }),
}))

vi.mock('@/lib/api/wallet', () => ({
  walletApi: {
    getVouchers: vi.fn().mockResolvedValue([
      {
        id: 'v1', code: 'PROMO10', discountType: 'Percentage', discountValue: 10,
        usedCount: 2, maxUses: 50, isActive: true, createdAt: '2026-01-01T00:00:00Z',
      },
      {
        id: 'v2', code: 'FLAT5', discountType: 'Fixed', discountValue: 5,
        usedCount: 0, isActive: false, createdAt: '2026-02-01T00:00:00Z',
      },
    ]),
    createVoucher: vi.fn().mockResolvedValue('new-id'),
    deactivateVoucher: vi.fn().mockResolvedValue(undefined),
    addCredits: vi.fn().mockResolvedValue(undefined),
  },
}))

import CarteiraPage from '@/app/(admin)/admin/carteira/page'

describe('CarteiraPage', () => {
  it('renders voucher codes after loading', async () => {
    render(<CarteiraPage />)
    await waitFor(() => {
      expect(screen.getByText('PROMO10')).toBeInTheDocument()
      expect(screen.getByText('FLAT5')).toBeInTheDocument()
    })
  })

  it('shows correct status badges', async () => {
    render(<CarteiraPage />)
    await waitFor(() => {
      expect(screen.getByText('Ativo')).toBeInTheDocument()
      expect(screen.getByText('Inativo')).toBeInTheDocument()
    })
  })
})
