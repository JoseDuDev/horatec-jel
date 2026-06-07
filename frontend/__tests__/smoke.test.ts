// Smoke tests — executar após deploy com: SMOKE_API_URL=https://horafy.com.br npm run test:run
// Em CI sem backend ativo, os testes de API são pulados automaticamente.
import { describe, it, expect } from 'vitest'

const BASE = process.env.SMOKE_API_URL

const skip = !BASE

describe.skipIf(skip)('Smoke tests — API endpoints', () => {
  it('health endpoint returns 200', async () => {
    const res = await fetch(`${BASE}/health`)
    expect(res.status).toBe(200)
  })

  it('GET /api/v1/platform/tenants without auth returns 401', async () => {
    const res = await fetch(`${BASE}/api/v1/platform/tenants`)
    expect(res.status).toBe(401)
  })

  it('GET /api/v1/services without tenant slug returns 400 or 404', async () => {
    const res = await fetch(`${BASE}/api/v1/services`)
    expect([400, 404]).toContain(res.status)
  })

  it('GET /api/v1/platform/tenants/{slug} with unknown slug returns 404', async () => {
    const res = await fetch(`${BASE}/api/v1/platform/tenants/slug-que-nao-existe-12345`)
    expect(res.status).toBe(404)
  })
})

describe('Smoke tests — PLAN_LIMITS validation', () => {
  it('has 4 plans defined', async () => {
    const { PLAN_LIMITS } = await import('@/lib/types/platform')
    expect(PLAN_LIMITS).toHaveLength(4)
  })

  it('Free plan has zero price', async () => {
    const { PLAN_LIMITS } = await import('@/lib/types/platform')
    expect(PLAN_LIMITS.find(p => p.plan === 'Free')?.priceMonthly).toBe(0)
  })

  it('Enterprise plan has unlimited services (999)', async () => {
    const { PLAN_LIMITS } = await import('@/lib/types/platform')
    expect(PLAN_LIMITS.find(p => p.plan === 'Enterprise')?.maxServices).toBe(999)
  })
})
