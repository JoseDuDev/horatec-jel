import { test, expect } from '@playwright/test'
import { setupTenant, createService, createRentableItem } from './helpers/api'

// Valida o enforcement da feature "Capacidades + Limites por plano" contra a stack real
// (API .NET + Postgres). São testes de API (sem navegador).
const API = 'http://localhost:8086/api/v1'

test.describe('Planos: enforcement de capacidade e quota', () => {
  test('plano Free bloqueia o 6º serviço (limite de quota)', async () => {
    const slug = `e2e-quota-${Date.now()}`
    const setup = await setupTenant(slug) // nasce Free (maxServices=5) + ambos os módulos

    for (let i = 1; i <= 5; i++) {
      await createService(setup.ownerToken, slug, { name: `Servico ${i}`, durationMinutes: 30, price: 50 })
    }

    // O 6º estoura o limite do plano Free.
    await expect(
      createService(setup.ownerToken, slug, { name: 'Servico 6', durationMinutes: 30, price: 50 })
    ).rejects.toThrow(/limite/i)
  })

  test('tenant só-aluguel: bloqueia serviço (capacidade) e permite item de locação', async () => {
    const slug = `e2e-cap-${Date.now()}`

    const res = await fetch(`${API}/platform/tenants`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: `Loc ${slug}`, slug, vertical: 'ToolRental',
        ownerName: 'Owner', ownerEmail: `owner-${slug}@e2e.test`,
        ownerPassword: 'E2eTest1', capabilities: 'Rentals',
      }),
    })
    expect(res.ok).toBeTruthy()
    const data = await res.json()
    const token = data.tokens.accessToken

    // Criar serviço deve falhar — o tenant não contratou o módulo de Agendamento.
    const svcRes = await fetch(`${API}/services`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`,
        'X-Tenant-Slug': slug,
      },
      body: JSON.stringify({ name: 'Corte', durationMinutes: 30, price: 50, description: null, category: null }),
    })
    expect(svcRes.ok).toBeFalsy()

    // Criar item de locação deve funcionar — o módulo de Aluguel está contratado.
    const itemId = await createRentableItem(token, slug, {
      name: 'Furadeira', quantity: 1, dailyRate: 30, securityDeposit: 50,
    })
    expect(itemId).toBeTruthy()
  })
})
