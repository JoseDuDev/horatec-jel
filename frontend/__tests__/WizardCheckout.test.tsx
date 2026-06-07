import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { vi } from 'vitest'

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/slug/agendar',
}))

vi.mock('@/store/portal-auth', () => ({
  usePortalAuthStore: () => ({
    accessToken: 'tok123',
    customer: { id: 'c1', name: 'Cliente', email: 'c@test.com' },
  }),
}))

vi.mock('@/lib/api/wallet', () => ({
  portalWalletApi: {
    getWallet: vi.fn().mockResolvedValue({ walletId: 'w1', balance: 50, transactions: [] }),
    validateVoucher: vi.fn().mockResolvedValue({
      code: 'PROMO10',
      discountType: 'Percentage',
      discountValue: 10,
      discountAmount: 10,
      finalPrice: 90,
      description: null,
    }),
  },
}))

vi.mock('@/lib/api/portal', () => ({
  portalApi: {
    createBooking: vi.fn().mockResolvedValue({
      id: 'b1', scheduledAt: '2026-07-01T10:00:00Z', status: 'Pending',
    }),
    createPayment: vi.fn().mockResolvedValue({
      paymentId: 'p1', preferenceId: 'pref1', paymentUrl: undefined,
    }),
    slots: vi.fn().mockResolvedValue([]),
  },
}))

import { WizardStepConfirm } from '@/components/portal/WizardStepConfirm'

const mockService = {
  id: 's1', name: 'Corte', price: 100, durationMinutes: 30,
  isActive: true, description: '', category: null, tenantId: 't1',
}
const mockResource = {
  id: 'r1', name: 'João', isActive: true, type: 'Professional' as const,
  serviceIds: ['s1'], email: null, phone: null, specialty: null, bio: null, avatarUrl: null,
  tenantId: 't1',
}

describe('WizardStepConfirm', () => {
  it('renders price and shows wallet balance after loading', async () => {
    render(
      <WizardStepConfirm
        service={mockService as any}
        resource={mockResource as any}
        slot="2026-07-01T10:00:00Z"
        notes=""
        onNotesChange={() => {}}
        onConfirm={() => {}}
        loading={false}
      />
    )
    await waitFor(() => {
      expect(screen.getAllByText(/R\$ 100/).length).toBeGreaterThan(0)
      expect(screen.getByText(/Saldo disponível/)).toBeInTheDocument()
    })
  })

  it('calls onConfirm with voucher code when applied', async () => {
    const onConfirm = vi.fn()
    render(
      <WizardStepConfirm
        service={mockService as any}
        resource={mockResource as any}
        slot="2026-07-01T10:00:00Z"
        notes=""
        onNotesChange={() => {}}
        onConfirm={onConfirm}
        loading={false}
      />
    )

    const input = screen.getByPlaceholderText(/CÓDIGO DO VOUCHER/i)
    fireEvent.change(input, { target: { value: 'PROMO10' } })
    fireEvent.click(screen.getByText('Aplicar'))

    await waitFor(() => {
      expect(screen.getByText('PROMO10')).toBeInTheDocument()
    })

    fireEvent.click(screen.getByRole('button', { name: /confirmar/i }))

    expect(onConfirm).toHaveBeenCalledWith(
      expect.objectContaining({ voucherCode: 'PROMO10' })
    )
  })
})
