import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { vi } from 'vitest'

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/configuracoes',
}))

vi.mock('@/store/auth', () => ({
  useAuthStore: (sel: (s: { accessToken: string | null }) => unknown) =>
    sel({ accessToken: 'test-token' }),
}))

vi.mock('@/lib/api/tenants', () => ({
  tenantsApi: {
    me: vi.fn().mockResolvedValue({
      id: 't1', name: 'Barbearia', slug: 'barb', timezone: 'America/Sao_Paulo', plan: 'Free',
      cancellationPolicy: { minCancellationHours: 2, cancellationFeePercent: 0, allowCustomerCancellation: true },
      loyaltySettings: { isEnabled: true, creditRatePercent: 5, minBookingAmount: 0 },
    }),
    update: vi.fn().mockResolvedValue(undefined),
    updateTheme: vi.fn().mockResolvedValue(undefined),
    updateLoyaltySettings: vi.fn().mockResolvedValue(undefined),
    updateCancellationPolicy: vi.fn().mockResolvedValue(undefined),
  },
}))

import ConfiguracoesPage from '@/app/(admin)/admin/configuracoes/page'

describe('ConfiguracoesPage — Loyalty & Cancellation tabs', () => {
  it('shows loyalty tab with taxa de crédito field', async () => {
    render(<ConfiguracoesPage />)
    fireEvent.click(await screen.findByRole('tab', { name: /fidelidade/i }))
    await waitFor(() => {
      expect(screen.getByLabelText(/taxa de crédito/i)).toBeInTheDocument()
    })
  })

  it('shows cancellation tab with horas mínimas field', async () => {
    render(<ConfiguracoesPage />)
    fireEvent.click(await screen.findByRole('tab', { name: /cancelamentos/i }))
    await waitFor(() => {
      expect(screen.getByLabelText(/horas mínimas/i)).toBeInTheDocument()
    })
  })
})
