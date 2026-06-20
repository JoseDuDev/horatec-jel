import { test, expect } from '@playwright/test'
import {
  setupTenant, createService, createResource, linkServiceToResource,
  setResourceRulesWeekdays, customerTestLogin,
  adminStorageState, customerStorageState,
} from './helpers/api'

test.describe('Admin workflow: confirm → customer cancel → financeiro', () => {
  const slug = `e2e-workflow-${Date.now()}`
  let ownerToken: string
  let tenantSetup: Awaited<ReturnType<typeof setupTenant>>
  let customerSetup: Awaited<ReturnType<typeof customerTestLogin>>
  let bookingId: string

  test.beforeAll(async ({ browser }) => {
    tenantSetup = await setupTenant(slug)
    ownerToken = tenantSetup.ownerToken

    const serviceId = await createService(ownerToken, slug, {
      name: 'Massagem',
      durationMinutes: 60,
      price: 120,
    })
    const resourceId = await createResource(ownerToken, slug, 'Ana Massagista')
    await linkServiceToResource(ownerToken, slug, resourceId, serviceId)
    await setResourceRulesWeekdays(ownerToken, slug, resourceId)

    customerSetup = await customerTestLogin(`cliente-workflow-${slug}@e2e.test`, slug)

    // Customer creates booking via wizard
    const ctx = await browser.newContext({
      storageState: customerStorageState(customerSetup) as any,
    })
    const page = await ctx.newPage()
    try {
      await page.goto(`/${slug}/agendar`)
      await expect(page.getByText('Massagem')).toBeVisible({ timeout: 15_000 })
      await page.getByText('Massagem').click()
      await page.getByRole('button', { name: 'Próximo' }).click()

      await expect(page.getByText('Ana Massagista')).toBeVisible()
      await page.getByText('Ana Massagista').click()
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
  })

  test('admin confirma agendamento via UI', async ({ browser }) => {
    const ctx = await browser.newContext({
      storageState: adminStorageState(tenantSetup) as any,
    })
    const page = await ctx.newPage()
    try {
      await page.goto('/admin/agendamentos')

      // Extend "to" date to show future bookings (default is today)
      await page.locator('input[type="date"]').nth(1).fill('2027-12-31')
      // Wait for table to reload
      await page.waitForTimeout(1000)

      const row = page.locator('tr', { hasText: 'Massagem' }).first()
      await expect(row).toBeVisible({ timeout: 10_000 })
      await row.getByRole('button', { name: 'Confirmar' }).click()

      await expect(row.getByText(/confirmado/i)).toBeVisible({ timeout: 10_000 })
    } finally {
      await ctx.close()
    }
  })

  test('cliente cancela agendamento via portal', async ({ browser }) => {
    const ctx = await browser.newContext({
      storageState: customerStorageState(customerSetup) as any,
    })
    const page = await ctx.newPage()
    try {
      await page.goto(`/${slug}/minha-conta`)
      await expect(page.getByText('Próximos')).toBeVisible({ timeout: 10_000 })
      await expect(page.getByText('Massagem')).toBeVisible({ timeout: 10_000 })

      // Step 1: click "Cancelar" ghost button
      await page.getByRole('button', { name: 'Cancelar' }).first().click()

      // Step 2: inline confirmation — click "Confirmar cancelamento" (no dialog)
      await page.getByRole('button', { name: 'Confirmar cancelamento' }).click()

      // Booking removed from list
      await expect(page.getByText('Massagem')).not.toBeVisible({ timeout: 10_000 })
    } finally {
      await ctx.close()
    }
  })

  test('admin acessa financeiro após cancelamento', async ({ browser }) => {
    const ctx = await browser.newContext({
      storageState: adminStorageState(tenantSetup) as any,
    })
    const page = await ctx.newPage()
    try {
      await page.goto('/admin/financeiro')
      await expect(page.getByText('Reembolsos')).toBeVisible({ timeout: 10_000 })
    } finally {
      await ctx.close()
    }
  })
})
