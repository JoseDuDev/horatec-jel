# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: admin-workflow.spec.ts >> Admin workflow: confirm → customer cancel → financeiro >> admin confirma agendamento via UI
- Location: e2e\admin-workflow.spec.ts:71:7

# Error details

```
Error: expect(locator).toBeVisible() failed

Locator: getByRole('button', { name: /^\d{2}:\d{2}$/ }).first()
Expected: visible
Timeout: 10000ms
Error: element(s) not found

Call log:
  - Expect "toBeVisible" with timeout 10000ms
  - waiting for getByRole('button', { name: /^\d{2}:\d{2}$/ }).first()

```

```yaml
- banner:
  - link "e2e-workflow-1781253926786":
    - /url: /e2e-workflow-1781253926786
  - navigation:
    - link "Serviços":
      - /url: /e2e-workflow-1781253926786/servicos
    - link "Agendar":
      - /url: /e2e-workflow-1781253926786/agendar
    - link "Minha Conta":
      - /url: /e2e-workflow-1781253926786/minha-conta
  - text: c
  - button "Sair"
- main:
  - heading "Agendar" [level=1]
  - text: 1 Serviço 2 Recurso 3 Data/Hora 4 Confirmar
  - heading "Escolha a data e horário" [level=2]
  - button "sexta 12 jun"
  - button "sábado 13 jun"
  - button "domingo 14 jun"
  - button "segunda 15 jun"
  - button "terça 16 jun"
  - button "quarta 17 jun"
  - button "quinta 18 jun"
  - button "sexta 19 jun"
  - button "sábado 20 jun"
  - button "domingo 21 jun"
  - button "segunda 22 jun"
  - button "terça 23 jun"
  - button "quarta 24 jun"
  - button "quinta 25 jun"
  - paragraph: Sem horários disponíveis nesta data.
  - button "Voltar"
  - button "Próximo" [disabled]
- contentinfo:
  - paragraph: Powered by Horafy
- alert
```

# Test source

