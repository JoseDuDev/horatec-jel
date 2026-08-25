import { portalApi } from '@/lib/api/portal'
import { usePortalAuthStore } from '@/store/portal-auth'
import type { CustomerProfile } from '@/lib/types/portal'

/**
 * Pós-processamento comum a TODOS os logins do portal (Google, e-mail ou
 * celular): busca o perfil, persiste no store `portal-auth` e grava o cookie
 * `portal_access_token` lido pelo middleware/SSR.
 *
 * Só pode rodar no browser (usa document.cookie).
 */
export async function completePortalLogin(
  slug: string,
  tokens: { accessToken: string; refreshToken: string }
): Promise<CustomerProfile> {
  let profile: CustomerProfile
  try {
    profile = await portalApi.profile(slug, tokens.accessToken)
  } catch {
    // Usuário sem role Customer (ex.: admin do tenant testando o portal):
    // /customers/me responde 403 — caímos para /auth/me, que aceita qualquer role.
    const me = await portalApi.me(slug, tokens.accessToken)
    profile = {
      id: me.id,
      name: me.name ?? me.email,
      email: me.email,
      avatarUrl: me.avatarUrl ?? undefined,
    }
  }

  usePortalAuthStore.getState().setCustomerAuth(profile, tokens.accessToken, tokens.refreshToken)
  // Validade longa (ver CustomerRefreshTokenExpirationDays no backend) — o
  // cliente final não deve perceber a sessão expirando.
  document.cookie = `portal_access_token=${tokens.accessToken}; path=/; max-age=${60 * 60 * 24 * 365}`
  return profile
}
