import { headers } from 'next/headers'
import { resolveBrandFromHost, type Brand } from './brand'

/**
 * Resolve a marca ativa no servidor a partir do header `Host` da requisição.
 * Importar `next/headers` torna este módulo server-only (erro de build se
 * usado em Client Component) — os Client Components consomem a marca via
 * `BrandProvider`/`useBrand`.
 */
export async function getActiveBrand(): Promise<Brand> {
  const h = await headers()
  // Atrás de proxy (Caddy) o host original chega em x-forwarded-host; host é fallback.
  const host = h.get('x-forwarded-host') ?? h.get('host')
  return resolveBrandFromHost(host, process.env.BRAND_CONFIG)
}