```ts
  1   | import { test, expect } from '@playwright/test'
  2   | import {
  3   |   setupTenant, createService, createResource, linkServiceToResource,
  4   |   setBusinessHoursWeekdays, customerTestLogin,
  5   |   adminStorageState, customerStorageState,
  6   | } from './helpers/api'
  7   | 
  8   | test.describe('Admin workflow: confirm → customer cancel → financeiro', () => {
  9   |   const slug = `e2e-workflow-${Date.now()}`
  10  |   let ownerToken: string
  11  |   let tenantSetup: Awaited<ReturnType<typeof setupTenant>>
  12  |   let customerSetup: Awaited<ReturnType<typeof customerTestLogin>>
  13  |   let bookingId: string
  14  | 
  15  |   test.beforeAll(async ({ browser }) => {
  16  |     tenantSetup = await setupTenant(slug)
  17  |     ownerToken = tenantSetup.ownerToken
  18  | 
  19  |     const serviceId = await createService(ownerToken, slug, {
  20  |       name: 'Massagem',
  21  |       durationMinutes: 60,
  22  |       price: 120,
  23  |     })
  24  |     const resourceId = await createResource(ownerToken, slug, 'Ana Massagista')
  25  |     await linkServiceToResource(ownerToken, slug, resourceId, serviceId)
  26  |     await setBusinessHoursWeekdays(ownerToken, slug)
  27  | 
  28  |     customerSetup = await customerTestLogin(`cliente-workflow-${slug}@e2e.test`, slug)
  29  | 
  30  |     // Customer creates booking via wizard
  31  |     const ctx = await browser.newContext({
  32  |       storageState: customerStorageState(customerSetup) as any,
  33  |     })
  34  |     const page = await ctx.newPage()
  35  |     try {
  36  |       await page.goto(`/${slug}/agendar`)
  37  |       await expect(page.getByText('Massagem')).toBeVisible({ timeout: 15_000 })
  38  |       await page.getByText('Massagem').click()
  39  |       await page.getByRole('button', { name: 'Próximo' }).click()
  40  | 
  41  |       await expect(page.getByText('Ana Massagista')).toBeVisible()
  42  |       await page.getByText('Ana Massagista').click()
  43  |       await page.getByRole('button', { name: 'Próximo' }).click()
  44  | 
  45  |       await expect(page.getByText('Escolha a data e horário')).toBeVisible()
  46  |       const dayBtns = page.locator('.overflow-x-auto').locator('button')
  47  |       let slotFound = false
  48  |       for (let i = 0; i < 14 && !slotFound; i++) {
  49  |         await dayBtns.nth(i).click()
  50  |         await page.waitForTimeout(600)
  51  |         const count = await page.getByRole('button', { name: /^\d{2}:\d{2}$/ }).count()
  52  |         if (count > 0) slotFound = true
  53  |       }
  54  |       const slotButton = page.getByRole('button', { name: /^\d{2}:\d{2}$/ }).first()
> 55  |       await expect(slotButton).toBeVisible({ timeout: 10_000 })
      |                                ^ Error: expect(locator).toBeVisible() failed
  56  |       await slotButton.click()
  57  |       await page.getByRole('button', { name: 'Próximo' }).click()
  58  | 
  59  |       await expect(page.getByText('Confirme seu agendamento')).toBeVisible()
  60  |       await page.getByRole('button', { name: /confirmar/i }).click()
  61  | 
  62  |       await page.waitForURL(/\/agendar\/.+\/status/, { timeout: 15_000 })
  63  |       const url = page.url()
  64  |       bookingId = url.match(/agendar\/([^/]+)\/status/)?.[1] ?? ''
  65  |       expect(bookingId).toBeTruthy()
  66  |     } finally {
  67  |       await ctx.close()
  68  |     }
  69  |   })
  70  | 
  71  |   test('admin confirma agendamento via UI', async ({ browser }) => {
  72  |     const ctx = await browser.newContext({
  73  |       storageState: adminStorageState(tenantSetup) as any,
  74  |     })
  75  |     const page = await ctx.newPage()
  76  |     try {
  77  |       await page.goto('/admin/agendamentos')
  78  | 
  79  |       // Extend "to" date to show future bookings (default is today)
  80  |       await page.locator('input[type="date"]').nth(1).fill('2027-12-31')
  81  |       // Wait for table to reload
  82  |       await page.waitForTimeout(1000)
  83  | 
  84  |       const row = page.locator('tr', { hasText: 'Massagem' }).first()
  85  |       await expect(row).toBeVisible({ timeout: 10_000 })
  86  |       await row.getByRole('button', { name: 'Confirmar' }).click()
  87  | 
  88  |       await expect(row.getByText(/confirmado/i)).toBeVisible({ timeout: 10_000 })
  89  |     } finally {
  90  |       await ctx.close()
  91  |     }
  92  |   })
  93  | 
  94  |   test('cliente cancela agendamento via portal', async ({ browser }) => {
  95  |     const ctx = await browser.newContext({
  96  |       storageState: customerStorageState(customerSetup) as any,
  97  |     })
  98  |     const page = await ctx.newPage()
  99  |     try {
  100 |       await page.goto(`/${slug}/minha-conta`)
  101 |       await expect(page.getByText('Próximos')).toBeVisible({ timeout: 10_000 })
  102 |       await expect(page.getByText('Massagem')).toBeVisible({ timeout: 10_000 })
  103 | 
  104 |       // Step 1: click "Cancelar" ghost button
  105 |       await page.getByRole('button', { name: 'Cancelar' }).first().click()
  106 | 
  107 |       // Step 2: inline confirmation — click "Confirmar cancelamento" (no dialog)
  108 |       await page.getByRole('button', { name: 'Confirmar cancelamento' }).click()
  109 | 
  110 |       // Booking removed from list
  111 |       await expect(page.getByText('Massagem')).not.toBeVisible({ timeout: 10_000 })
  112 |     } finally {
  113 |       await ctx.close()
  114 |     }
  115 |   })
  116 | 
  117 |   test('admin acessa financeiro após cancelamento', async ({ browser }) => {
  118 |     const ctx = await browser.newContext({
  119 |       storageState: adminStorageState(tenantSetup) as any,
  120 |     })
  121 |     const page = await ctx.newPage()
  122 |     try {
  123 |       await page.goto('/admin/financeiro')
  124 |       await expect(page.getByText('Reembolsos')).toBeVisible({ timeout: 10_000 })
  125 |     } finally {
  126 |       await ctx.close()
  127 |     }
  128 |   })
  129 | })
  130 | 
```