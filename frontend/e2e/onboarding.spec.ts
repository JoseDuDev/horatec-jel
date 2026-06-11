import { test, expect } from '@playwright/test'
import { setupTenant } from './helpers/api'

test.describe('Onboarding wizard', () => {
  const slug = `e2e-onboarding-${Date.now()}`
  let ownerEmail: string

  test.beforeAll(async () => {
    const setup = await setupTenant(slug)
    ownerEmail = setup.ownerEmail
  })

  test('owner faz login e conclui o wizard de 5 passos', async ({ page }) => {
    // 1. Abre a página de login
    await page.goto('/login')

    // Preenche o formulário de login
    await page.locator('#tenantSlug').fill(slug)
    await page.locator('#email').fill(ownerEmail)
    await page.locator('#password').fill('E2eTest1')
    await page.getByRole('button', { name: 'Entrar' }).click()

    // 2. Deve redirecionar para /admin/onboarding (tenant novo = onboarding não concluído)
    await page.waitForURL(/\/admin\/onboarding/, { timeout: 15_000 })
    await expect(page).toHaveURL(/\/admin\/onboarding/)

    // 3. Passo 0 — Informações do negócio
    await expect(page.getByText('Informações do negócio')).toBeVisible()
    await page.locator('#name').fill('Barbearia E2E')
    await page.getByRole('button', { name: 'Próximo →' }).click()

    // 4. Passo 1 — Identidade Visual
    await expect(page.getByText('Identidade Visual')).toBeVisible()
    await page.getByRole('button', { name: 'Próximo →' }).click()

    // 5. Passo 2 — Primeiro serviço
    await expect(page.getByText('Primeiro serviço')).toBeVisible()
    await page.locator('#svc-name').fill('Corte de Cabelo')
    await page.getByRole('button', { name: 'Próximo →' }).click()

    // 6. Passo 3 — Primeiro recurso
    await expect(page.getByText('Primeiro recurso')).toBeVisible()
    await page.locator('#res-name').fill('João Barbeiro')
    await page.getByRole('button', { name: 'Próximo →' }).click()

    // 7. Passo 4 — Horários
    const concluirBtn = page.getByRole('button', { name: /Concluir/i })
    await expect(concluirBtn).toBeVisible()
    await concluirBtn.click()

    // 8. Dashboard carregado
    await page.waitForURL(/\/admin\/dashboard/, { timeout: 15_000 })
    await expect(page).toHaveURL(/\/admin\/dashboard/)
  })
})
