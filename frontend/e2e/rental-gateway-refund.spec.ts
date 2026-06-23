import { test, expect } from '@playwright/test'
import {
  setupTenant, customerTestLogin,
  createRentableItem, createRentalBooking, createPayment, approvePayment,
  adminStorageState, customerStorageState,
} from './helpers/api'

// yyyy-MM-dd com offset de dias a partir de hoje (UTC).
const dateOffset = (days: number) => {
  const d = new Date()
  d.setUTCDate(d.getUTCDate() + days)
  return d.toISOString().slice(0, 10)
}

// Variante da locação onde a devolução usa o botão "Estornar no cartão" (toggle do
// gateway): a caução volta ao meio de pagamento original (fake gateway → sucesso) e,
// portanto, NÃO é creditada na carteira do cliente.
test.describe('Rental: estorno da caução no gateway (botão "Estornar no cartão")', () => {
  const slug = `e2e-rental-gw-${Date.now()}`
  let tenantSetup: Awaited<ReturnType<typeof setupTenant>>
  let customerSetup: Awaited<ReturnType<typeof customerTestLogin>>
  let bookingId: string

  const DAILY_RATE = 30
  const DEPOSIT = 50
  const startDate = dateOffset(1)
  const endDate = dateOffset(4)            // 3 diárias
  const amount = DAILY_RATE * 3 + DEPOSIT  // diárias (90) + caução (50) = 140

  test.beforeAll(async () => {
    tenantSetup = await setupTenant(slug)

    const itemId = await createRentableItem(tenantSetup.ownerToken, slug, {
      name: 'Serra Circular',
      quantity: 2,
      dailyRate: DAILY_RATE,
      securityDeposit: DEPOSIT,
    })

    customerSetup = await customerTestLogin(`cliente-rental-gw-${slug}@e2e.test`, slug)

    bookingId = await createRentalBooking(customerSetup.customerToken, slug, {
      itemId, startDate, endDate,
    })
    expect(bookingId).toBeTruthy()

    // Cobra diárias + caução e aprova o pagamento (fake gateway) → confirma a reserva
    // e deixa o Payment Approved com MpPaymentId (requisito do estorno no gateway).
    await createPayment(customerSetup.customerToken, slug, { bookingId, amount })
    await approvePayment(slug, bookingId)
  })

  test('admin retira e devolve estornando no cartão pela UI', async ({ browser }) => {
    const ctx = await browser.newContext({
      storageState: adminStorageState(tenantSetup) as any,
    })
    const page = await ctx.newPage()
    page.on('dialog', d => d.accept()) // alerta de estorno exibido na devolução
    try {
      await page.goto('/admin/agendamentos')
      // A locação tem retirada amanhã — amplia o filtro "até" para que apareça.
      await page.locator('input[type="date"]').nth(1).fill('2027-12-31')
      await page.waitForTimeout(1000)

      const row = page.locator('tr', { hasText: 'Serra Circular' }).first()
      await expect(row).toBeVisible({ timeout: 10_000 })

      await row.getByRole('button', { name: 'Retirar' }).click()
      await expect(row.getByText('Retirado')).toBeVisible({ timeout: 10_000 })

      // Em vez de "Devolver" (carteira), usa o estorno no cartão original (gateway).
      await row.getByRole('button', { name: 'Estornar no cartão' }).click()
      await expect(row.getByText('Devolvido')).toBeVisible({ timeout: 10_000 })
    } finally {
      await ctx.close()
    }
  })

  test('carteira do cliente NÃO recebe a caução (estorno foi para o cartão)', async ({ browser }) => {
    const ctx = await browser.newContext({
      storageState: customerStorageState(customerSetup) as any,
    })
    const page = await ctx.newPage()
    try {
      await page.goto(`/${slug}/minha-conta`)
      await page.getByRole('tab', { name: /carteira/i }).click()

      await expect(page.getByText('Saldo disponível')).toBeVisible({ timeout: 10_000 })
      // Estorno foi ao gateway → saldo permanece zero e sem movimentações na carteira.
      await expect(page.getByText('R$ 0,00')).toBeVisible({ timeout: 10_000 })
      await expect(page.getByText('Nenhuma movimentação ainda.')).toBeVisible()
      await expect(page.getByText('R$ 50,00', { exact: true })).not.toBeVisible()
    } finally {
      await ctx.close()
    }
  })
})
