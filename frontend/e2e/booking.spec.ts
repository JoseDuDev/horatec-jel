import { test, expect } from '@playwright/test'
import {
  setupTenant, createService, createResource, linkServiceToResource,
  setBusinessHoursWeekdays, customerTestLogin, confirmBooking,
  customerStorageState,
} from './helpers/api'

test.describe('Portal booking flow', () => {
  const slug = `e2e-booking-${Date.now()}`
  let serviceId: string
  let ownerToken: string
  let customerSetup: Awaited<ReturnType<typeof customerTestLogin>>

  test.beforeAll(async () => {
    const tenant = await setupTenant(slug)
    ownerToken = tenant.ownerToken

    serviceId = await createService(ownerToken, slug, {
      name: 'Corte de cabelo',
      durationMinutes: 60,
      price: 80,
    })
    const resourceId = await createResource(ownerToken, slug, 'João Barbeiro')
    await linkServiceToResource(ownerToken, slug, resourceId, serviceId)
    await setBusinessHoursWeekdays(ownerToken, slug)

    customerSetup = await customerTestLogin(`cliente-${slug}@e2e.test`, slug)
  })

  test('cliente navega pelo wizard e agendamento fica Confirmado', async ({ browser }) => {
    const ctx = await browser.newContext({
      storageState: customerStorageState(customerSetup) as any,
    })
    const page = await ctx.newPage()

    // 1. Abre portal e step 0 — escolhe serviço
    await page.goto(`/${slug}/agendar`)
    await expect(page.getByText('Escolha o serviço')).toBeVisible({ timeout: 15_000 })
    await page.getByText('Corte de cabelo').click()
    await page.getByRole('button', { name: 'Próximo' }).click()

    // 2. Step 1 — escolhe recurso
    await expect(page.getByText('Escolha o profissional/recurso')).toBeVisible()
    await page.getByText('João Barbeiro').click()
    await page.getByRole('button', { name: 'Próximo' }).click()

    // 3. Step 2 — escolhe horário
    await expect(page.getByText('Escolha a data e horário')).toBeVisible()

    // Date strip: click each day until slots appear (handles weekends with no business hours)
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

    // 4. Step 3 — confirma agendamento
    await expect(page.getByText('Confirme seu agendamento')).toBeVisible()
    const confirmBtn = page.getByRole('button', { name: /confirmar/i })
    await expect(confirmBtn).toBeVisible()
    await confirmBtn.click()

    // 5. Aguarda navegação para página de status
    await page.waitForURL(/\/agendar\/.+\/status/, { timeout: 15_000 })

    // Extrai bookingId da URL
    const url = page.url()
    const bookingId = url.match(/agendar\/([^/]+)\/status/)?.[1]
    expect(bookingId).toBeTruthy()

    // 6. Status inicial: Aguardando confirmação
    await expect(page.getByRole('heading', { name: /aguardando/i })).toBeVisible({ timeout: 10_000 })

    // 7. Admin confirma via API
    await confirmBooking(ownerToken, slug, bookingId!)

    // 8. Recarrega e verifica status Confirmado
    await page.reload()
    await expect(page.getByRole('heading', { name: 'Confirmado' })).toBeVisible({ timeout: 10_000 })

    await ctx.close()
  })
})
