import { afterEach, describe, it, expect, vi } from 'vitest'
import { apiFetch } from '@/lib/api/client'
import { authApi } from '@/lib/api/auth'

/** Resposta sem corpo, como o `Ok()` do backend devolve: 200 e body vazio. */
function emptyResponse(status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => { throw new SyntaxError('Unexpected end of JSON input') },
    text: async () => '',
  } as unknown as Response
}

describe('apiFetch — respostas sem corpo', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('não estoura em 200 com corpo vazio (Ok() do backend)', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(emptyResponse(200)))

    await expect(apiFetch<void>('/api/v1/auth/forgot-password', { method: 'POST' }))
      .resolves.toBeUndefined()
  })

  it('forgot-password conclui com sucesso quando a API responde 200 sem corpo', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(emptyResponse(200)))

    await expect(authApi.forgotPassword('dono@teste.com')).resolves.toBeUndefined()
  })

  it('continua tratando 204 como resposta sem valor', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(emptyResponse(204)))

    await expect(apiFetch<void>('/api/v1/qualquer', { method: 'DELETE' }))
      .resolves.toBeUndefined()
  })

  it('ainda devolve o JSON quando a resposta tem corpo', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ id: '1' }),
      text: async () => JSON.stringify({ id: '1' }),
    } as Response))

    await expect(apiFetch<{ id: string }>('/api/v1/bookings')).resolves.toEqual({ id: '1' })
  })
})
