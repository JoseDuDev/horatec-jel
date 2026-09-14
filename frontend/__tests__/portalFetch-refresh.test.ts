import { beforeEach, afterEach, describe, it, expect, vi } from 'vitest'
import { usePortalAuthStore } from '@/store/portal-auth'
import { portalApi } from '@/lib/api/portal'

function jsonResponse(body: unknown, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
    text: async () => JSON.stringify(body),
  } as Response
}

describe('portalFetch — renovação silenciosa de sessão (cliente final)', () => {
  beforeEach(() => {
    usePortalAuthStore.setState({
      customer: { id: 'c1', name: 'Cliente', email: 'cliente@teste.com', phone: null },
      accessToken: 'old-token',
      refreshToken: 'valid-refresh-token',
    })
  })

  afterEach(() => {
    usePortalAuthStore.getState().clearCustomerAuth()
    document.cookie = 'portal_access_token=; path=/; max-age=0'
    vi.unstubAllGlobals()
  })

  it('em 401, renova o token do cliente e repete a chamada original', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse({ title: 'Unauthorized' }, 401))
      .mockResolvedValueOnce(jsonResponse({
        accessToken: 'new-token',
        refreshToken: 'new-refresh-token',
        expiresAt: new Date(Date.now() + 3600_000).toISOString(),
      }))
      .mockResolvedValueOnce(jsonResponse([{ id: 'b1' }]))
    vi.stubGlobal('fetch', fetchMock)

    const bookings = await portalApi.myBookings('barbearia-do-joao', 'old-token')

    expect(bookings).toEqual([{ id: 'b1' }])
    expect(fetchMock).toHaveBeenCalledTimes(3)
    expect(fetchMock.mock.calls[1][0]).toContain('/api/v1/auth/refresh')

    const retryHeaders = fetchMock.mock.calls[2][1].headers
    expect(retryHeaders.Authorization).toBe('Bearer new-token')

    expect(usePortalAuthStore.getState().accessToken).toBe('new-token')
    expect(usePortalAuthStore.getState().refreshToken).toBe('new-refresh-token')
  })

  it('chamadas anônimas (sem token) não tentam renovar em erro', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(jsonResponse({ title: 'Not Found' }, 404))
    vi.stubGlobal('fetch', fetchMock)

    await expect(portalApi.services('barbearia-do-joao')).rejects.toThrow('Not Found')
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })
})
