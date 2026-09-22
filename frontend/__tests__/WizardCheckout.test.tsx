import { render, screen, fireEvent } from '@testing-library/react'
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

function renderStep(onConfirm = vi.fn(), loading = false) {
  render(
    <WizardStepConfirm
      slug="barbearia"
      service={mockService as any}
      resource={mockResource as any}
      slot="2026-07-01T10:00:00Z"
      notes=""
      onNotesChange={vi.fn()}
      onConfirm={onConfirm}
      loading={loading}
    />
  )
  return onConfirm
}

describe('WizardStepConfirm — sem cobrança online', () => {
  it('mostra o valor do serviço e avisa que o pagamento é no local', () => {
    renderStep()

    expect(screen.getByText('R$ 100,00')).toBeInTheDocument()
    expect(screen.getByText(/pagamento é feito no local/i)).toBeInTheDocument()
  })

  // O portal não cobra: o gateway do Mercado Pago é global e o dinheiro cairia na
  // conta da plataforma, não na do lojista. Sem cobrança, cupom e créditos da
  // carteira não têm no que ser aplicados — por isso saíram da tela.
  it('não oferece cupom nem créditos da carteira', () => {
    renderStep()

    expect(screen.queryByPlaceholderText(/CÓDIGO DO VOUCHER/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/créditos da carteira/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/total a pagar/i)).not.toBeInTheDocument()
  })

  it('o botão confirma sem falar em pagar', () => {
    const onConfirm = renderStep()

    const botao = screen.getByRole('button', { name: 'Confirmar agendamento' })
    expect(botao).toBeInTheDocument()

    fireEvent.click(botao)
    expect(onConfirm).toHaveBeenCalledTimes(1)
  })

  it('desabilita o botão enquanto confirma', () => {
    renderStep(vi.fn(), true)

    expect(screen.getByRole('button', { name: 'Confirmando...' })).toBeDisabled()
  })
})
