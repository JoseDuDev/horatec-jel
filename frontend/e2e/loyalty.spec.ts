import { test, expect } from '@playwright/test'
import {
  setupTenant, createService, createResource, linkServiceToResource,
  setResourceRulesWeekdays, customerTestLogin, setLoyalty,
  approvePayment, adminStorageState, customerStorageState,
} from './helpers/api'

test.describe('Loyalty: booking concluído credita carteira', () => {
  const slug = `e2e-loyalty-${Date.now()}`
  let ownerToken: string
  let tenantSetup: Awaited<ReturnType<typeof setupTenant>>
  let customerSetup: Awaited<ReturnType<typeof customerTestLogin>>
  let bookingId: string

  test.beforeAll(async ({ browser }) => {
    tenantSetup = await setupTenant(slug)
    ownerToken = tenantSetup.ownerToken

    await setLoyalty(ownerToken, slug, 10)

    const serviceId = await createService(ownerToken, slug, {
      name: 'Manicure',
      durationMinutes: 45,
      price: 60,
    })
    const resourceId = await createResource(ownerToken, slug, 'Carla Manicure')
    await linkServiceToResource(ownerToken, slug, resourceId, serviceId)
    await setResourceRulesWeekdays(ownerToken, slug, resourceId)

    customerSetup = await customerTestLogin(`cliente-loyalty-${slug}@e2e.test`, slug)

    const ctx = await browser.newContext({
      storageState: customerStorageState(customerSetup) as any,
    })
    const page = await ctx.newPage()
    try {
      await page.goto(`/${slug}/agendar`)
      await expect(page.getByText('Manicure')).toBeVisible({ timeout: 15_000 })
      await page.getByText('Manicure').click()
      await page.getByRole('button', { name: 'Próximo' }).click()

      await expect(page.getByText('Carla Manicure')).toBeVisible()
      await page.getByText('Carla Manicure').click()
      await page.getByRole('button', { name: 'Próximo' }).click()

      await expect(page.getByText('Escolha a data e horário')).toBeVisible()
      const dayBtns = page.locator('.overflow-x-auto').locator('button')
      let slotFound = false
      for (let i = 0; i < 14 && !slotFound; i++) {
        await dayBtns.nth(i).click()
        await page.waitForTimeout(600)
        const count = await page.getByRole('button', { name: /^\d{2}:\d{2}$/ }).count()
        if (count > 0) slotFound = true
      }
      const slotButton = page.getByRole('button', { name: /^\d{2}:\d{2}$/ }).first()
      await expect(slotButton).toBeVisible({ timeout: 10_000 })
      await slotButton.click()
      await page.getByRole('button', { name: 'Próximo' }).click()

      await expect(page.getByText('Confirme seu agendamento')).toBeVisible()
      await page.getByRole('button', { name: /confirmar/i }).click()

      await page.waitForURL(/\/agendar\/.+\/status/, { timeout: 15_000 })
      const url = page.url()
      bookingId = url.match(/agendar\/([^/]+)\/status/)?.[1] ?? ''
      expect(bookingId).toBeTruthy()
    } finally {
      await ctx.close()
    }

    // Aprova o pagamento (fake gateway) — pré-condição do bônus de fidelidade.
    // O PaymentConfirmedEventHandler também confirma o booking (Pending → Confirmed),
    // então não é preciso confirmar manualmente.
    await approvePayment(slug, bookingId)
  })

  test('admin marca booking como Concluído via UI', async ({ browser }) => {
    const ctx = await browser.newContext({
      storageState: adminStorageState(tenantSetup) as any,
    })
    const page = await ctx.newPage()
    try {
      await page.goto('/admin/agendamentos')

      await page.locator('input[type="date"]').nth(1).fill('2027-12-31')
      await page.waitForTimeout(1000)

      const row = page.locator('tr', { hasText: 'Manicure' }).first()
      await expect(row).toBeVisible({ timeout: 10_000 })
      await row.getByRole('button', { name: 'Concluir' }).click()

      await expect(row.getByText(/concluído/i)).toBeVisible({ timeout: 10_000 })
    } finally {
      await ctx.close()
    }
  })

  test('carteira do cliente mostra crédito de fidelidade (R$ 6,00 = 10% de R$ 60)', async ({ browser }) => {
    const ctx = await browser.newContext({
      storageState: customerStorageState(customerSetup) as any,
    })
    const page = await ctx.newPage()
    try {
      await page.goto(`/${slug}/minha-conta`)
      await page.getByRole('tab', { name: /carteira/i }).click()

      await expect(page.getByText('Saldo disponível')).toBeVisible({ timeout: 10_000 })
      // Crédito de fidelidade aparece em 2 lugares (saldo "R$ 6,00" e badge "+R$ 6,00").
      // exact:true isola o saldo e evita strict mode violation.
      await expect(page.getByText('R$ 6,00', { exact: true })).toBeVisible({ timeout: 15_000 })
    } finally {
      await ctx.close()
    }
  })
})
