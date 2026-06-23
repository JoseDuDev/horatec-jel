import { test, expect } from '@playwright/test'
import { setupTenant, adminStorageState } from './helpers/api'

// Cobre o campo "Tipo" do ResourceForm, que é um Select (combobox base-ui) — interação
// inviável no jsdom (portais/pointer events), por isso validada aqui no navegador real.
test.describe('Admin: criar recurso escolhendo o Tipo pelo Select', () => {
  const slug = `e2e-recurso-${Date.now()}`
  let tenantSetup: Awaited<ReturnType<typeof setupTenant>>

  test.beforeAll(async () => {
    tenantSetup = await setupTenant(slug)
  })

  test('seleciona "Quadra" no Select e cria o recurso com esse tipo', async ({ browser }) => {
    const ctx = await browser.newContext({
      storageState: adminStorageState(tenantSetup) as any,
    })
    const page = await ctx.newPage()
    try {
      await page.goto('/admin/recursos')

      await page.getByRole('button', { name: 'Novo Recurso' }).click()

      // O diálogo abre com o ResourceForm (Tipo padrão = Profissional).
      await expect(page.getByRole('dialog')).toBeVisible({ timeout: 10_000 })
      await page.getByLabel('Nome').fill('Quadra de Tênis')

      // Abre o Select de Tipo e escolhe "Quadra".
      await page.locator('[data-slot="select-trigger"]').click()
      await page.locator('[data-slot="select-item"]').filter({ hasText: 'Quadra' }).click()
      // O trigger reflete o rótulo PT-BR do tipo escolhido (não o valor cru "Court").
      await expect(page.locator('[data-slot="select-trigger"]')).toContainText('Quadra')

      await page.getByRole('button', { name: 'Salvar' }).click()

      // O recurso é criado com o tipo escolhido — o card o exibe pelo rótulo "Quadra".
      await expect(page.getByText('Quadra de Tênis')).toBeVisible({ timeout: 10_000 })
      await expect(page.getByText('Tipo: Quadra')).toBeVisible()
    } finally {
      await ctx.close()
    }
  })
})
