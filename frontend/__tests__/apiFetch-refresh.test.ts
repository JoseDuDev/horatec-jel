import { beforeEach, afterEach, describe, it, expect, vi } from 'vitest'
import { useAuthStore } from '@/store/auth'
import { apiFetch } from '@/lib/api/client'

function jsonResponse(body: unknown, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
    text: async () => JSON.stringify(body),
  } as Response
}

describe('apiFetch — renovação silenciosa de sessão (admin)', () => {
  beforeEach(() => {
    document.cookie = 'access_token=old-token; path=/'
    document.cookie = 'tenant_slug=barbearia-do-joao; path=/'
    useAuthStore.setState({
      user: { id: '1', name: 'Dono', email: 'dono@teste.com', role: 'TenantOwner' },
      accessToken: 'old-token',
      refreshToken: 'valid-refresh-token',
      tenantSlug: 'barbearia-do-joao',
    })
  })

  afterEach(() => {
    document.cookie = 'access_token=; path=/; max-age=0'
    document.cookie = 'tenant_slug=; path=/; max-age=0'
    useAuthStore.getState().clearAuth()
    vi.unstubAllGlobals()
  })

  it('em 401, renova o token e repete a chamada original com sucesso', async () => {
    const fetchMock = vi.fn()
      // 1ª chamada: token velho -> 401
      .mockResolvedValueOnce(jsonResponse({ title: 'Unauthorized' }, 401))
      // renovação: refresh token válido -> novo par de tokens
      .mockResolvedValueOnce(jsonResponse({
        accessToken: 'new-token',
        refreshToken: 'new-refresh-token',
        expiresAt: new Date(Date.now() + 3600_000).toISOString(),
      }))
      // repetição da chamada original com o token novo -> sucesso
      .mockResolvedValueOnce(jsonResponse({ ok: true }))
    vi.stubGlobal('fetch', fetchMock)

    const result = await apiFetch<{ ok: boolean }>('/api/v1/bookings')

    expect(result).toEqual({ ok: true })
    expect(fetchMock).toHaveBeenCalledTimes(3)

    // 1ª e 3ª chamadas vão para o mesmo endpoint de negócio.
    expect(fetchMock.mock.calls[0][0]).toContain('/api/v1/bookings')
    expect(fetchMock.mock.calls[2][0]).toContain('/api/v1/bookings')
    // A do meio é a renovação.
    expect(fetchMock.mock.calls[1][0]).toContain('/api/v1/auth/refresh')

    // A chamada repetida usa o token NOVO, não o velho.
    const retryHeaders = fetchMock.mock.calls[2][1].headers
    expect(retryHeaders.Authorization).toBe('Bearer new-token')

    // Cookie e store ficam com o token novo.
    expect(document.cookie).toContain('access_token=new-token')
    expect(useAuthStore.getState().accessToken).toBe('new-token')
    expect(useAuthStore.getState().refreshToken).toBe('new-refresh-token')
  })

  it('em 401 com refresh token também inválido, limpa a sessão e avisa o usuário', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse({ title: 'Unauthorized' }, 401))
      .mockResolvedValueOnce(jsonResponse({ title: 'Invalid refresh token' }, 401))
    vi.stubGlobal('fetch', fetchMock)

    const originalLocation = window.location
    // @ts-expect-error -- jsdom permite reatribuir location para o teste
    delete window.location
    // @ts-expect-error -- stub mínimo só com o que o código usa (href)
    window.location = { href: '' }


    await expect(apiFetch('/api/v1/bookings')).rejects.toThrow('Sessão expirada')

    expect(useAuthStore.getState().accessToken).toBeNull()
    expect(useAuthStore.getState().refreshToken).toBeNull()
    expect(document.cookie).not.toContain('access_token=old-token')
    expect(window.location.href).toContain('/login?sessionExpired=1')

    window.location = originalLocation
  })

  it('chamadas 401 concorrentes compartilham UMA única renovação', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse({}, 401)) // chamada A
      .mockResolvedValueOnce(jsonResponse({}, 401)) // chamada B
      .mockResolvedValueOnce(jsonResponse({        // única renovação
        accessToken: 'new-token',
        refreshToken: 'new-refresh-token',
        expiresAt: new Date(Date.now() + 3600_000).toISOString(),
      }))
      .mockResolvedValueOnce(jsonResponse({ a: true }))  // retry A
      .mockResolvedValueOnce(jsonResponse({ b: true }))  // retry B
    vi.stubGlobal('fetch', fetchMock)

    const [a, b] = await Promise.all([
      apiFetch<{ a: boolean }>('/api/v1/a'),
      apiFetch<{ b: boolean }>('/api/v1/b'),
    ])

    expect(a).toEqual({ a: true })
    expect(b).toEqual({ b: true })
    const refreshCalls = fetchMock.mock.calls.filter(c => String(c[0]).includes('/api/v1/auth/refresh'))
    expect(refreshCalls).toHaveLength(1)
  })
})
