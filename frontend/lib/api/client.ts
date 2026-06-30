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

export async function apiFetch<T>(
  path: string,
  options: RequestInit = {}
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

  if (!res.ok) {
    const error = await res.json().catch(() => ({ title: res.statusText }))
    throw new Error(error.title ?? `HTTP ${res.status}`)
  }

  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
}

/**
 * Baixa um arquivo de um endpoint autenticado (ex.: export CSV) e dispara o
 * download no navegador. Mesmos cabeçalhos de auth/tenant que `apiFetch`.
 */
export async function apiDownload(path: string, fallbackName: string): Promise<void> {
  const token = getToken()
  const slug = getSlug()

  const res = await fetch(`${API_URL}${path}`, {
    credentials: 'include',
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(slug ? { 'X-Tenant-Slug': slug } : {}),
    },
  })

  if (!res.ok) {
    const error = await res.json().catch(() => ({ title: res.statusText }))
    throw new Error(error.title ?? `HTTP ${res.status}`)
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
