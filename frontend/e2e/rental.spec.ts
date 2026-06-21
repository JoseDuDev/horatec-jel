import { test, expect } from '@playwright/test'
import {
  setupTenant, customerTestLogin,
  createRentableItem, createRentalBooking, createPayment, approvePayment,
  rentalPickup, rentalReturn, customerStorageState,
} from './helpers/api'

// yyyy-MM-dd com offset de dias a partir de hoje (UTC).
const dateOffset = (days: number) => {
  const d = new Date()
  d.setUTCDate(d.getUTCDate() + days)
  return d.toISOString().slice(0, 10)
}

test.describe('Rental: locação multi-dia (alugar → pagar → retirar → devolver)', () => {
  const slug = `e2e-rental-${Date.now()}`
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
      name: 'Furadeira de Impacto',
      quantity: 2,
      dailyRate: DAILY_RATE,
      securityDeposit: DEPOSIT,
    })

    customerSetup = await customerTestLogin(`cliente-rental-${slug}@e2e.test`, slug)

    bookingId = await createRentalBooking(customerSetup.customerToken, slug, {
      itemId, startDate, endDate,
    })
    expect(bookingId).toBeTruthy()

    // Cobra diárias + caução e aprova o pagamento (fake gateway) → confirma a reserva.
    await createPayment(customerSetup.customerToken, slug, { bookingId, amount })
    await approvePayment(slug, bookingId)
  })

  test('admin retira e devolve — estorna a caução integral', async () => {
    await rentalPickup(tenantSetup.ownerToken, slug, bookingId)

    const result = await rentalReturn(tenantSetup.ownerToken, slug, bookingId)

    expect(result.lateDays).toBe(0)
    expect(result.lateFee).toBe(0)
    expect(result.depositRefunded).toBe(DEPOSIT)
  })

  test('carteira do cliente recebe o estorno da caução (R$ 50,00)', async ({ browser }) => {
    const ctx = await browser.newContext({
      storageState: customerStorageState(customerSetup) as any,
    })
    const page = await ctx.newPage()
    try {
      await page.goto(`/${slug}/minha-conta`)
      await page.getByRole('tab', { name: /carteira/i }).click()

      await expect(page.getByText('Saldo disponível')).toBeVisible({ timeout: 10_000 })
      await expect(page.getByText('R$ 50,00', { exact: true })).toBeVisible({ timeout: 15_000 })
    } finally {
      await ctx.close()
    }
  })
})
