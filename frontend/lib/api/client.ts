import { parseJsonBody } from './json'
import { useAuthStore } from '@/store/auth'
import type { TokenPair } from '../types/auth'

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

export { API_URL }

function getSlug(): string {
  if (typeof window === 'undefined') return ''
  return document.cookie
    .split(';')
    .find(c => c.trim().startsWith('tenant_slug='))
    ?.split('=')[1] ?? ''
}

function getToken(): string {
  if (typeof window === 'undefined') return ''
  return document.cookie
    .split(';')
    .find(c => c.trim().startsWith('access_token='))
    ?.split('=')[1] ?? ''
}

// Mesma validade do refresh token (backend): o cookie precisa sobreviver tempo
// suficiente para o interceptor de 401 abaixo conseguir renovar o access token.
function setAccessTokenCookie(accessToken: string) {
  document.cookie = `access_token=${accessToken}; path=/; max-age=${60 * 60 * 24 * 7}`
}

function clearAuthAndRedirectToLogin() {
  document.cookie = 'access_token=; path=/; max-age=0'
  document.cookie = 'tenant_slug=; path=/; max-age=0'
  useAuthStore.getState().clearAuth()
  if (typeof window !== 'undefined') {
    window.location.href = '/login?sessionExpired=1'
  }
}

// Deduplica renovações concorrentes: várias chamadas 401 ao mesmo tempo devem
// esperar UMA renovação, não disparar uma pra cada.
let refreshPromise: Promise<string | null> | null = null

async function refreshAccessToken(): Promise<string | null> {
  const { refreshToken, user, tenantSlug } = useAuthStore.getState()
  if (!refreshToken) return null

  try {
    const res = await fetch(`${API_URL}/api/v1/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    })
    if (!res.ok) return null

    const tokens = (await res.json()) as TokenPair
    setAccessTokenCookie(tokens.accessToken)
    if (user && tenantSlug) useAuthStore.getState().setAuth(user, tokens, tenantSlug)
    return tokens.accessToken
  } catch {
    return null
  }
}

export async function apiFetch<T>(
  path: string,
  options: RequestInit = {},
  isRetry = false
): Promise<T> {
  const token = getToken()
  const slug = getSlug()

  const res = await fetch(`${API_URL}${path}`, {
    ...options,
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(slug ? { 'X-Tenant-Slug': slug } : {}),
      ...options.headers,
    },
  })

  if (res.status === 401 && !isRetry && token) {
    refreshPromise ??= refreshAccessToken().finally(() => { refreshPromise = null })
    const newToken = await refreshPromise
    if (newToken) return apiFetch<T>(path, options, true)

    clearAuthAndRedirectToLogin()
    throw new Error('Sessão expirada. Faça login novamente.')
  }

  if (!res.ok) {
    // `detail` primeiro: no ProblemDetails da API o `title` é o código do erro
    // ("Plan.RentalsNotEnabled") e o `detail` é a frase que o cliente entende.
    const error = await res.json().catch(() => ({} as { detail?: string; title?: string }))
    throw new Error(error.detail ?? error.title ?? res.statusText ?? `HTTP ${res.status}`)
  }

  return parseJsonBody<T>(res)
}

/**
 * Baixa um arquivo de um endpoint autenticado (ex.: export CSV) e dispara o
 * download no navegador. Mesmos cabeçalhos de auth/tenant e renovação de
 * sessão que `apiFetch`.
 */
export async function apiDownload(path: string, fallbackName: string, isRetry = false): Promise<void> {
  const token = getToken()
  const slug = getSlug()

  const res = await fetch(`${API_URL}${path}`, {
    credentials: 'include',
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(slug ? { 'X-Tenant-Slug': slug } : {}),
    },
  })

  if (res.status === 401 && !isRetry && token) {
    refreshPromise ??= refreshAccessToken().finally(() => { refreshPromise = null })
    const newToken = await refreshPromise
    if (newToken) return apiDownload(path, fallbackName, true)

    clearAuthAndRedirectToLogin()
    throw new Error('Sessão expirada. Faça login novamente.')
  }

  if (!res.ok) {
    // `detail` primeiro: no ProblemDetails da API o `title` é o código do erro
    // ("Plan.RentalsNotEnabled") e o `detail` é a frase que o cliente entende.
    const error = await res.json().catch(() => ({} as { detail?: string; title?: string }))
    throw new Error(error.detail ?? error.title ?? res.statusText ?? `HTTP ${res.status}`)
  }

  const blob = await res.blob()
  const disposition = res.headers.get('Content-Disposition') ?? ''
  const match = /filename="?([^"]+)"?/.exec(disposition)
  const filename = match?.[1] ?? fallbackName

  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(url)
}

/**
 * Envia um arquivo por multipart para um endpoint autenticado.
 *
 * Não define Content-Type de propósito: quem escreve o boundary do multipart é o
 * próprio browser, e passar o cabeçalho na mão quebra o parse no servidor.
 * Mesmos cabeçalhos de auth/tenant e renovação de sessão que `apiFetch`.
 */
export async function apiUpload<T>(
  path: string,
  file: File,
  fieldName = 'file',
  isRetry = false
): Promise<T> {
  const token = getToken()
  const slug = getSlug()

  const body = new FormData()
  body.append(fieldName, file, file.name)

  const res = await fetch(`${API_URL}${path}`, {
    method: 'POST',
    credentials: 'include',
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(slug ? { 'X-Tenant-Slug': slug } : {}),
    },
    body,
  })

  if (res.status === 401 && !isRetry && token) {
    refreshPromise ??= refreshAccessToken().finally(() => { refreshPromise = null })
    const newToken = await refreshPromise
    if (newToken) return apiUpload<T>(path, file, fieldName, true)

    clearAuthAndRedirectToLogin()
    throw new Error('Sessão expirada. Faça login novamente.')
  }

  if (!res.ok) {
    // `detail` primeiro: no ProblemDetails da API o `title` é o código do erro
    // ("Image.TooLarge") e o `detail` é a frase que o cliente entende.
    const error = await res.json().catch(() => ({} as { detail?: string; title?: string }))
    throw new Error(error.detail ?? error.title ?? `HTTP ${res.status}`)
  }

  return parseJsonBody<T>(res)
}
